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

	/// <summary>Location of the class declaration. Excluded from equality because it changes between compilations and must not defeat the incremental pipeline cache. <see cref="IsStatic"/> and <see cref="IsPartial"/> are included in equality because a change in those modifiers must invalidate the cached result.</summary>
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