using ChannelMediator.Generators.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace ChannelMediator.GrpcGenerator;

[Generator]
public class GrpcGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor NotStaticDescriptor = new(
        id: "CMGRPC001",
        title: "GrpcServiceExtension class must be static",
        messageFormat: "Class '{0}' decorated with [GrpcServiceExtension] must be declared as 'static'. Code generation has been skipped.",
        category: "ChannelMediator.GrpcGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NotPartialDescriptor = new(
        id: "CMGRPC002",
        title: "GrpcServiceExtension class must be partial",
        messageFormat: "Class '{0}' decorated with [GrpcServiceExtension] must be declared as 'partial'. Code generation has been skipped.",
        category: "ChannelMediator.GrpcGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidGroupNameDescriptor = new(
        id: "CMGRPC003",
        title: "EndpointApi group name contains invalid characters",
        messageFormat: "Endpoint '{0}' declares GroupName '{1}', but group names may only contain letters and digits. Use only [A-Z], [a-z], [0-9]. Code generation has been skipped.",
        category: "ChannelMediator.GrpcGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // Protocol flag: Grpc = 2, Http = 1, Both = 3
    private const int GrpcProtocolFlag = 2;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var grpcServiceExtensionClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsGrpcServiceExtensionClass(s),
                transform: static (ctx, _) => GetGrpcServiceClass(ctx))
            .Where(static m => m is not null);

        var endpointApiClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsEndpointApiClass(s),
                transform: static (ctx, _) => GetEndpointApiClass(ctx))
            .Where(static m => m is not null);

        var allData = context.CompilationProvider
            .Combine(grpcServiceExtensionClasses.Collect())
            .Combine(endpointApiClasses.Collect());

        context.RegisterSourceOutput(allData,
            static (spc, source) => Execute(source.Left.Left, source.Left.Right!, source.Right!, spc));
    }

    private static bool IsGrpcServiceExtensionClass(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDeclaration
            && classDeclaration.AttributeLists.Count > 0;
    }

    private static bool IsEndpointApiClass(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax or RecordDeclarationSyntax
            && node is BaseTypeDeclarationSyntax { AttributeLists.Count: > 0 };
    }

    private static GrpcServiceInfo? GetGrpcServiceClass(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        foreach (var attributeList in classDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(attribute);
                if (symbolInfo.Symbol is not IMethodSymbol attributeSymbol)
                    continue;

                var fullName = attributeSymbol.ContainingType.ToDisplayString();
                if (fullName != "ChannelMediator.ApiGenerators.Abstraction.GrpcServiceExtensionAttribute")
                    continue;

                var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
                if (classSymbol is null)
                    continue;

                var attributeData = classSymbol.GetAttributes()
                    .FirstOrDefault(ad => ad.AttributeClass?.ToDisplayString() == "ChannelMediator.ApiGenerators.Abstraction.GrpcServiceExtensionAttribute");

                var scanAssemblies = GetAttributeArrayValue(attributeData, "ScanAssemblies");

                var isStatic = false;
                var isPartial = false;
                foreach (var modifier in classDeclaration.Modifiers)
                {
                    if (modifier.IsKind(SyntaxKind.StaticKeyword)) isStatic = true;
                    else if (modifier.IsKind(SyntaxKind.PartialKeyword)) isPartial = true;
                }

                return new GrpcServiceInfo
                {
                    ClassName = classSymbol.Name,
                    Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
                    ScanAssemblies = scanAssemblies,
                    IsStatic = isStatic,
                    IsPartial = isPartial,
                    Location = classDeclaration.GetLocation()
                };
            }
        }

        return null;
    }

    private static GrpcEndpointInfo? GetEndpointApiClass(GeneratorSyntaxContext context)
    {
        var typeDeclaration = (BaseTypeDeclarationSyntax)context.Node;

        foreach (var attributeList in typeDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(attribute);
                if (symbolInfo.Symbol is not IMethodSymbol attributeSymbol)
                    continue;

                var fullName = attributeSymbol.ContainingType.ToDisplayString();
                if (fullName != "ChannelMediator.ApiGenerators.Abstraction.EndpointApiAttribute")
                    continue;

                var typeSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
                if (typeSymbol is null)
                    continue;

                var attributeData = typeSymbol.GetAttributes()
                    .FirstOrDefault(ad => ad.AttributeClass?.ToDisplayString() == "ChannelMediator.ApiGenerators.Abstraction.EndpointApiAttribute");

                var protocol = GetAttributeValue<int>(attributeData, "Protocol");
                // Only include if Grpc flag is set (bit 2)
                if ((protocol & GrpcProtocolFlag) == 0)
                    return null;

                var groupName = GetAttributeValue<string>(attributeData, "GroupName") ?? "Default";
                var hasExplicitGroupName = attributeData?.NamedArguments.Any(na => na.Key == "GroupName") == true;

                var (responseTypeName, isStream) = ExtractResponseTypeInfo(typeSymbol);
                var methodName = CreateMethodName(typeSymbol.Name);

                return new GrpcEndpointInfo
                {
                    RequestTypeName = typeSymbol.ToDisplayString(),
                    RequestShortName = typeSymbol.Name,
                    GroupName = groupName,
                    HasExplicitGroupName = hasExplicitGroupName,
                    Location = typeDeclaration.GetLocation(),
                    ResponseTypeName = responseTypeName,
                    IsStream = isStream,
                    MethodName = methodName
                };
            }
        }

        return null;
    }

    private static (string typeName, bool isStream) ExtractResponseTypeInfo(INamedTypeSymbol typeSymbol)
    {
        var iStreamInterface = typeSymbol.AllInterfaces
            .FirstOrDefault(i => i.Name == "IStreamRequest" && i.TypeArguments.Length == 1);

        if (iStreamInterface != null)
        {
            return (iStreamInterface.TypeArguments[0].ToDisplayString(), true);
        }

        var iRequestInterface = typeSymbol.AllInterfaces
            .FirstOrDefault(i => i.Name == "IRequest" && i.TypeArguments.Length == 1);

        if (iRequestInterface != null)
        {
            return (iRequestInterface.TypeArguments[0].ToDisplayString(), false);
        }

        return ("object", false);
    }

    private static string CreateMethodName(string requestTypeName)
    {
        return requestTypeName.EndsWith("Request", StringComparison.Ordinal)
            ? requestTypeName.Substring(0, requestTypeName.Length - "Request".Length)
            : requestTypeName;
    }

    private static void Execute(
        Compilation compilation,
        ImmutableArray<GrpcServiceInfo?> serviceClasses,
        ImmutableArray<GrpcEndpointInfo?> endpointApis,
        SourceProductionContext context)
    {
        if (serviceClasses.IsDefaultOrEmpty)
            return;

        var distinctServiceClasses = serviceClasses.Where(m => m is not null).Select(m => m!).Distinct();
        var localEndpoints = endpointApis.IsDefaultOrEmpty
            ? Enumerable.Empty<GrpcEndpointInfo>()
            : endpointApis.Where(e => e is not null).Select(e => e!);

        foreach (var serviceClass in distinctServiceClasses)
        {
            if (!serviceClass.IsStatic)
            {
                context.ReportDiagnostic(Diagnostic.Create(NotStaticDescriptor, serviceClass.Location, serviceClass.ClassName));
                continue;
            }

            if (!serviceClass.IsPartial)
            {
                context.ReportDiagnostic(Diagnostic.Create(NotPartialDescriptor, serviceClass.Location, serviceClass.ClassName));
                continue;
            }

            var referencedEndpoints = GetEndpointApisFromReferencedAssemblies(compilation, serviceClass.ScanAssemblies);
            var allEndpoints = localEndpoints.Concat(referencedEndpoints).Distinct().ToList();

            var hasInvalidGroupNames = false;
            foreach (var endpoint in allEndpoints.Where(IsInvalidGroupName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidGroupNameDescriptor,
                    endpoint.Location,
                    endpoint.RequestShortName,
                    endpoint.GroupName));
                hasInvalidGroupNames = true;
            }

            if (hasInvalidGroupNames || !allEndpoints.Any())
                continue;

            var source = GenerateGrpcServiceExtension(serviceClass, allEndpoints);
            context.AddSource($"Grpc{serviceClass.ClassName}.g.cs", source);
        }
    }

    private static List<GrpcEndpointInfo> GetEndpointApisFromReferencedAssemblies(Compilation compilation, string[] scanAssemblies)
    {
        var results = new List<GrpcEndpointInfo>();
        var endpointApiAttributeName = "ChannelMediator.ApiGenerators.Abstraction.EndpointApiAttribute";
        var filterByAssembly = scanAssemblies.Length > 0;

        foreach (var reference in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (filterByAssembly && !scanAssemblies.Contains(reference.Name))
                continue;

            var types = GetAllNamedTypes(reference.GlobalNamespace);
            foreach (var typeSymbol in types)
            {
                var attributeData = typeSymbol.GetAttributes()
                    .FirstOrDefault(ad => ad.AttributeClass?.ToDisplayString() == endpointApiAttributeName);

                if (attributeData is null)
                    continue;

                var protocol = GetAttributeValue<int>(attributeData, "Protocol");
                if ((protocol & GrpcProtocolFlag) == 0)
                    continue;

                var groupName = GetAttributeValue<string>(attributeData, "GroupName") ?? "Default";
                var hasExplicitGroupName = attributeData.NamedArguments.Any(na => na.Key == "GroupName");

                var (responseTypeName, isStream) = ExtractResponseTypeInfo(typeSymbol);
                var methodName = CreateMethodName(typeSymbol.Name);

                results.Add(new GrpcEndpointInfo
                {
                    RequestTypeName = typeSymbol.ToDisplayString(),
                    RequestShortName = typeSymbol.Name,
                    GroupName = groupName,
                    HasExplicitGroupName = hasExplicitGroupName,
                    Location = typeSymbol.Locations.FirstOrDefault(),
                    ResponseTypeName = responseTypeName,
                    IsStream = isStream,
                    MethodName = methodName
                });
            }
        }

        return results;
    }

    private static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol namespaceSymbol)
    {
        foreach (var member in namespaceSymbol.GetMembers())
        {
            if (member is INamedTypeSymbol namedType)
                yield return namedType;
            else if (member is INamespaceSymbol childNamespace)
            {
                foreach (var type in GetAllNamedTypes(childNamespace))
                    yield return type;
            }
        }
    }

    private static bool IsInvalidGroupName(GrpcEndpointInfo endpoint)
    {
        if (!endpoint.HasExplicitGroupName)
            return false;

        return string.IsNullOrWhiteSpace(endpoint.GroupName)
            || endpoint.GroupName.Any(c => !char.IsLetterOrDigit(c));
    }

    private static T? GetAttributeValue<T>(AttributeData? attributeData, string propertyName)
    {
        if (attributeData is null)
            return default;

        var namedArgument = attributeData.NamedArguments
            .FirstOrDefault(na => na.Key == propertyName);

        if (namedArgument.Value.Value is T value)
            return value;

        return default;
    }

    private static string[] GetAttributeArrayValue(AttributeData? attributeData, string propertyName)
    {
        if (attributeData is null)
            return Array.Empty<string>();

        var namedArgument = attributeData.NamedArguments
            .FirstOrDefault(na => na.Key == propertyName);

        if (namedArgument.Value.IsNull || namedArgument.Value.Values.IsDefaultOrEmpty)
            return Array.Empty<string>();

        return namedArgument.Value.Values
            .Where(v => v.Value is string)
            .Select(v => (string)v.Value!)
            .ToArray();
    }

    private static string GenerateGrpcServiceExtension(GrpcServiceInfo serviceClass, List<GrpcEndpointInfo> endpoints)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using ChannelMediator;");
        sb.AppendLine("using Microsoft.AspNetCore.Builder;");
        sb.AppendLine("using Microsoft.AspNetCore.Routing;");
        sb.AppendLine("using ProtoBuf.Grpc.Server;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();
        sb.AppendLine($"namespace {serviceClass.Namespace};");
        sb.AppendLine();

        var groups = endpoints.GroupBy(e => e.GroupName).OrderBy(g => g.Key).ToList();

        // Emit service interfaces
        foreach (var group in groups)
        {
            AppendServiceInterface(sb, group.Key, group.ToList());
            sb.AppendLine();
        }

        // Emit service implementations
        foreach (var group in groups)
        {
            AppendServiceImpl(sb, group.Key, group.ToList());
            sb.AppendLine();
        }

        // Emit mapper extension
        sb.AppendLine($"public static partial class {serviceClass.ClassName}");
        sb.AppendLine("{");
        sb.AppendLine($"    public static void Map{serviceClass.ClassName}GrpcServices(this IEndpointRouteBuilder routes)");
        sb.AppendLine("    {");
        foreach (var group in groups)
        {
            sb.AppendLine($"        routes.MapGrpcService<{group.Key}ServiceImpl>();");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void AppendServiceInterface(StringBuilder sb, string groupName, List<GrpcEndpointInfo> endpoints)
    {
        sb.AppendLine("[global::System.ServiceModel.ServiceContract]");
        sb.AppendLine($"public interface I{groupName}Service");
        sb.AppendLine("{");

        foreach (var endpoint in endpoints)
        {
            sb.AppendLine("    [global::System.ServiceModel.OperationContract]");
            if (endpoint.IsStream)
            {
                sb.AppendLine($"    global::System.Collections.Generic.IAsyncEnumerable<{endpoint.ResponseTypeName}> {endpoint.MethodName}({endpoint.RequestTypeName} request, ProtoBuf.Grpc.CallContext context = default);");
            }
            else
            {
                sb.AppendLine($"    global::System.Threading.Tasks.Task<{endpoint.ResponseTypeName}> {endpoint.MethodName}({endpoint.RequestTypeName} request, ProtoBuf.Grpc.CallContext context = default);");
            }
        }

        sb.AppendLine("}");
    }

    private static void AppendServiceImpl(StringBuilder sb, string groupName, List<GrpcEndpointInfo> endpoints)
    {
        sb.AppendLine($"internal class {groupName}ServiceImpl : I{groupName}Service");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly IMediator _mediator;");
        sb.AppendLine();
        sb.AppendLine($"    public {groupName}ServiceImpl(IMediator mediator) {{ _mediator = mediator; }}");
        sb.AppendLine();

        foreach (var endpoint in endpoints)
        {
            if (endpoint.IsStream)
            {
                sb.AppendLine($"    public async global::System.Collections.Generic.IAsyncEnumerable<{endpoint.ResponseTypeName}> {endpoint.MethodName}({endpoint.RequestTypeName} request, ProtoBuf.Grpc.CallContext context = default)");
                sb.AppendLine("    {");
                sb.AppendLine($"        await foreach (var item in _mediator.CreateStream(request, context.CancellationToken))");
                sb.AppendLine("            yield return item;");
                sb.AppendLine("    }");
            }
            else
            {
                sb.AppendLine($"    public async global::System.Threading.Tasks.Task<{endpoint.ResponseTypeName}> {endpoint.MethodName}({endpoint.RequestTypeName} request, ProtoBuf.Grpc.CallContext context = default)");
                sb.AppendLine($"        => await _mediator.Send(request, context.CancellationToken);");
            }
            sb.AppendLine();
        }

        sb.AppendLine("}");
    }
}
