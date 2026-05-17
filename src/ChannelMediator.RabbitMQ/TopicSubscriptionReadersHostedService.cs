using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

namespace ChannelMediator.RabbitMQ;

/// <summary>
/// Background service that manages all registered topic (exchange) subscription readers.
/// </summary>
internal sealed class TopicSubscriptionReadersHostedService : IHostedService, IAsyncDisposable
{
	private readonly IServiceProvider _serviceProvider;
	private readonly List<TopicSubscriptionReader> _readers = [];
	private readonly ILogger _logger;
	private bool _disposed;

	public TopicSubscriptionReadersHostedService(
		IServiceProvider serviceProvider,
		ILogger<TopicSubscriptionReadersHostedService> logger)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		var options = _serviceProvider.GetRequiredService<RabbitMqOptions>();
		if (options.ProcessMode == RabbitMqMode.Mock)
		{
			return;
		}

		var connection = _serviceProvider.GetRequiredService<IConnection>();
		var entityManager = _serviceProvider.GetRequiredService<RabbitMqEntityManager>();

		if (!options.SubscribeToAllTopics)
		{
			foreach (var readerOptions in TopicSubscriptionReaderRegistry.GetAll())
			{
				var reader = ActivatorUtilities.CreateInstance<TopicSubscriptionReader>(
					_serviceProvider, connection, entityManager, readerOptions, _serviceProvider);
				_readers.Add(reader);
				await reader.StartAsync(cancellationToken);
			}
		}
		else
		{
			// RabbitMQ does not have a management API built into the client to list exchanges.
			// We discover notification types by scanning the DI registrations for INotificationHandler<>.
			var subscriptionName = options.TopicSubscriberName.ToLowerInvariant();
			var processedExchanges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			var notificationHandlerOpenType = typeof(INotificationHandler<>);
			var notificationTypes = options.Services
				.Where(sd => sd.ServiceType.IsGenericType &&
							 sd.ServiceType.GetGenericTypeDefinition() == notificationHandlerOpenType)
				.Select(sd => sd.ServiceType.GetGenericArguments()[0])
				.Distinct()
				.ToList();

			foreach (var notificationType in notificationTypes)
			{
				var exchangeName = RabbitMqNameBuilder.Build(options.Prefix, notificationType.Name);

				if (!processedExchanges.Add(exchangeName))
				{
					continue;
				}

				_logger.LogDebug("Creating subscription reader for exchange {ExchangeName}.", exchangeName);

				var readerOptions = new TopicSubscriptionReaderOptions
				{
					ExchangeName = exchangeName,
					SubscriptionName = subscriptionName,
					MessageType = typeof(INotification),
					PrefetchCount = options.PrefetchCount
				};

				var reader = ActivatorUtilities.CreateInstance<TopicSubscriptionReader>(
					_serviceProvider, connection, entityManager, readerOptions, _serviceProvider);
				_readers.Add(reader);
				await reader.StartAsync(cancellationToken);
			}
		}
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
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

		foreach (var reader in _readers)
		{
			await reader.DisposeAsync();
		}

		_readers.Clear();
	}
}
