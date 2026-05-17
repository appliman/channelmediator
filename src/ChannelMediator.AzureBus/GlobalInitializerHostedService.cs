using Microsoft.Extensions.Hosting;

namespace ChannelMediator.AzureBus;

/// <summary>
/// Hosted service that initializes the global publisher for the MediatorExtensions.
/// </summary>
internal sealed class GlobalInitializerHostedService : IHostedService
{
    private readonly IAzurePublisher _globalPublisher;

	public GlobalInitializerHostedService(IAzurePublisher globalPublisher)
    {
        _globalPublisher = globalPublisher ?? throw new ArgumentNullException(nameof(globalPublisher));
	}

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        MediatorExtensions.SetGlobalPublisher(_globalPublisher);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
