namespace ChannelMediator.ApiGenerators.Abstraction;

/// <summary>
/// Marks an assembly as an API client target for the source generator.
/// The generator will scan <see cref="ContractsAssemblyType"/> to discover request types decorated with <see cref="EndpointApiAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
public class ApiClientAttribute : Attribute
{
	/// <summary>
	/// A type from the assembly that contains the request contracts.
	/// The generator uses this type's assembly to discover all <see cref="EndpointApiAttribute"/> request types.
	/// </summary>
	public Type ContractsAssemblyType { get; }

	/// <summary>
	/// The named <see cref="System.Net.Http.HttpClient"/> to inject into generated handlers.
	/// </summary>
	public string HttpClientName { get; set; } = "ApiClient";

	/// <summary>
	/// Gets or sets the JSON serializer options preset used when serializing requests
	/// and deserializing responses in generated handlers.
	/// Defaults to <see cref="JsonSerializerOptionsPreset.Web"/>.
	/// </summary>
	/// <remarks>
	/// Set the same value on <see cref="MapApiExtensionAttribute.JsonOptions"/> so that
	/// the client and the server use identical serialization behaviour (e.g. enum representation).
	/// </remarks>
	public JsonSerializerOptionsPreset JsonOptions { get; set; } = JsonSerializerOptionsPreset.Web;

	/// <summary>
	/// Initializes a new instance of <see cref="ApiClientAttribute"/>.
	/// </summary>
	/// <param name="contractsAssemblyType">A type from the assembly containing the request contracts.</param>
	public ApiClientAttribute(Type contractsAssemblyType)
	{
		ContractsAssemblyType = contractsAssemblyType;
	}
}
