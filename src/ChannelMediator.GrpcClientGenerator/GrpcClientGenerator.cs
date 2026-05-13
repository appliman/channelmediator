using ChannelMediator.Generators.Shared;
using Microsoft.CodeAnalysis;
using System.Linq;
using System.Text;

namespace ChannelMediator.GrpcClientGenerator;

[Generator]
public class GrpcClientGenerator : IIncrementalGenerator
{
    private const string EndpointApiAttributeFullName = "ChannelMediator.ApiGenerators.Abstraction.EndpointApiAttribute";
    private const string GrpcClientAttributeFullName = "ChannelMediator.ApiGenerators.Abstraction.GrpcClientAttribute";

    // Protocol flag: Grpc = 2
    private const int GrpcProtocolFlag = 2;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(context.CompilationProvider,
            static (spc, compilation) => Execute(compilation, spc));
    }

    private static GrpcClientInfo? GetGrpcClientInfo(Compilation compilation)
    {
        var assemblyAttributes = compilation.Assembly.GetAttributes();
        var attributeData = assemblyAttributes
            .FirstOrDefault(ad => ad.AttributeClass?.ToDisplayString() == GrpcClientAttributeFullName);
        if (attributeData is null) return null;

        if (attributeData.ConstructorArguments.Length == 0) return null;
        var contractsType = attributeData.ConstructorArguments[0].Value as INamedTypeSymbol;
        if (contractsType is null) return null;

        var grpcClientName = GetNamedArgValue<string>(attributeData, "GrpcClientName") ?? "GrpcClient";

        return new GrpcClientInfo
        {
            ContractsAssembly = contractsType.ContainingAssembly,
            OutputNamespace = compilation.Assembly.Name,
            GrpcClientName = grpcClientName
        };
    }

    private static List<GrpcEndpointInfo> GetGrpcEndpointsFromAssembly(IAssemblySymbol assembly)
    {
        var endpoints = new List<GrpcEndpointInfo>();
        CollectEndpointsFromNamespace(assembly.GlobalNamespace, endpoints);
        return endpoints;
    }

    private static void CollectEndpointsFromNamespace(INamespaceSymbol ns, List<GrpcEndpointInfo> endpoints)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            var ep = TryGetGrpcEndpointInfo(type);
            if (ep != null) endpoints.Add(ep);
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            CollectEndpointsFromNamespace(child, endpoints);
        }
    }

    private static GrpcEndpointInfo? TryGetGrpcEndpointInfo(INamedTypeSymbol typeSymbol)
    {
        var attributeData = typeSymbol.GetAttributes()
            .FirstOrDefault(ad => ad.AttributeClass?.ToDisplayString() == EndpointApiAttributeFullName);
        if (attributeData is null) return null;

        var protocol = GetNamedArgValue<int>(attributeData, "Protocol");
        if ((protocol & GrpcProtocolFlag) == 0) return null;

        var groupName = GetNamedArgValue<string>(attributeData, "GroupName") ?? "Default";

        var (responseTypeName, isStream) = ExtractResponseType(typeSymbol);
        var methodName = CreateMethodName(typeSymbol.Name);

        return new GrpcEndpointInfo
        {
            RequestFullName = typeSymbol.ToDisplayString(),
            RequestShortName = typeSymbol.Name,
            Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
            GroupName = groupName,
            ResponseTypeName = responseTypeName,
            IsStream = isStream,
            MethodName = methodName
        };
    }

    private static (string typeName, bool isStream) ExtractResponseType(INamedTypeSymbol typeSymbol)
    {
        var iStreamInterface = typeSymbol.AllInterfaces
            .FirstOrDefault(i => i.Name == "IStreamRequest" && i.TypeArguments.Length == 1);

        if (iStreamInterface != null)
            return (iStreamInterface.TypeArguments[0].ToDisplayString(), true);

        var iface = typeSymbol.AllInterfaces
            .FirstOrDefault(i => i.Name == "IRequest" && i.TypeArguments.Length == 1);

        if (iface != null)
            return (iface.TypeArguments[0].ToDisplayString(), false);

        return ("object", false);
    }

    private static string CreateMethodName(string requestTypeName)
    {
        return requestTypeName.EndsWith("Request", StringComparison.Ordinal)
            ? requestTypeName.Substring(0, requestTypeName.Length - "Request".Length)
            : requestTypeName;
    }

    private static void Execute(Compilation compilation, SourceProductionContext context)
    {
        var client = GetGrpcClientInfo(compilation);
        if (client is null) return;

        var endpoints = GetGrpcEndpointsFromAssembly(client.ContractsAssembly);
        if (!endpoints.Any()) return;

        // Emit one service interface per group (needed to compile the generated handlers)
        foreach (var group in endpoints.GroupBy(e => e.GroupName).OrderBy(g => g.Key))
        {
            context.AddSource($"I{group.Key}Service.g.cs", GenerateServiceInterface(client, group.Key, group.ToList()));
        }

        foreach (var ep in endpoints)
        {
            var handlerName = ep.RequestShortName + "Handler";
            context.AddSource($"{handlerName}.g.cs", GenerateHandler(client, ep));
        }
    }

    private static string GenerateServiceInterface(GrpcClientInfo client, string groupName, List<GrpcEndpointInfo> endpoints)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine($"namespace {client.OutputNamespace};");
        sb.AppendLine();
        sb.AppendLine("[global::System.ServiceModel.ServiceContract]");
        sb.AppendLine($"public interface I{groupName}Service");
        sb.AppendLine("{");
        foreach (var ep in endpoints)
        {
            sb.AppendLine("    [global::System.ServiceModel.OperationContract]");
            if (ep.IsStream)
            {
                sb.AppendLine($"    global::System.Collections.Generic.IAsyncEnumerable<{ep.ResponseTypeName}> {ep.MethodName}({ep.RequestFullName} request, ProtoBuf.Grpc.CallContext context = default);");
            }
            else
            {
                sb.AppendLine($"    global::System.Threading.Tasks.Task<{ep.ResponseTypeName}> {ep.MethodName}({ep.RequestFullName} request, ProtoBuf.Grpc.CallContext context = default);");
            }
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateHandler(GrpcClientInfo client, GrpcEndpointInfo ep)
    {
        var sb = new StringBuilder();
        var serviceInterfaceName = $"I{ep.GroupName}Service";

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");

        if (ep.IsStream)
        {
            sb.AppendLine($"namespace {client.OutputNamespace};");
            sb.AppendLine();
            sb.AppendLine($"internal class {ep.RequestShortName}Handler");
            sb.AppendLine($"    : global::ChannelMediator.IStreamRequestHandler<{ep.RequestFullName}, {ep.ResponseTypeName}>");
            sb.AppendLine("{");
            sb.AppendLine("    private readonly global::Grpc.Net.ClientFactory.GrpcClientFactory _grpcClientFactory;");
            sb.AppendLine();
            sb.AppendLine($"    public {ep.RequestShortName}Handler(global::Grpc.Net.ClientFactory.GrpcClientFactory grpcClientFactory)");
            sb.AppendLine("    {");
            sb.AppendLine("        _grpcClientFactory = grpcClientFactory;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine($"    public async global::System.Collections.Generic.IAsyncEnumerable<{ep.ResponseTypeName}> Handle(");
            sb.AppendLine($"        {ep.RequestFullName} request,");
            sb.AppendLine("        [global::System.Runtime.CompilerServices.EnumeratorCancellation] global::System.Threading.CancellationToken cancellationToken)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var client = _grpcClientFactory.CreateClient<{serviceInterfaceName}>(\"{client.GrpcClientName}\");");
            sb.AppendLine($"        await foreach (var item in client.{ep.MethodName}(request, cancellationToken).WithCancellation(cancellationToken))");
            sb.AppendLine("            yield return item;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
        }
        else
        {
            sb.AppendLine($"namespace {client.OutputNamespace};");
            sb.AppendLine();
            sb.AppendLine($"internal class {ep.RequestShortName}Handler");
            sb.AppendLine($"    : global::ChannelMediator.IRequestHandler<{ep.RequestFullName}, {ep.ResponseTypeName}>");
            sb.AppendLine("{");
            sb.AppendLine("    private readonly global::Grpc.Net.ClientFactory.GrpcClientFactory _grpcClientFactory;");
            sb.AppendLine();
            sb.AppendLine($"    public {ep.RequestShortName}Handler(global::Grpc.Net.ClientFactory.GrpcClientFactory grpcClientFactory)");
            sb.AppendLine("    {");
            sb.AppendLine("        _grpcClientFactory = grpcClientFactory;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine($"    public async global::System.Threading.Tasks.Task<{ep.ResponseTypeName}> Handle(");
            sb.AppendLine($"        {ep.RequestFullName} request,");
            sb.AppendLine("        global::System.Threading.CancellationToken cancellationToken)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var client = _grpcClientFactory.CreateClient<{serviceInterfaceName}>(\"{client.GrpcClientName}\");");
            sb.AppendLine($"        return await client.{ep.MethodName}(request, cancellationToken);");
            sb.AppendLine("    }");
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private static T? GetNamedArgValue<T>(AttributeData attributeData, string argName)
    {
        var arg = attributeData.NamedArguments.FirstOrDefault(na => na.Key == argName);
        if (arg.Value.Value is T value) return value;
        return default;
    }
}
