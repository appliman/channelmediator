using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChannelMediator.AzureBus;

/// <summary>
/// Background service that manages all registered topic subscription readers.
/// </summary>
internal sealed class TopicSubscriptionReadersHostedService : IHostedService, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<TopicSubscriptionReader> _readers = [];
    private bool _disposed;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TopicSubscriptionReadersHostedService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public TopicSubscriptionReadersHostedService(IServiceProvider serviceProvider, 
        ILogger<TopicSubscriptionReadersHostedService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _serviceProvider.GetRequiredService<AzureServiceBusOptions>();
        if (options.ProcessMode == AzureServiceBusMode.Mock)
        {
            return;
        }

        var client = _serviceProvider.GetRequiredService<ServiceBusClient>();
        var entityManager = _serviceProvider.GetRequiredService<AzureServiceBusEntityManager>();

        if (!options.SubscribeToAllTopics)
        {

            foreach (var readerOptions in TopicSubscriptionReaderRegistry.GetAll())
            {
                var reader = ActivatorUtilities.CreateInstance<TopicSubscriptionReader>(_serviceProvider, client, entityManager, readerOptions, _serviceProvider);
                _readers.Add(reader);
                await reader.StartAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
			var subscriptionName = options.TopicSubscriberName.ToLowerInvariant();
            var adminClient = _serviceProvider.GetRequiredService<ServiceBusAdministrationClient>();

			await foreach (var topic in adminClient.GetTopicsAsync())
			{
                _logger.LogDebug("Checking topic {TopicName} for subscription reader creation. status : {Status}.", topic.Name, topic.Status);
				if (!topic.Name.StartsWith(options.Prefix, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				await entityManager.EnsureSubscriptionExistsAsync(topic.Name, subscriptionName, typeof(INotification)).ConfigureAwait(false);

				var readerOptions = new TopicSubscriptionReaderOptions
				{
					TopicName = topic.Name,
					SubscriptionName = subscriptionName,
					MessageType = typeof(INotification),
					MaxConcurrentCalls = options.MaxConcurrentCalls,
					AutoCompleteMessages = options.AutoCompleteMessages,
					MaxAutoLockRenewalDuration = options.MaxAutoLockRenewalDuration
				};

				var reader = ActivatorUtilities.CreateInstance<TopicSubscriptionReader>(_serviceProvider, client, entityManager, readerOptions, _serviceProvider);
				_readers.Add(reader);
				await reader.StartAsync(cancellationToken).ConfigureAwait(false);
			}
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
