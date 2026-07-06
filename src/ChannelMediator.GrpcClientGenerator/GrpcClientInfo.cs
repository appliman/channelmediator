using Microsoft.CodeAnalysis;

namespace ChannelMediator.GrpcClientGenerator;

internal class GrpcClientInfo
{
    public IAssemblySymbol ContractsAssembly { get; set; } = null!;
    public string OutputNamespace { get; set; } = null!;
    public string GrpcClientName { get; set; } = "GrpcClient";
}

internal class GrpcEndpointInfo
{
    public string RequestFullName { get; set; } = null!;
    public string RequestShortName { get; set; } = null!;
    public string Namespace { get; set; } = null!;
    public string GroupName { get; set; } = null!;
    public string ResponseTypeName { get; set; } = null!;
    public bool IsStream { get; set; }
    public string MethodName { get; set; } = null!;
}
