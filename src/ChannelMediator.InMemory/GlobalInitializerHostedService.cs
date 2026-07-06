using Microsoft.Extensions.Hosting;

namespace ChannelMediator.InMemory;

/// <summary>
/// Hosted service that initializes the global publisher for the memory mediator extensions.
/// </summary>
internal sealed class GlobalInitializerHostedService : IHostedService
{
	private readonly IMemoryPublisher _globalPublisher;

	public GlobalInitializerHostedService(IMemoryPublisher globalPublisher)
	{
		_globalPublisher = globalPublisher ?? throw new ArgumentNullException(nameof(globalPublisher));
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		MediatorExtensions.SetGlobalPublisher(_globalPublisher);
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}
}
