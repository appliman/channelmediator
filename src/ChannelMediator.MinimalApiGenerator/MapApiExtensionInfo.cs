using Microsoft.CodeAnalysis;

namespace ChannelMediator.MinimalApiGenerator;

internal class MapApiExtensionInfo
{
	public string ClassName { get; set; } = null!;
	public string Namespace { get; set; } = null!;
	public bool WithVersionning { get; set; }
	public string[] ScanAssemblies { get; set; } = Array.Empty<string>();
	public bool IsStatic { get; set; }
	public bool IsPartial { get; set; }

	/// <summary>Location of the class declaration — excluded from equality so it doesn't break the incremental pipeline.</summary>
	public Location? Location { get; set; }

	public override bool Equals(object? obj)
	{
		return obj is MapApiExtensionInfo other
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