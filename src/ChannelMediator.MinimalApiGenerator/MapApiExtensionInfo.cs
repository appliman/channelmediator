namespace ChannelMediator.MinimalApiGenerator;

internal class MapApiExtensionInfo
{
	public string ClassName { get; set; } = null!;
	public string Namespace { get; set; } = null!;
	public bool WithVersionning { get; set; }
	public string[] ScanAssemblies { get; set; } = Array.Empty<string>();

	public override bool Equals(object? obj)
	{
		return obj is MapApiExtensionInfo other
			&& ClassName == other.ClassName
			&& Namespace == other.Namespace;
	}

	public override int GetHashCode()
	{
		return (ClassName, Namespace).GetHashCode();
	}
}