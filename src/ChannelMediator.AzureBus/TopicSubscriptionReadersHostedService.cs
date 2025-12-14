using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChannelMediator.AzureBus;

/// <summary>
/// Background service that manages all registered topic subscription readers.
/// </summary>
internal sealed class TopicSubscriptionReadersHostedService : IHostedService, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<TopicSubscriptionReader> _readers = [];
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TopicSubscriptionReadersHostedService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public TopicSubscriptionReadersHostedService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var client = _serviceProvider.GetRequiredService<ServiceBusClient>();
        var entityManager = _serviceProvider.GetRequiredService<AzureServiceBusEntityManager>();

        foreach (var options in TopicSubscriptionReaderRegistry.GetAll())
        {
            var reader = new TopicSubscriptionReader(client, entityManager, options, _serviceProvider);
            _readers.Add(reader);
            await reader.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var reader in _readers)
        {
            await reader.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var reader in _readers)
        {
            await reader.DisposeAsync().ConfigureAwait(false);
        }

        _readers.Clear();
    }
}
