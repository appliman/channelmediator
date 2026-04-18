using Microsoft.CodeAnalysis;

namespace ChannelMediator.MinimalApiGenerator;

internal class EndpointApiInfo
{
	public string RequestTypeName { get; set; } = null!;
	public string RequestShortName { get; set; } = null!;
	public string Namespace { get; set; } = null!;
	public string GroupName { get; set; } = null!;
	public bool HasExplicitGroupName { get; set; }
	public Location? Location { get; set; }
	public string EntityName { get; set; } = null!;
	public bool HasExplicitEntityName { get; set; }
	public string[] Tags { get; set; } = Array.Empty<string>();
	public string? Summary { get; set; }
	public string? Description { get; set; }
	public string[] AuthenticationSchemes { get; set; } = Array.Empty<string>();
	public string HttpVerb { get; set; } = "POST";
	public bool UseHttpStandardVerbs { get; set; }
	public List<RequestParameter> Parameters { get; set; } = new();
	public bool IsResponseNullable { get; set; }
	public string ResponseTypeName { get; set; } = null!;

	public override bool Equals(object? obj)
	{
		return obj is EndpointApiInfo other
			&& RequestTypeName == other.RequestTypeName;
	}

	public override int GetHashCode()
	{
		return RequestTypeName.GetHashCode();
	}
}