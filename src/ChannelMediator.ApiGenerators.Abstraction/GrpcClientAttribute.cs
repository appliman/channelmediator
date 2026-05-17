namespace ChannelMediator.ApiGenerators.Abstraction;

/// <summary>
/// Applied at assembly level to enable the ChannelMediator gRPC client source generator
/// (<c>ChannelMediator.GrpcClientGenerator</c>) for the consuming project.
/// </summary>
/// <remarks>
/// The generator scans <see cref="ContractsAssemblyType"/>'s assembly for all request types decorated
/// with <see cref="EndpointApiAttribute"/> where <see cref="EndpointApiAttribute.Protocol"/>
/// includes <see cref="EndpointProtocol.Grpc"/>, then emits one <c>IRequestHandler</c> (or
/// <c>IStreamRequestHandler</c> for stream requests) per endpoint.
/// Each generated handler calls the corresponding method on the
/// <c>I{GroupName}Service</c> gRPC service interface via <c>GrpcClientFactory</c>.
/// </remarks>
/// <example>
/// <code>
/// [assembly: GrpcClient(typeof(MyContracts.Marker), GrpcClientName = "MyGrpcClient")]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
public class GrpcClientAttribute : Attribute
{
    /// <summary>
    /// A type from the assembly that contains the request contracts.
    /// The generator uses this type's assembly to discover all gRPC <see cref="EndpointApiAttribute"/> request types.
    /// </summary>
    public Type ContractsAssemblyType { get; }

    /// <summary>
    /// The name of the named gRPC client registered via <c>services.AddGrpcClient&lt;I{GroupName}Service&gt;(name)</c>.
    /// Defaults to <c>"GrpcClient"</c>.
    /// </summary>
    public string GrpcClientName { get; set; } = "GrpcClient";

    /// <summary>
    /// Initializes a new instance of <see cref="GrpcClientAttribute"/>.
    /// </summary>
    /// <param name="contractsAssemblyType">A type from the assembly containing the request contracts.</param>
    public GrpcClientAttribute(Type contractsAssemblyType)
    {
        ContractsAssemblyType = contractsAssemblyType;
    }
}
