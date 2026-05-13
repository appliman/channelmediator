namespace ChannelMediator.ApiGenerators.Abstraction;

/// <summary>
/// Specifies which transport protocol(s) an endpoint decorated with <see cref="EndpointApiAttribute"/> should be exposed on.
/// </summary>
/// <remarks>
/// This is a flags enum — use <see cref="Both"/> to expose the same request on both HTTP and gRPC simultaneously.
/// </remarks>
[Flags]
public enum EndpointProtocol
{
    /// <summary>
    /// Expose the endpoint as an ASP.NET Core Minimal API HTTP endpoint.
    /// The <c>ChannelMediator.MinimalApiGenerator</c> source generator picks this up.
    /// This is the default.
    /// </summary>
    Http = 1,

    /// <summary>
    /// Expose the endpoint as a gRPC operation (via <c>protobuf-net.Grpc</c>).
    /// The <c>ChannelMediator.GrpcGenerator</c> source generator picks this up.
    /// </summary>
    Grpc = 2,

    /// <summary>
    /// Expose the endpoint on both HTTP (Minimal API) and gRPC.
    /// Both source generators will generate code for this request.
    /// </summary>
    Both = Http | Grpc
}
