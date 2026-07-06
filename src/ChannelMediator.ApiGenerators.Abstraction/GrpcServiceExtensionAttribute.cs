namespace ChannelMediator.ApiGenerators.Abstraction;

/// <summary>
/// Marks a <see langword="static partial"/> class as the target for the ChannelMediator
/// gRPC source generator (<c>ChannelMediator.GrpcGenerator</c>).
/// </summary>
/// <remarks>
/// The generator scans the compilation for all request types decorated with
/// <see cref="EndpointApiAttribute"/> where <see cref="EndpointApiAttribute.Protocol"/>
/// includes <see cref="EndpointProtocol.Grpc"/>.
/// For each distinct <c>GroupName</c> it emits:
/// <list type="bullet">
///   <item>An <c>I{GroupName}Service</c> interface with <c>[ServiceContract]</c> and <c>[OperationContract]</c> operations (requires <c>protobuf-net.Grpc</c>).</item>
///   <item>A <c>{GroupName}ServiceImpl</c> class that delegates to <c>IMediator</c>.</item>
/// </list>
/// The decorated class must be <see langword="static"/> and <see langword="partial"/>.
/// Call the generated <c>Map{ClassName}GrpcServices(this IEndpointRouteBuilder)</c> extension in your <c>Program.cs</c>.
/// </remarks>
/// <example>
/// <code>
/// [GrpcServiceExtension]
/// public static partial class MyGrpcMapper { }
///
/// // In Program.cs:
/// app.MapMyGrpcMapperGrpcServices();
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class GrpcServiceExtensionAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the list of assembly names to scan for <see cref="EndpointApiAttribute"/> gRPC types.
    /// </summary>
    /// <remarks>
    /// When empty or <see langword="null"/>, the generator scans all referenced assemblies.
    /// When specified, only the listed assemblies are scanned.
    /// Use the simple assembly name (e.g. <c>"MyContracts"</c>).
    /// </remarks>
    public string[]? ScanAssemblies { get; set; }
}
