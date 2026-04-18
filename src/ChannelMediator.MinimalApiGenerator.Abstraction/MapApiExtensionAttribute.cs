namespace ChannelMediator.MinimalApiGenerator.Abstraction;

/// <summary>
/// Marks a <see langword="static partial"/> class as the target for the ChannelMediator
/// Minimal API source generator.
/// </summary>
/// <remarks>
/// The generator scans the compilation for all request types decorated with
/// <see cref="EndpointApiAttribute"/> and emits a <c>Map{ClassName}(this IEndpointRouteBuilder)</c>
/// extension method inside the decorated partial class.
/// <para>
/// The decorated class must be <see langword="static"/> and <see langword="partial"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [MapApiExtension]
/// public static partial class MyApiMapper { }
///
/// // In Program.cs:
/// app.MapMyApiMapper();
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class MapApiExtensionAttribute : Attribute
{
	/// <summary>
	/// Gets or sets a value indicating whether API versioning support is enabled.
	/// </summary>
	/// <remarks>
	/// When <see langword="true"/>, the generated extension method accepts an additional
	/// <c>ApiVersionSet</c> parameter and adds the required
	/// <c>using Asp.Versioning.Builder;</c> directive.
	/// Requires the <c>Asp.Versioning.Http</c> package.
	/// </remarks>
	public bool WithVersionning { get; set; } = false;

	/// <summary>
	/// Gets or sets the list of assembly names to scan for <see cref="EndpointApiAttribute"/> types.
	/// </summary>
	/// <remarks>
	/// When empty or <see langword="null"/>, the generator scans all referenced assemblies.
	/// When specified, only the listed assemblies are scanned.
	/// Use the simple assembly name (e.g. <c>"ChannelMediatorApiContractsSample"</c>).
	/// </remarks>
	public string[]? ScanAssemblies { get; set; }
}
