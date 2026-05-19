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
	private readonly SemaphoreSlim _refreshLock = new(1, 1);
	private readonly HashSet<string> _subscribedTopics = new(StringComparer.OrdinalIgnoreCase);
	private ServiceBusProcessor? _reloadProcessor;
	private bool _disposed;
	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="TopicSubscriptionReadersHostedService"/> class.
	/// </summary>
	/// <param name="serviceProvider">The service provider.</param>
	/// <param name="logger">The logger used to record hosted service activity.</param>
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
				await reader.StartAsync(cancellationToken);
			}
		}
		else
		{
			var adminClient = _serviceProvider.GetRequiredService<ServiceBusAdministrationClient>();

			// Always start the internal reload-topics reader first — created by design.
			await StartReloadTopicsReaderAsync(options, client, entityManager, adminClient, cancellationToken);

			// Initial scan: subscribe to all existing topics matching the prefix.
			await RefreshSubscriptionsAsync(options, client, entityManager, adminClient, cancellationToken);
		}
	}

	/// <summary>
	/// Creates the internal <c>{prefix}reload-topics</c> topic and starts a processor on it.
	/// When a message arrives (sent by the publisher after creating a new topic), triggers
	/// a full subscription refresh so the reader picks up the new topic immediately.
	/// </summary>
	private async Task StartReloadTopicsReaderAsync(
		AzureServiceBusOptions options,
		ServiceBusClient client,
		AzureServiceBusEntityManager entityManager,
		ServiceBusAdministrationClient adminClient,
		CancellationToken cancellationToken)
	{
		var reloadTopicName = AzureServiceBusNameBuilder.BuildReloadTopicName(options.Prefix);
		var subscriptionName = options.TopicSubscriberName.ToLowerInvariant();

		await entityManager.EnsureTopicExistsAsync(reloadTopicName, cancellationToken);
		await entityManager.EnsureSubscriptionExistsAsync(reloadTopicName, subscriptionName, typeof(INotification), cancellationToken);

		_logger.LogInformation("[reload-topics] Listening on internal topic '{ReloadTopic}' / subscription '{Subscription}'.", reloadTopicName, subscriptionName);

		var processorOptions = new ServiceBusProcessorOptions
		{
			MaxConcurrentCalls = 1,
			AutoCompleteMessages = false,
			ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
		};

		_reloadProcessor = client.CreateProcessor(reloadTopicName, subscriptionName, processorOptions);

		_reloadProcessor.ProcessMessageAsync += async args =>
		{
			var newTopicName = args.Message.Body.ToString();
			_logger.LogInformation("[reload-topics] Signal received: topic '{NewTopic}' was created. Refreshing subscriptions...", newTopicName);
			await RefreshSubscriptionsAsync(options, client, entityManager, adminClient, args.CancellationToken);
		};

		_reloadProcessor.ProcessErrorAsync += args =>
		{
			_logger.LogError(args.Exception, "[reload-topics] Error processing reload signal.");
			return Task.CompletedTask;
		};

		await _reloadProcessor.StartProcessingAsync(cancellationToken);
	}

	/// <summary>
	/// Scans all Azure Service Bus topics matching <c>{prefix}*</c> (excluding the reload topic itself)
	/// and subscribes to any that are not yet tracked. Safe to call concurrently — serialised by a lock.
	/// </summary>
	private async Task RefreshSubscriptionsAsync(
		AzureServiceBusOptions options,
		ServiceBusClient client,
		AzureServiceBusEntityManager entityManager,
		ServiceBusAdministrationClient adminClient,
		CancellationToken cancellationToken)
	{
		await _refreshLock.WaitAsync(cancellationToken);
		try
		{
			var subscriptionName = options.TopicSubscriberName.ToLowerInvariant();
			var reloadTopicName = AzureServiceBusNameBuilder.BuildReloadTopicName(options.Prefix);

			await foreach (var topic in adminClient.GetTopicsAsync(cancellationToken))
			{
				if (!topic.Name.StartsWith(options.Prefix, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				// Skip the internal reload-topics topic — it is managed separately.
				if (string.Equals(topic.Name, reloadTopicName, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				if (_subscribedTopics.Contains(topic.Name))
				{
					continue;
				}

				_logger.LogDebug("Subscribing to topic '{TopicName}' (discovered via refresh).", topic.Name);

				await entityManager.EnsureSubscriptionExistsAsync(topic.Name, subscriptionName, typeof(INotification), cancellationToken);

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
				await reader.StartAsync(cancellationToken);

				_subscribedTopics.Add(topic.Name);
				_logger.LogInformation("Now listening on topic '{TopicName}'.", topic.Name);
			}
		}
		finally
		{
			_refreshLock.Release();
		}
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_reloadProcessor is not null)
		{
			await _reloadProcessor.StopProcessingAsync(cancellationToken);
		}

		foreach (var reader in _readers)
		{
			await reader.StopAsync(cancellationToken);
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

		if (_reloadProcessor is not null)
		{
			await _reloadProcessor.DisposeAsync();
		}

		foreach (var reader in _readers)
		{
			await reader.DisposeAsync();
		}

		_readers.Clear();
		_refreshLock.Dispose();
	}
}
