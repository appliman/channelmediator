using ChannelMediator.Generators.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace ChannelMediator.MinimalApiGenerator;

[Generator]
public class MinimalApiGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor NotStaticDescriptor = new(
        id: "CMAPI001",
        title: "MapApiExtension class must be static",
        messageFormat: "Class '{0}' decorated with [MapApiExtension] must be declared as 'static'. Code generation has been skipped.",
        category: "ChannelMediator.MinimalApiGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NotPartialDescriptor = new(
        id: "CMAPI002",
        title: "MapApiExtension class must be partial",
        messageFormat: "Class '{0}' decorated with [MapApiExtension] must be declared as 'partial'. Code generation has been skipped.",
        category: "ChannelMediator.MinimalApiGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidGroupNameDescriptor = new(
        id: "CMAPI003",
        title: "EndpointApi group name contains invalid characters",
        messageFormat: "Endpoint '{0}' declares GroupName '{1}', but group names may only contain letters and digits. Use only [A-Z], [a-z], [0-9]. Code generation has been skipped.",
        category: "ChannelMediator.MinimalApiGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidEntityNameDescriptor = new(
        id: "CMAPI004",
        title: "EndpointApi entity name is invalid",
        messageFormat: "Endpoint '{0}' declares EntityName '{1}', but entity names must be lowercase and valid in a URL path segment. Use only [a-z], [0-9], '-' or '_'. Code generation has been skipped.",
        category: "ChannelMediator.MinimalApiGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var mapApiExtensionClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsMapApiExtensionClass(s),
                transform: static (ctx, _) => GetMapApiExtensionClass(ctx))
            .Where(static m => m is not null);

        var endpointApiClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsEndpointApiClass(s),
                transform: static (ctx, _) => GetEndpointApiClass(ctx))
            .Where(static m => m is not null);

        var allData = context.CompilationProvider
            .Combine(mapApiExtensionClasses.Collect())
            .Combine(endpointApiClasses.Collect());

        context.RegisterSourceOutput(allData,
            static (spc, source) => Execute(source.Left.Left, source.Left.Right!, source.Right!, spc));
    }

    private static bool IsMapApiExtensionClass(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDeclaration
            && classDeclaration.AttributeLists.Count > 0;
    }

    private static bool IsEndpointApiClass(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax or RecordDeclarationSyntax
            && node is BaseTypeDeclarationSyntax { AttributeLists.Count: > 0 };
    }

    private static MapApiExtensionInfo? GetMapApiExtensionClass(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        foreach (var attributeList in classDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(attribute);
                if (symbolInfo.Symbol is not IMethodSymbol attributeSymbol)
                {
                    continue;
                }

                var attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                var fullName = attributeContainingTypeSymbol.ToDisplayString();

                if (fullName == "ChannelMediator.MinimalApiGenerator.Abstraction.MapApiExtensionAttribute")
                {
                    var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
                    if (classSymbol is null)
                    {
                        continue;
                    }

                    var attributeData = classSymbol.GetAttributes()
                        .FirstOrDefault(ad => ad.AttributeClass?.ToDisplayString() == "ChannelMediator.MinimalApiGenerator.Abstraction.MapApiExtensionAttribute");

                    var withVersionning = GetAttributeValue<bool>(attributeData, "WithVersionning");
                    var scanAssemblies = GetAttributeArrayValue(attributeData, "ScanAssemblies");

                    var isStatic = false;
                    var isPartial = false;
                    foreach (var modifier in classDeclaration.Modifiers)
                    {
                        if (modifier.IsKind(SyntaxKind.StaticKeyword)) isStatic = true;
                        else if (modifier.IsKind(SyntaxKind.PartialKeyword)) isPartial = true;
                    }

                    return new MapApiExtensionInfo
                    {
                        ClassName = classSymbol.Name,
                        Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
                        WithVersionning = withVersionning,
                        ScanAssemblies = scanAssemblies,
                        IsStatic = isStatic,
                        IsPartial = isPartial,
                        Location = classDeclaration.GetLocation()
                    };
                }
            }
        }

        return null;
    }

    private static EndpointApiInfo? GetEndpointApiClass(GeneratorSyntaxContext context)
    {
        var typeDeclaration = (BaseTypeDeclarationSyntax)context.Node;

        foreach (var attributeList in typeDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(attribute);
                if (symbolInfo.Symbol is not IMethodSymbol attributeSymbol)
                {
                    continue;
                }

                var attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                var fullName = attributeContainingTypeSymbol.ToDisplayString();

                if (fullName == "ChannelMediator.MinimalApiGenerator.Abstraction.EndpointApiAttribute")
                {
                    var typeSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
                    if (typeSymbol is null)
                    {
                        continue;
                    }

                    var attributeData = typeSymbol.GetAttributes()
                        .FirstOrDefault(ad => ad.AttributeClass?.ToDisplayString() == "ChannelMediator.MinimalApiGenerator.Abstraction.EndpointApiAttribute");

                    var groupName = GetAttributeValue<string>(attributeData, "GroupName") ?? "Default";
                    var hasExplicitGroupName = attributeData?.NamedArguments.Any(na => na.Key == "GroupName") == true;
                    var entityName = GetAttributeValue<string>(attributeData, "EntityName") ?? CreateDefaultEntityName(typeSymbol.Name);
                    var hasExplicitEntityName = attributeData?.NamedArguments.Any(na => na.Key == "EntityName") == true;
                    var tags = GetAttributeArrayValue(attributeData, "Tags");
                    var summary = GetAttributeValue<string>(attributeData, "Summary");
                    var description = GetAttributeValue<string>(attributeData, "Description");
                    var authenticationSchemes = GetAttributeArrayValue(attributeData, "AuthenticationSchemes");
                    var useHttpStandardVerbs = GetAttributeValue<bool>(attributeData, "UseHttpStandardVerbs");

                    var httpVerb = "POST";
                    var parameters = new List<RequestParameter>();

                    if (useHttpStandardVerbs)
                    {
                        if (typeSymbol.Name.StartsWith("Get"))
                        {
                            httpVerb = "GET";
                        }
                        else if (typeSymbol.Name.StartsWith("Delete"))
                        {
                            httpVerb = "DELETE";
                        }
                        else if (typeSymbol.Name.StartsWith("Put") || typeSymbol.Name.StartsWith("Update"))
                        {
                            httpVerb = "PUT";
                        }
                        else if (typeSymbol.Name.StartsWith("Post") || typeSymbol.Name.StartsWith("Create") || typeSymbol.Name.StartsWith("Save"))
                        {
                            httpVerb = "POST";
                        }
                    }

                    var (isResponseNullable, responseTypeName, isStream) = ExtractResponseTypeInfo(typeSymbol);

                    if (httpVerb == "GET" || httpVerb == "DELETE" || isStream)
                    {
                        parameters = ExtractRecordParameters(typeSymbol);
                    }

                    return new EndpointApiInfo
                    {
                        RequestTypeName = typeSymbol.ToDisplayString(),
                        RequestShortName = typeSymbol.Name,
                        Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
                        GroupName = groupName,
                        HasExplicitGroupName = hasExplicitGroupName,
                        Location = typeDeclaration.GetLocation(),
                        EntityName = entityName,
                        HasExplicitEntityName = hasExplicitEntityName,
                        Tags = tags,
                        Summary = summary,
                        Description = description,
                        AuthenticationSchemes = authenticationSchemes,
                        HttpVerb = httpVerb,
                        UseHttpStandardVerbs = useHttpStandardVerbs,
                        Parameters = parameters,
                        IsResponseNullable = isResponseNullable,
                        ResponseTypeName = responseTypeName,
                        IsStream = isStream
                    };
                }
            }
        }

        return null;
    }

    private static List<RequestParameter> ExtractRecordParameters(INamedTypeSymbol typeSymbol)
    {
        var parameters = new List<RequestParameter>();

        var primaryConstructor = typeSymbol.Constructors
            .FirstOrDefault(c => c.MethodKind == MethodKind.Constructor
                && !c.IsImplicitlyDeclared
                && c.Parameters.Length > 0
                && !RoslynHelpers.IsRecordCopyConstructor(c, typeSymbol));

        if (primaryConstructor != null)
        {
            foreach (var param in primaryConstructor.Parameters)
            {
                parameters.Add(new RequestParameter
                {
                    Name = param.Name,
                    Type = param.Type.ToDisplayString()
                });
            }
        }

        return parameters;
    }

    private static (bool isNullable, string typeName, bool isStream) ExtractResponseTypeInfo(INamedTypeSymbol typeSymbol)
    {
        var iStreamInterface = typeSymbol.AllInterfaces
            .FirstOrDefault(i => i.Name == "IStreamRequest" && i.TypeArguments.Length == 1);

        if (iStreamInterface != null)
        {
            var responseType = iStreamInterface.TypeArguments[0];
            return (false, responseType.ToDisplayString(), true);
        }

        var iRequestInterface = typeSymbol.AllInterfaces
            .FirstOrDefault(i => i.Name == "IRequest" && i.TypeArguments.Length == 1);

        if (iRequestInterface != null && iRequestInterface.TypeArguments.Length > 0)
        {
            var responseType = iRequestInterface.TypeArguments[0];
            var isNullable = responseType.NullableAnnotation == NullableAnnotation.Annotated;
            var typeName = responseType.ToDisplayString();

            return (isNullable, typeName, false);
        }

        return (false, "object", false);
    }

    private static void Execute(Compilation compilation, ImmutableArray<MapApiExtensionInfo?> mapApiClasses, ImmutableArray<EndpointApiInfo?> endpointApis, SourceProductionContext context)
    {
        if (mapApiClasses.IsDefaultOrEmpty)
        {
            return;
        }

        var distinctMapApiClasses = mapApiClasses.Where(m => m is not null).Select(m => m!).Distinct();

        // Merge endpoints from local syntax and referenced assemblies
        var localEndpoints = endpointApis.IsDefaultOrEmpty
            ? Enumerable.Empty<EndpointApiInfo>()
            : endpointApis.Where(e => e is not null).Select(e => e!);

        foreach (var mapApiClass in distinctMapApiClasses)
        {
            // Validate that the target class is both static and partial
            if (!mapApiClass.IsStatic)
            {
                context.ReportDiagnostic(Diagnostic.Create(NotStaticDescriptor, mapApiClass.Location, mapApiClass.ClassName));
                continue;
            }

            if (!mapApiClass.IsPartial)
            {
                context.ReportDiagnostic(Diagnostic.Create(NotPartialDescriptor, mapApiClass.Location, mapApiClass.ClassName));
                continue;
            }

            var referencedEndpoints = GetEndpointApisFromReferencedAssemblies(compilation, mapApiClass.ScanAssemblies);
            var distinctEndpointApis = localEndpoints.Concat(referencedEndpoints).Distinct().ToList();

            var hasInvalidGroupNames = false;
            foreach (var endpoint in distinctEndpointApis.Where(IsInvalidGroupName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidGroupNameDescriptor,
                    endpoint.Location,
                    endpoint.RequestShortName,
                    endpoint.GroupName));
                hasInvalidGroupNames = true;
            }

            if (hasInvalidGroupNames)
            {
                continue;
            }

            if (!distinctEndpointApis.Any())
            {
                continue;
            }

            var source = GenerateApiMapperExtension(mapApiClass, distinctEndpointApis);
            context.AddSource($"Map{mapApiClass.ClassName}.g.cs", source);
        }
    }

    private static List<EndpointApiInfo> GetEndpointApisFromReferencedAssemblies(Compilation compilation, string[] scanAssemblies)
    {
        var results = new List<EndpointApiInfo>();
        var endpointApiAttributeName = "ChannelMediator.MinimalApiGenerator.Abstraction.EndpointApiAttribute";
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

                var groupName = GetAttributeValue<string>(attributeData, "GroupName") ?? "Default";
                var hasExplicitGroupName = attributeData.NamedArguments.Any(na => na.Key == "GroupName");
                var entityName = GetAttributeValue<string>(attributeData, "EntityName") ?? CreateDefaultEntityName(typeSymbol.Name);
                var hasExplicitEntityName = attributeData.NamedArguments.Any(na => na.Key == "EntityName");
                var tags = GetAttributeArrayValue(attributeData, "Tags");
                var summary = GetAttributeValue<string>(attributeData, "Summary");
                var description = GetAttributeValue<string>(attributeData, "Description");
                var authenticationSchemes = GetAttributeArrayValue(attributeData, "AuthenticationSchemes");
                var useHttpStandardVerbs = GetAttributeValue<bool>(attributeData, "UseHttpStandardVerbs");

                var httpVerb = "POST";
                var parameters = new List<RequestParameter>();

                if (useHttpStandardVerbs)
                {
                    if (typeSymbol.Name.StartsWith("Get"))
                        httpVerb = "GET";
                    else if (typeSymbol.Name.StartsWith("Delete"))
                        httpVerb = "DELETE";
                    else if (typeSymbol.Name.StartsWith("Put") || typeSymbol.Name.StartsWith("Update"))
                        httpVerb = "PUT";
                    else if (typeSymbol.Name.StartsWith("Post") || typeSymbol.Name.StartsWith("Create") || typeSymbol.Name.StartsWith("Save"))
                        httpVerb = "POST";
                }

                if (httpVerb == "GET" || httpVerb == "DELETE")
                {
                    parameters = ExtractRecordParameters(typeSymbol);
                }

                var (isResponseNullable, responseTypeName, isStream) = ExtractResponseTypeInfo(typeSymbol);

                if (httpVerb == "GET" || httpVerb == "DELETE" || isStream)
                {
                    parameters = ExtractRecordParameters(typeSymbol);
                }

                results.Add(new EndpointApiInfo
                {
                    RequestTypeName = typeSymbol.ToDisplayString(),
                    RequestShortName = typeSymbol.Name,
                    Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
                    GroupName = groupName,
                    HasExplicitGroupName = hasExplicitGroupName,
                    Location = typeSymbol.Locations.FirstOrDefault(),
                    EntityName = entityName,
                    HasExplicitEntityName = hasExplicitEntityName,
                    Tags = tags,
                    Summary = summary,
                    Description = description,
                    AuthenticationSchemes = authenticationSchemes,
                    HttpVerb = httpVerb,
                    UseHttpStandardVerbs = useHttpStandardVerbs,
                    Parameters = parameters,
                    IsResponseNullable = isResponseNullable,
                    ResponseTypeName = responseTypeName,
                    IsStream = isStream
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

    private static T? GetAttributeValue<T>(AttributeData? attributeData, string propertyName)
    {
        if (attributeData is null)
        {
            return default;
        }

        var namedArgument = attributeData.NamedArguments
            .FirstOrDefault(na => na.Key == propertyName);

        if (namedArgument.Value.Value is T value)
        {
            return value;
        }

        return default;
    }

    private static List<string> GetAttributeListValue(AttributeData? attributeData, string propertyName)
    {
        if (attributeData is null)
        {
            return new List<string>();
        }

        var namedArgument = attributeData.NamedArguments
            .FirstOrDefault(na => na.Key == propertyName);

        if (namedArgument.Value.IsNull)
        {
            return new List<string>();
        }

        var values = namedArgument.Value.Values;
        if (values.IsDefaultOrEmpty)
        {
            return new List<string>();
        }

        return values
            .Where(v => v.Value is string)
            .Select(v => (string)v.Value!)
            .ToList();
    }

    private static string[] GetAttributeArrayValue(AttributeData? attributeData, string propertyName)
    {
        return GetAttributeListValue(attributeData, propertyName).ToArray();
    }

    private static bool IsInvalidGroupName(EndpointApiInfo endpoint)
    {
        if (!endpoint.HasExplicitGroupName)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(endpoint.GroupName)
            || endpoint.GroupName.Any(character => !char.IsLetterOrDigit(character));
    }

    private static bool IsInvalidEntityName(EndpointApiInfo endpoint)
    {
        if (!endpoint.HasExplicitEntityName)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(endpoint.EntityName))
        {
            return true;
        }

        return endpoint.EntityName.Any(character => !char.IsLower(character) && !char.IsDigit(character) && character != '-' && character != '_');
    }

    private static string CreateDefaultEntityName(string typeName)
    {
        var baseName = typeName.EndsWith("Request", StringComparison.Ordinal)
            ? typeName.Substring(0, typeName.Length - "Request".Length)
            : typeName;

        return ToKebabCase(baseName);
    }

    private static string ToKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "endpoint";
        }

        var sb = new StringBuilder(value.Length + 8);

        for (int i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (!char.IsLetterOrDigit(character))
            {
                if (sb.Length > 0 && sb[sb.Length - 1] != '-')
                {
                    sb.Append('-');
                }

                continue;
            }

            var shouldInsertSeparator = sb.Length > 0
                && sb[sb.Length - 1] != '-'
                && char.IsUpper(character)
                && (char.IsLower(value[i - 1]) || (i + 1 < value.Length && char.IsLower(value[i + 1])));

            if (shouldInsertSeparator)
            {
                sb.Append('-');
            }

            sb.Append(char.ToLowerInvariant(character));
        }

        if (sb.Length > 0 && sb[sb.Length - 1] == '-')
        {
            sb.Length--;
        }

        return sb.Length == 0 ? "endpoint" : sb.ToString();
    }

    private static string GenerateApiMapperExtension(MapApiExtensionInfo mapApiClass, List<EndpointApiInfo> endpoints)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using ChannelMediator;");
        sb.AppendLine("using Microsoft.AspNetCore.Builder;");
        sb.AppendLine("using Microsoft.AspNetCore.Routing;");
        sb.AppendLine("using Microsoft.AspNetCore.Http;");
        sb.AppendLine("using Microsoft.AspNetCore.Authorization;");

        if (mapApiClass.WithVersionning)
        {
            sb.AppendLine("using Asp.Versioning.Builder;");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {mapApiClass.Namespace};");
        sb.AppendLine();
        sb.AppendLine($"public static partial class {mapApiClass.ClassName}");
        sb.AppendLine("{");

        var groupedEndpoints = endpoints.Where(e => e.HasExplicitGroupName).GroupBy(e => e.GroupName).OrderBy(g => g.Key).ToList();
        var ungroupedEndpoints = endpoints.Where(e => !e.HasExplicitGroupName).ToList();
        var useGroupedMethods = groupedEndpoints.Count > 0;

        AppendMapMethodSignature(sb, mapApiClass, $"Map{mapApiClass.ClassName}");
        sb.AppendLine("    {");

        if (useGroupedMethods)
        {
            foreach (var group in groupedEndpoints)
            {
                var methodName = GetGroupMethodName(mapApiClass.ClassName, group.Key);
                sb.AppendLine(mapApiClass.WithVersionning
                    ? $"        routes.{methodName}(versionSet);"
                    : $"        routes.{methodName}();");
            }

            if (ungroupedEndpoints.Count > 0)
            {
                AppendEndpointMappings(sb, ungroupedEndpoints, "routes", true);
            }
        }
        else
        {
            AppendEndpointMappings(sb, ungroupedEndpoints, "routes", true);
        }

        sb.AppendLine("    }");

        if (useGroupedMethods)
        {
            foreach (var group in groupedEndpoints)
            {
                sb.AppendLine();
                AppendMapMethodSignature(sb, mapApiClass, GetGroupMethodName(mapApiClass.ClassName, group.Key));
                sb.AppendLine("    {");
                AppendEndpointMappings(sb, group.ToList(), "routes", true);
                sb.AppendLine("    }");
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void AppendMapMethodSignature(StringBuilder sb, MapApiExtensionInfo mapApiClass, string methodName)
    {
        if (mapApiClass.WithVersionning)
        {
            sb.AppendLine($"    public static void {methodName}(this IEndpointRouteBuilder routes, ApiVersionSet versionSet)");
            return;
        }

        sb.AppendLine($"    public static void {methodName}(this IEndpointRouteBuilder routes)");
    }

    private static string GetGroupMethodName(string className, string groupName)
    {
        return $"Map{className}{SanitizeIdentifierPart(groupName)}";
    }

    private static string SanitizeIdentifierPart(string value)
    {
        var sb = new StringBuilder(value.Length);
        var capitalizeNext = true;

        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character))
            {
                capitalizeNext = true;
                continue;
            }

            sb.Append(capitalizeNext ? char.ToUpperInvariant(character) : character);
            capitalizeNext = false;
        }

        if (sb.Length == 0)
        {
            return "Group";
        }

        if (!char.IsLetter(sb[0]) && sb[0] != '_')
        {
            sb.Insert(0, '_');
        }

        return sb.ToString();
    }

    private static void AppendEndpointMappings(StringBuilder sb, List<EndpointApiInfo> endpoints, string routeBuilderExpression, bool createGroupRoute)
    {
        if (createGroupRoute)
        {
            var groupName = endpoints[0].GroupName;
            var groupVariableName = $"{SanitizeIdentifierPart(groupName).ToLowerInvariant()}Group";
            var firstEndpoint = endpoints[0];
            var groupChain = new List<string>
            {
                $"        var {groupVariableName} = {routeBuilderExpression}.MapGroup(\"/api/{groupName.ToLowerInvariant()}\")"
            };

            if (firstEndpoint.Tags.Any())
            {
                var tagsString = string.Join("\", \"", firstEndpoint.Tags);
                groupChain.Add($"            .WithTags(\"{tagsString}\")");
            }

            if (firstEndpoint.AuthenticationSchemes.Any())
            {
                var schemesString = string.Join(",", firstEndpoint.AuthenticationSchemes);
                groupChain.Add($"            .RequireAuthorization(new AuthorizeAttribute {{ AuthenticationSchemes = \"{schemesString}\" }})");
            }

            sb.AppendLine();
            sb.AppendLine($"        // Group: {groupName}");
            for (int i = 0; i < groupChain.Count - 1; i++)
            {
                sb.AppendLine(groupChain[i]);
            }

            sb.AppendLine(groupChain[groupChain.Count - 1] + ";");
            sb.AppendLine();

            AppendEndpointMappingsCore(sb, endpoints, groupVariableName);
            return;
        }

        AppendEndpointMappingsCore(sb, endpoints, routeBuilderExpression);
    }

    private static void AppendEndpointMappingsCore(StringBuilder sb, List<EndpointApiInfo> endpoints, string routeBuilderExpression)
    {
        foreach (var endpoint in endpoints)
        {
            var entityName = endpoint.EntityName.ToLowerInvariant();
            var handlerLines = new List<string>();
            string lastHandlerLine;

            if (endpoint.IsStream)
            {
                string lambdaParams;
                string requestCreation;

                if (endpoint.Parameters.Any())
                {
                    var paramsList = string.Join(", ", endpoint.Parameters.Select(p => $"{p.Type} {p.Name}"));
                    lambdaParams = $"{paramsList}, IMediator mediator, HttpResponse httpResponse, CancellationToken cancellationToken";
                    requestCreation = $"new {endpoint.RequestTypeName}({string.Join(", ", endpoint.Parameters.Select(p => p.Name))})";
                }
                else
                {
                    lambdaParams = "IMediator mediator, HttpResponse httpResponse, CancellationToken cancellationToken";
                    requestCreation = $"new {endpoint.RequestTypeName}()";
                }

                handlerLines.Add($"        {routeBuilderExpression}.MapGet(\"/{entityName}\", async ({lambdaParams}) =>");
                handlerLines.Add("        {");
                handlerLines.Add("            httpResponse.ContentType = \"application/x-ndjson\";");
                handlerLines.Add($"            await foreach (var item in mediator.CreateStream({requestCreation}, cancellationToken))");
                handlerLines.Add("            {");
                handlerLines.Add("                await httpResponse.WriteAsync(System.Text.Json.JsonSerializer.Serialize(item, System.Text.Json.JsonSerializerOptions.Web), cancellationToken);");
                handlerLines.Add("                await httpResponse.WriteAsync(\"\\n\", cancellationToken);");
                handlerLines.Add("                await httpResponse.Body.FlushAsync(cancellationToken);");
                handlerLines.Add("            }");
                lastHandlerLine = "        })";
            }
            else if (endpoint.HttpVerb == "GET")
            {
                if (endpoint.Parameters.Any())
                {
                    var paramsList = string.Join(", ", endpoint.Parameters.Select(p => $"{p.Type} {p.Name}"));
                    var requestCreation = endpoint.Parameters.Count == 1
                        ? $"new {endpoint.RequestTypeName}({endpoint.Parameters[0].Name})"
                        : $"new {endpoint.RequestTypeName}({string.Join(", ", endpoint.Parameters.Select(p => p.Name))})";

                    if (endpoint.IsResponseNullable)
                    {
                        handlerLines.Add($"        {routeBuilderExpression}.MapGet(\"/{entityName}\", async ({paramsList}, IMediator mediator) =>");
                        handlerLines.Add("        {");
                        handlerLines.Add($"            var result = await mediator.Send({requestCreation});");
                        handlerLines.Add("            return result is not null ? Microsoft.AspNetCore.Http.Results.Ok(result) : Microsoft.AspNetCore.Http.Results.NotFound();");
                        lastHandlerLine = "        })";
                    }
                    else
                    {
                        handlerLines.Add($"        {routeBuilderExpression}.MapGet(\"/{entityName}\", async ({paramsList}, IMediator mediator)");
                        lastHandlerLine = $"            => await mediator.Send({requestCreation}))";
                    }
                }
                else if (endpoint.IsResponseNullable)
                {
                    handlerLines.Add($"        {routeBuilderExpression}.MapGet(\"/{entityName}\", async (IMediator mediator) =>");
                    handlerLines.Add("        {");
                    handlerLines.Add($"            var result = await mediator.Send(new {endpoint.RequestTypeName}());");
                    handlerLines.Add("            return result is not null ? Microsoft.AspNetCore.Http.Results.Ok(result) : Microsoft.AspNetCore.Http.Results.NotFound();");
                    lastHandlerLine = "        })";
                }
                else
                {
                    handlerLines.Add($"        {routeBuilderExpression}.MapGet(\"/{entityName}\", async (IMediator mediator)");
                    lastHandlerLine = $"            => await mediator.Send(new {endpoint.RequestTypeName}()))";
                }
            }
            else if (endpoint.HttpVerb == "DELETE" && endpoint.Parameters.Any())
            {
                var paramsList = string.Join(", ", endpoint.Parameters.Select(p => $"{p.Type} {p.Name}"));
                var requestCreation = endpoint.Parameters.Count == 1
                    ? $"new {endpoint.RequestTypeName}({endpoint.Parameters[0].Name})"
                    : $"new {endpoint.RequestTypeName}({string.Join(", ", endpoint.Parameters.Select(p => p.Name))})";

                handlerLines.Add($"        {routeBuilderExpression}.MapDelete(\"/{entityName}\", async ({paramsList}, IMediator mediator)");
                lastHandlerLine = $"            => await mediator.Send({requestCreation}))";
            }
            else if (endpoint.HttpVerb == "PUT")
            {
                handlerLines.Add($"        {routeBuilderExpression}.MapPut(\"/{entityName}\", async (HttpRequest httpRequest, IMediator mediator, {endpoint.RequestTypeName} request)");
                lastHandlerLine = "            => await mediator.Send(request))";
            }
            else
            {
                handlerLines.Add($"        {routeBuilderExpression}.MapPost(\"/{entityName}\", async (HttpRequest httpRequest, IMediator mediator, {endpoint.RequestTypeName} request)");
                lastHandlerLine = "            => await mediator.Send(request))";
            }

            var endpointChain = new List<string>();

            if (!string.IsNullOrWhiteSpace(endpoint.Summary))
            {
                endpointChain.Add($"            .WithSummary(\"{endpoint.Summary}\")");
            }

            if (endpoint.Tags.Any())
            {
                var tagsString = string.Join("\", \"", endpoint.Tags);
                endpointChain.Add($"            .WithTags(\"{tagsString}\")");
            }

            if (!string.IsNullOrWhiteSpace(endpoint.Description))
            {
                endpointChain.Add($"            .WithDescription(\"{endpoint.Description}\")");
            }

            if (endpoint.HttpVerb == "GET" && endpoint.IsResponseNullable)
            {
                var responseTypeWithoutNullable = endpoint.ResponseTypeName.TrimEnd('?');
                endpointChain.Add($"            .Produces<{responseTypeWithoutNullable}>(StatusCodes.Status200OK)");
                endpointChain.Add("            .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)");
            }

            foreach (var line in handlerLines)
            {
                sb.AppendLine(line);
            }

            if (endpointChain.Count > 0)
            {
                sb.AppendLine(lastHandlerLine);
                for (int i = 0; i < endpointChain.Count - 1; i++)
                {
                    sb.AppendLine(endpointChain[i]);
                }

                sb.AppendLine(endpointChain[endpointChain.Count - 1] + ";");
            }
            else
            {
                sb.AppendLine(lastHandlerLine + ";");
            }

            sb.AppendLine();
        }
    }

    


}
