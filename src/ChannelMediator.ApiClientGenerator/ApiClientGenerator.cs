using Microsoft.CodeAnalysis;
using System.Linq;
using System.Text;

namespace ChannelMediator.ApiClientGenerator;

[Generator]
public class ApiClientGenerator : IIncrementalGenerator
{
	private const string EndpointApiAttributeFullName = "ChannelMediator.MinimalApiGenerator.Abstraction.EndpointApiAttribute";
	private const string ApiClientAttributeFullName = "ChannelMediator.MinimalApiGenerator.Abstraction.ApiClientAttribute";

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Use CompilationProvider to read the [assembly: ApiClient] attribute
		// and scan the referenced assembly for [EndpointApi] types.
		context.RegisterSourceOutput(context.CompilationProvider,
			static (spc, compilation) => Execute(compilation, spc));
	}

	private static ApiClientInfo? GetApiClientInfo(Compilation compilation)
	{
		var assemblyAttributes = compilation.Assembly.GetAttributes();
		var attributeData = assemblyAttributes
			.FirstOrDefault(ad => ad.AttributeClass?.ToDisplayString() == ApiClientAttributeFullName);
		if (attributeData is null) return null;

		// First constructor arg is the typeof(ContractsAssemblyType)
		if (attributeData.ConstructorArguments.Length == 0) return null;
		var contractsType = attributeData.ConstructorArguments[0].Value as INamedTypeSymbol;
		if (contractsType is null) return null;

		var httpClientName = GetNamedArgValue<string>(attributeData, "HttpClientName") ?? "ApiClient";

		return new ApiClientInfo
		{
			ContractsAssembly = contractsType.ContainingAssembly,
			OutputNamespace = compilation.Assembly.Name,
			HttpClientName = httpClientName
		};
	}

	private static List<EndpointInfo> GetEndpointsFromAssembly(IAssemblySymbol assembly)
	{
		var endpoints = new List<EndpointInfo>();
		CollectEndpointsFromNamespace(assembly.GlobalNamespace, endpoints);
		return endpoints;
	}

	private static void CollectEndpointsFromNamespace(INamespaceSymbol ns, List<EndpointInfo> endpoints)
	{
		foreach (var type in ns.GetTypeMembers())
		{
			var ep = TryGetEndpointInfo(type);
			if (ep != null) endpoints.Add(ep);
		}

		foreach (var child in ns.GetNamespaceMembers())
		{
			CollectEndpointsFromNamespace(child, endpoints);
		}
	}

	private static EndpointInfo? TryGetEndpointInfo(INamedTypeSymbol typeSymbol)
	{
		var attributeData = typeSymbol.GetAttributes()
			.FirstOrDefault(ad => ad.AttributeClass?.ToDisplayString() == EndpointApiAttributeFullName);
		if (attributeData is null) return null;

		var groupName = GetNamedArgValue<string>(attributeData, "GroupName") ?? "Default";
		var entityName = GetNamedArgValue<string>(attributeData, "EntityName") ?? typeSymbol.Name.Replace("Request", "");
		var useHttpStandardVerbs = GetNamedArgValue<bool>(attributeData, "UseHttpStandardVerbs");

		var httpVerb = "POST";
		if (useHttpStandardVerbs)
		{
			if (typeSymbol.Name.StartsWith("Get")) httpVerb = "GET";
			else if (typeSymbol.Name.StartsWith("Delete")) httpVerb = "DELETE";
			else if (typeSymbol.Name.StartsWith("Put") || typeSymbol.Name.StartsWith("Update")) httpVerb = "PUT";
			else if (typeSymbol.Name.StartsWith("Post") || typeSymbol.Name.StartsWith("Create") || typeSymbol.Name.StartsWith("Save")) httpVerb = "POST";
		}

		var parameters = new List<RequestParameter>();
		var primaryCtor = typeSymbol.Constructors.FirstOrDefault(c =>
			c.Parameters.Length > 0
			&& !(c.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(c.Parameters[0].Type.OriginalDefinition, typeSymbol.OriginalDefinition)));
		if (primaryCtor != null)
		{
			foreach (var p in primaryCtor.Parameters)
			{
				parameters.Add(new RequestParameter
				{
					Name = p.Name,
					Type = p.Type.ToDisplayString()
				});
			}
		}

		var (isNullable, responseType) = ExtractResponseType(typeSymbol);

		return new EndpointInfo
		{
			RequestFullName = typeSymbol.ToDisplayString(),
			RequestShortName = typeSymbol.Name,
			Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
			GroupName = groupName,
			EntityName = entityName,
			HttpVerb = httpVerb,
			Parameters = parameters,
			IsResponseNullable = isNullable,
			ResponseTypeName = responseType
		};
	}

	private static (bool isNullable, string typeName) ExtractResponseType(INamedTypeSymbol typeSymbol)
	{
		var iface = typeSymbol.AllInterfaces
			.FirstOrDefault(i => i.Name == "IRequest" && i.TypeArguments.Length == 1);

		if (iface != null)
		{
			var rt = iface.TypeArguments[0];
			return (rt.NullableAnnotation == NullableAnnotation.Annotated, rt.ToDisplayString());
		}
		return (false, "object");
	}

	private static void Execute(Compilation compilation, SourceProductionContext context)
	{
		var client = GetApiClientInfo(compilation);
		if (client is null) return;

		var endpointList = GetEndpointsFromAssembly(client.ContractsAssembly);
		if (!endpointList.Any()) return;

		// Emit ClientApiException
		context.AddSource("ClientApiException.g.cs", GenerateClientApiException(client.OutputNamespace));

		// Emit one handler per endpoint
		foreach (var ep in endpointList)
		{
			var handlerName = ep.RequestShortName + "Handler";
			context.AddSource($"{handlerName}.g.cs", GenerateHandler(client, ep, handlerName));
		}
	}

	private static string GenerateClientApiException(string ns)
	{
		return $@"using System;
using System.Net.Http;

namespace {ns};

/// <summary>
/// Exception thrown when an API client call returns a non-success status code.
/// </summary>
public class ClientApiException : Exception
{{
	/// <summary>Gets the HTTP response that caused the exception.</summary>
	public HttpResponseMessage Response {{ get; }}

	public ClientApiException(string message, HttpResponseMessage response)
		: base(message)
	{{
		Response = response;
	}}

	public ClientApiException(string message, HttpResponseMessage response, Exception innerException)
		: base(message, innerException)
	{{
		Response = response;
	}}
}}
";
	}

	private static string GenerateHandler(ApiClientInfo client, EndpointInfo ep, string handlerName)
	{
		var sb = new StringBuilder();
		var responseType = ep.ResponseTypeName;
		var cleanResponse = responseType.TrimEnd('?');
		var route = $"{ep.GroupName.ToLowerInvariant()}/{ep.EntityName.ToLowerInvariant()}";

		sb.AppendLine("using System.Net.Http;");
		sb.AppendLine("using System.Net.Http.Json;");
		sb.AppendLine("using System.Threading;");
		sb.AppendLine("using System.Threading.Tasks;");
		sb.AppendLine("using ChannelMediator;");
		sb.AppendLine($"using {client.OutputNamespace};");
		sb.AppendLine();
		sb.AppendLine($"namespace {client.OutputNamespace}.Handlers;");
		sb.AppendLine();
		sb.AppendLine($"/// <summary>Generated API client handler for <see cref=\"{ep.RequestShortName}\"/>.</summary>");
		sb.AppendLine($"internal class {handlerName} : IRequestHandler<{ep.RequestFullName}, {cleanResponse}>");
		sb.AppendLine("{");
		sb.AppendLine("    private readonly IHttpClientFactory _httpClientFactory;");
		sb.AppendLine();
		sb.AppendLine($"    public {handlerName}(IHttpClientFactory httpClientFactory)");
		sb.AppendLine("    {");
		sb.AppendLine("        _httpClientFactory = httpClientFactory;");
		sb.AppendLine("    }");
		sb.AppendLine();
		sb.AppendLine($"    public async Task<{cleanResponse}> Handle({ep.RequestFullName} request, CancellationToken cancellationToken)");
		sb.AppendLine("    {");
		sb.AppendLine($"        var httpClient = _httpClientFactory.CreateClient(\"{client.HttpClientName}\");");

		switch (ep.HttpVerb)
		{
			case "GET":
				EmitGetBody(sb, ep, route, cleanResponse);
				break;
			case "DELETE":
				EmitDeleteBody(sb, ep, route, cleanResponse);
				break;
			case "PUT":
				EmitPutBody(sb, ep, route, cleanResponse);
				break;
			default: // POST
				EmitPostBody(sb, ep, route, cleanResponse);
				break;
		}

		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}

	private static string BuildQueryString(EndpointInfo ep)
	{
		if (!ep.Parameters.Any()) return "";
		var parts = string.Join("&", ep.Parameters.Select(p => $"{p.Name}={{{FormatRequestAccess(p.Name)}}}"));
		return "?" + parts;
	}

	private static string FormatRequestAccess(string paramName)
	{
		// Capitalize first letter to match record property convention
		return "request." + char.ToUpperInvariant(paramName[0]) + paramName.Substring(1);
	}

	private static void EmitGetBody(StringBuilder sb, EndpointInfo ep, string route, string cleanResponse)
	{
		var qs = BuildQueryString(ep);
		sb.AppendLine($"        var url = $\"{{httpClient.BaseAddress}}{route}{qs}\";");
		sb.AppendLine($"        var result = await httpClient.GetFromJsonAsync<{cleanResponse}>(url, System.Text.Json.JsonSerializerOptions.Web, cancellationToken);");
		sb.AppendLine($"        return result!;");
	}

	private static void EmitDeleteBody(StringBuilder sb, EndpointInfo ep, string route, string cleanResponse)
	{
		var qs = BuildQueryString(ep);
		sb.AppendLine($"        var url = $\"{{httpClient.BaseAddress}}{route}{qs}\";");
		sb.AppendLine($"        var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, url);");
		sb.AppendLine($"        var response = await httpClient.SendAsync(httpRequestMessage, cancellationToken);");
		sb.AppendLine($"        if (!response.IsSuccessStatusCode)");
		sb.AppendLine("        {");
		sb.AppendLine($"            throw new ClientApiException($\"Error calling api {route}{qs}\", response);");
		sb.AppendLine("        }");
		sb.AppendLine($"        var result = await response.Content.ReadFromJsonAsync<{cleanResponse}>(System.Text.Json.JsonSerializerOptions.Web, cancellationToken);");
		sb.AppendLine($"        return result!;");
	}

	private static void EmitPutBody(StringBuilder sb, EndpointInfo ep, string route, string cleanResponse)
	{
		sb.AppendLine($"        var url = $\"{{httpClient.BaseAddress}}{route}\";");
		sb.AppendLine($"        var response = await httpClient.PutAsJsonAsync(url, request, System.Text.Json.JsonSerializerOptions.Web, cancellationToken);");
		sb.AppendLine($"        if (!response.IsSuccessStatusCode)");
		sb.AppendLine("        {");
		sb.AppendLine($"            throw new ClientApiException($\"Error calling api {route}\", response);");
		sb.AppendLine("        }");
		sb.AppendLine($"        var result = await response.Content.ReadFromJsonAsync<{cleanResponse}>(System.Text.Json.JsonSerializerOptions.Web, cancellationToken);");
		sb.AppendLine($"        return result!;");
	}

	private static void EmitPostBody(StringBuilder sb, EndpointInfo ep, string route, string cleanResponse)
	{
		sb.AppendLine($"        var url = $\"{{httpClient.BaseAddress}}{route}\";");
		sb.AppendLine($"        var response = await httpClient.PostAsJsonAsync(url, request, System.Text.Json.JsonSerializerOptions.Web, cancellationToken);");
		sb.AppendLine($"        if (!response.IsSuccessStatusCode)");
		sb.AppendLine("        {");
		sb.AppendLine($"            throw new ClientApiException($\"Error calling api {route}\", response);");
		sb.AppendLine("        }");
		sb.AppendLine($"        var result = await response.Content.ReadFromJsonAsync<{cleanResponse}>(System.Text.Json.JsonSerializerOptions.Web, cancellationToken);");
		sb.AppendLine($"        return result!;");
	}

	private static T? GetNamedArgValue<T>(AttributeData? attributeData, string propertyName)
	{
		if (attributeData is null) return default;
		var arg = attributeData.NamedArguments.FirstOrDefault(na => na.Key == propertyName);
		return arg.Value.Value is T value ? value : default;
	}

	private class ApiClientInfo
	{
		public IAssemblySymbol ContractsAssembly { get; set; } = null!;
		public string OutputNamespace { get; set; } = null!;
		public string HttpClientName { get; set; } = "ApiClient";
	}

	private class EndpointInfo
	{
		public string RequestFullName { get; set; } = null!;
		public string RequestShortName { get; set; } = null!;
		public string Namespace { get; set; } = null!;
		public string GroupName { get; set; } = null!;
		public string EntityName { get; set; } = null!;
		public string HttpVerb { get; set; } = "POST";
		public List<RequestParameter> Parameters { get; set; } = new();
		public bool IsResponseNullable { get; set; }
		public string ResponseTypeName { get; set; } = null!;

		public override bool Equals(object? obj)
			=> obj is EndpointInfo o && RequestFullName == o.RequestFullName;
		public override int GetHashCode() => RequestFullName.GetHashCode();
	}

	private class RequestParameter
	{
		public string Name { get; set; } = null!;
		public string Type { get; set; } = null!;
	}
}
