using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace ChannelMediator.MinimalApiGenerator;

[Generator]
public class MinimalApiGenerator : IIncrementalGenerator
{
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

                    return new MapApiExtensionInfo
                    {
                        ClassName = classSymbol.Name,
                        Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
                        WithVersionning = withVersionning,
                        ScanAssemblies = scanAssemblies
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
                    var entityName = GetAttributeValue<string>(attributeData, "EntityName") ?? typeSymbol.Name.Replace("Request", "");
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

                    if (httpVerb == "GET" || httpVerb == "DELETE")
                    {
                        parameters = ExtractRecordParameters(typeSymbol);
                    }

                    var (isResponseNullable, responseTypeName) = ExtractResponseTypeInfo(typeSymbol);

                    return new EndpointApiInfo
                    {
                        RequestTypeName = typeSymbol.ToDisplayString(),
                        RequestShortName = typeSymbol.Name,
                        Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
                        GroupName = groupName,
                        EntityName = entityName,
                        Tags = tags,
                        Summary = summary,
                        Description = description,
                        AuthenticationSchemes = authenticationSchemes,
                        HttpVerb = httpVerb,
                        UseHttpStandardVerbs = useHttpStandardVerbs,
                        Parameters = parameters,
                        IsResponseNullable = isResponseNullable,
                        ResponseTypeName = responseTypeName
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
            .FirstOrDefault(c => c.Parameters.Length > 0);

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

    private static (bool isNullable, string typeName) ExtractResponseTypeInfo(INamedTypeSymbol typeSymbol)
    {
        var iRequestInterface = typeSymbol.AllInterfaces
            .FirstOrDefault(i => i.Name == "IRequest" && i.TypeArguments.Length == 1);

        if (iRequestInterface != null && iRequestInterface.TypeArguments.Length > 0)
        {
            var responseType = iRequestInterface.TypeArguments[0];
            var isNullable = responseType.NullableAnnotation == NullableAnnotation.Annotated;
            var typeName = responseType.ToDisplayString();
            
            return (isNullable, typeName);
        }

        return (false, "object");
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
            var referencedEndpoints = GetEndpointApisFromReferencedAssemblies(compilation, mapApiClass.ScanAssemblies);
            var distinctEndpointApis = localEndpoints.Concat(referencedEndpoints).Distinct().ToList();

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
        var iRequestName = "IRequest";
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
                var entityName = GetAttributeValue<string>(attributeData, "EntityName") ?? typeSymbol.Name.Replace("Request", "");
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

                var (isResponseNullable, responseTypeName) = ExtractResponseTypeInfo(typeSymbol);

                results.Add(new EndpointApiInfo
                {
                    RequestTypeName = typeSymbol.ToDisplayString(),
                    RequestShortName = typeSymbol.Name,
                    Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
                    GroupName = groupName,
                    EntityName = entityName,
                    Tags = tags,
                    Summary = summary,
                    Description = description,
                    AuthenticationSchemes = authenticationSchemes,
                    HttpVerb = httpVerb,
                    UseHttpStandardVerbs = useHttpStandardVerbs,
                    Parameters = parameters,
                    IsResponseNullable = isResponseNullable,
                    ResponseTypeName = responseTypeName
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

        if (mapApiClass.WithVersionning)
        {
            sb.AppendLine($"    public static void Map{mapApiClass.ClassName}(this IEndpointRouteBuilder routes, ApiVersionSet versionSet)");
        }
        else
        {
            sb.AppendLine($"    public static void Map{mapApiClass.ClassName}(this IEndpointRouteBuilder routes)");
        }

        sb.AppendLine("    {");

        var groupedEndpoints = endpoints.GroupBy(e => e.GroupName).ToList();

        foreach (var group in groupedEndpoints)
        {
            var groupName = group.Key;
            var groupEndpoints = group.ToList();

            sb.AppendLine();
            sb.AppendLine($"        // Group: {groupName}");
            var firstEndpoint = groupEndpoints.First();
            var groupChain = new List<string>();
            groupChain.Add($"        var {groupName.ToLowerInvariant()}Group = routes.MapGroup(\"/api/{groupName.ToLowerInvariant()}/\")");

            if (firstEndpoint.Tags.Any())
            {
                var tagsString = string.Join("\", \"", firstEndpoint.Tags);
                groupChain.Add($"            .WithTags(\"{tagsString}\")");
            }

            if (firstEndpoint.AuthenticationSchemes.Any())
            {
                var schemesString = string.Join(", ", firstEndpoint.AuthenticationSchemes.Select(s => $"\"{s}\""));
                groupChain.Add($"            .RequireAuthorization(new AuthorizeAttribute {{ AuthenticationSchemes = {schemesString} }})");
            }

            for (int i = 0; i < groupChain.Count - 1; i++)
                sb.AppendLine(groupChain[i]);
            sb.AppendLine(groupChain[groupChain.Count - 1] + ";");
            sb.AppendLine();

            foreach (var endpoint in groupEndpoints)
            {
                var entityName = endpoint.EntityName.ToLowerInvariant();
                var groupVarName = $"{groupName.ToLowerInvariant()}Group";

                // Build the handler lines (all but last) and the last handler line separately
                var handlerLines = new List<string>();
                string lastHandlerLine;

                if (endpoint.HttpVerb == "GET")
                {
                    if (endpoint.Parameters.Any())
                    {
                        var paramsList = string.Join(", ", endpoint.Parameters.Select(p => $"{p.Type} {p.Name}"));
                        var requestCreation = endpoint.Parameters.Count == 1
                            ? $"new {endpoint.RequestTypeName}({endpoint.Parameters[0].Name})"
                            : $"new {endpoint.RequestTypeName}({string.Join(", ", endpoint.Parameters.Select(p => p.Name))})";

                        if (endpoint.IsResponseNullable)
                        {
                            handlerLines.Add($"        {groupVarName}.MapGet(\"/{entityName}\", async ({paramsList}, IMediator mediator) =>");
                            handlerLines.Add($"        {{");
                            handlerLines.Add($"            var result = await mediator.Send({requestCreation});");
                            handlerLines.Add($"            return result is not null ? Results.Ok(result) : Results.NotFound();");
                            lastHandlerLine = $"        }})";
                        }
                        else
                        {
                            handlerLines.Add($"        {groupVarName}.MapGet(\"/{entityName}\", async ({paramsList}, IMediator mediator)");
                            lastHandlerLine = $"            => await mediator.Send({requestCreation}))";
                        }
                    }
                    else
                    {
                        if (endpoint.IsResponseNullable)
                        {
                            handlerLines.Add($"        {groupVarName}.MapGet(\"/{entityName}\", async (IMediator mediator) =>");
                            handlerLines.Add($"        {{");
                            handlerLines.Add($"            var result = await mediator.Send(new {endpoint.RequestTypeName}());");
                            handlerLines.Add($"            return result is not null ? Results.Ok(result) : Results.NotFound();");
                            lastHandlerLine = $"        }})";
                        }
                        else
                        {
                            handlerLines.Add($"        {groupVarName}.MapGet(\"/{entityName}\", async (IMediator mediator)");
                            lastHandlerLine = $"            => await mediator.Send(new {endpoint.RequestTypeName}()))";
                        }
                    }
                }
                else if (endpoint.HttpVerb == "DELETE" && endpoint.Parameters.Any())
                {
                    var paramsList = string.Join(", ", endpoint.Parameters.Select(p => $"{p.Type} {p.Name}"));
                    var requestCreation = endpoint.Parameters.Count == 1
                        ? $"new {endpoint.RequestTypeName}({endpoint.Parameters[0].Name})"
                        : $"new {endpoint.RequestTypeName}({string.Join(", ", endpoint.Parameters.Select(p => p.Name))})";

                    handlerLines.Add($"        {groupVarName}.MapDelete(\"/{entityName}\", async ({paramsList}, IMediator mediator)");
                    lastHandlerLine = $"            => await mediator.Send({requestCreation}))";
                }
                else if (endpoint.HttpVerb == "PUT")
                {
                    handlerLines.Add($"        {groupVarName}.MapPut(\"/{entityName}\", async (HttpRequest httpRequest, IMediator mediator, {endpoint.RequestTypeName} request)");
                    lastHandlerLine = $"            => await mediator.Send(request))";
                }
                else
                {
                    handlerLines.Add($"        {groupVarName}.MapPost(\"/{entityName}\", async (HttpRequest httpRequest, IMediator mediator, {endpoint.RequestTypeName} request)");
                    lastHandlerLine = $"            => await mediator.Send(request))";
                }

                // Build metadata chain
                var endpointChain = new List<string>();

                if (!string.IsNullOrWhiteSpace(endpoint.Summary))
                    endpointChain.Add($"            .WithSummary(\"{endpoint.Summary}\")");

                if (endpoint.Tags.Any())
                {
                    var tagsString = string.Join("\", \"", endpoint.Tags);
                    endpointChain.Add($"            .WithTags(\"{tagsString}\")");
                }

                if (!string.IsNullOrWhiteSpace(endpoint.Description))
                    endpointChain.Add($"            .WithDescription(\"{endpoint.Description}\")");

                if (endpoint.HttpVerb == "GET" && endpoint.IsResponseNullable)
                {
                    var responseTypeWithoutNullable = endpoint.ResponseTypeName.TrimEnd('?');
                    endpointChain.Add($"            .Produces<{responseTypeWithoutNullable}>(StatusCodes.Status200OK)");
                    endpointChain.Add($"            .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)");
                }

                // Emit handler lines (all but last)
                foreach (var line in handlerLines)
                    sb.AppendLine(line);

                if (endpointChain.Count > 0)
                {
                    // Last handler line + full chain, semicolon on last chain item
                    sb.AppendLine(lastHandlerLine);
                    for (int i = 0; i < endpointChain.Count - 1; i++)
                        sb.AppendLine(endpointChain[i]);
                    sb.AppendLine(endpointChain[endpointChain.Count - 1] + ";");
                }
                else
                {
                    // No chain — semicolon directly on the last handler line
                    sb.AppendLine(lastHandlerLine + ";");
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    


}
