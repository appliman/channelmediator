using Microsoft.Extensions.Hosting;

namespace ChannelMediator.AzureBus;

/// <summary>
/// Hosted service that initializes the global publisher for the MediatorExtensions.
/// </summary>
internal sealed class GlobalPublisherInitializerHostedService : IHostedService
{
    private readonly IAzurePublisher _globalPublisher;

    public GlobalPublisherInitializerHostedService(IAzurePublisher globalPublisher)
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
