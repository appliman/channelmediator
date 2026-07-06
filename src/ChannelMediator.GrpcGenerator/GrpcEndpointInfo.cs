using Microsoft.CodeAnalysis;

namespace ChannelMediator.GrpcGenerator;

internal class GrpcEndpointInfo
{
    public string RequestTypeName { get; set; } = null!;
    public string RequestShortName { get; set; } = null!;
    public string GroupName { get; set; } = null!;
    public bool HasExplicitGroupName { get; set; }
    public Location? Location { get; set; }
    public string ResponseTypeName { get; set; } = null!;
    public bool IsStream { get; set; }
    /// <summary>gRPC method name — defaults to RequestShortName with "Request" suffix stripped.</summary>
    public string MethodName { get; set; } = null!;

    public override bool Equals(object? obj)
    {
        return obj is GrpcEndpointInfo other
            && RequestTypeName == other.RequestTypeName;
    }

    public override int GetHashCode()
    {
        return RequestTypeName.GetHashCode();
    }
}
