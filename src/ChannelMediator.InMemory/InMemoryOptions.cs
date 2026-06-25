using Microsoft.Extensions.DependencyInjection;

namespace ChannelMediator.InMemory;

/// <summary>
/// Configuration options for ChannelMediator in-memory publishing.
/// </summary>
public sealed class InMemoryOptions
{
	internal InMemoryOptions()
	{
	}

	/// <summary>
	/// Gets or sets the collection of service descriptors for dependency injection.
	/// </summary>
	public IServiceCollection Services { get; set; } = default!;
}
