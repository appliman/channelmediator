using Microsoft.CodeAnalysis;

namespace ChannelMediator.GrpcGenerator;

internal class GrpcServiceInfo
{
    public string ClassName { get; set; } = null!;
    public string Namespace { get; set; } = null!;
    public string[] ScanAssemblies { get; set; } = Array.Empty<string>();
    public bool IsStatic { get; set; }
    public bool IsPartial { get; set; }
    public Location? Location { get; set; }

    public override bool Equals(object? obj)
    {
        return obj is GrpcServiceInfo other
            && ClassName == other.ClassName
            && Namespace == other.Namespace
            && IsStatic == other.IsStatic
            && IsPartial == other.IsPartial;
    }

    public override int GetHashCode()
    {
        return (ClassName, Namespace, IsStatic, IsPartial).GetHashCode();
    }
}
