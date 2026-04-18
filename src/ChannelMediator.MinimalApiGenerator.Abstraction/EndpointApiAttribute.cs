namespace ChannelMediator.MinimalApiGenerator.Abstraction;

/// <summary>
/// Marks an <c>IRequest</c> or <c>IRequest&lt;TResponse&gt;</c> class as a Minimal API endpoint
/// to be auto-registered by the ChannelMediator source generator.
/// </summary>
/// <remarks>
/// Place this attribute on a request class (or record) to instruct the generator to emit a
/// <c>Map{Verb}</c> call for that request inside the appropriate route group.
/// The generated extension method is defined in the partial class decorated with
/// <see cref="MapApiExtensionAttribute"/>.
/// </remarks>
/// <example>
/// <code>
/// [EndpointApi(
///     GroupName = "Catalog",
///     EntityName = "products",
///     Summary = "Get a product by ID",
///     UseHttpStandardVerbs = true)]
/// public record GetProductRequest(int Id) : IRequest&lt;Product?&gt;;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class EndpointApiAttribute : Attribute
{
	/// <summary>
	/// Gets or sets the name of the route group this endpoint belongs to.
	/// The group is mapped to <c>/api/{groupName}</c> (lower-cased).
	/// </summary>
	/// <example><c>GroupName = "Catalog"</c> → route prefix <c>/api/catalog</c></example>
	public string GroupName { get; set; } = null!;

	/// <summary>
	/// Gets or sets the entity segment appended to the group prefix.
	/// Defaults to the request type name with the <c>Request</c> suffix removed (lower-cased).
	/// </summary>
	/// <example><c>EntityName = "products"</c> → full route <c>/api/catalog/products</c></example>
	public string EntityName { get; set; } = null!;

	/// <summary>
	/// Gets or sets the OpenAPI tags applied to this endpoint via <c>.WithTags(...)</c>.
	/// Tags are used for grouping endpoints in the Swagger UI.
	/// </summary>
	public string[] Tags { get; set; } = Array.Empty<string>();

	/// <summary>
	/// Gets or sets the short summary for this endpoint, emitted via <c>.WithSummary(...)</c>.
	/// Displayed as the endpoint title in Swagger UI.
	/// </summary>
	public string? Summary { get; set; }

	/// <summary>
	/// Gets or sets the detailed description for this endpoint, emitted via <c>.WithDescription(...)</c>.
	/// Supports Markdown in Swagger UI.
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// Gets or sets the authentication schemes required by this endpoint.
	/// When non-empty, the generator emits
	/// <c>.RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = ... })</c>.
	/// </summary>
	/// <example><c>AuthenticationSchemes = new[] { "Bearer" }</c></example>
	public string[] AuthenticationSchemes { get; set; } = Array.Empty<string>();

	/// <summary>
	/// Gets or sets a value indicating whether the HTTP verb is inferred from the request type name
	/// using standard naming conventions.
	/// </summary>
	/// <remarks>
	/// When <see langword="true"/>, the generator maps the verb as follows:
	/// <list type="table">
	///   <listheader><term>Prefix</term><description>HTTP verb</description></listheader>
	///   <item><term><c>Get*</c></term><description>GET</description></item>
	///   <item><term><c>Delete*</c></term><description>DELETE</description></item>
	///   <item><term><c>Put*</c> / <c>Update*</c></term><description>PUT</description></item>
	///   <item><term><c>Post*</c> / <c>Create*</c> / <c>Save*</c></term><description>POST</description></item>
	/// </list>
	/// When <see langword="false"/> (default), all endpoints are mapped as POST.
	/// </remarks>
	public bool UseHttpStandardVerbs { get; set; } = false;
}
