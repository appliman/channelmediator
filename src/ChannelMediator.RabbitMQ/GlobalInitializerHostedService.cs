using Microsoft.Extensions.Hosting;

namespace ChannelMediator.RabbitMQ;

/// <summary>
/// Hosted service that initializes the global publisher for the MediatorExtensions.
/// </summary>
internal sealed class GlobalInitializerHostedService : IHostedService
{
    private readonly IRabbitMqPublisher _globalPublisher;

    public GlobalInitializerHostedService(IRabbitMqPublisher globalPublisher)
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
