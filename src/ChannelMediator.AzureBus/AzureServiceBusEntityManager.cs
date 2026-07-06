using System.Collections.Concurrent;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using Microsoft.Extensions.Logging;

namespace ChannelMediator.AzureBus;

/// <summary>
/// Manages Azure Service Bus entities (topics and subscriptions) creation and existence verification.
/// </summary>
internal sealed class AzureServiceBusEntityManager
{
	private readonly ServiceBusAdministrationClient _adminClient;
	private readonly ILogger<AzureServiceBusEntityManager> _logger;
	private readonly ConcurrentDictionary<string, bool> _queueOrTopicsExist = new();
	private readonly ConcurrentDictionary<string, bool> _subscriptionsExist = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureServiceBusEntityManager"/> class.
	/// </summary>
	/// <param name="adminClient">The Service Bus administration client.</param>
	/// <param name="logger">The logger used to record entity management activity.</param>
	public AzureServiceBusEntityManager(ServiceBusAdministrationClient adminClient, ILogger<AzureServiceBusEntityManager> logger)
	{
		_adminClient = adminClient ?? throw new ArgumentNullException(nameof(adminClient));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Ensures that a topic exists in Azure Service Bus, creating it if necessary.
	/// </summary>
	/// <param name="topicName">The name of the topic.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><c>true</c> if the topic was newly created; <c>false</c> if it already existed.</returns>
	public async Task<bool> EnsureTopicExistsAsync(string topicName, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topicName);

		_logger.LogTrace("Ensuring topic exists: {TopicName}", topicName);

		// Check if we already verified this topic exists (local cache)
		if (_queueOrTopicsExist.ContainsKey(topicName))
		{
			_logger.LogTrace("Topic '{TopicName}' already verified in cache.", topicName);
			return false;
		}

		bool created = false;
		try
		{
			// Check if the topic exists in Azure Service Bus
			if (!await _adminClient.TopicExistsAsync(topicName, cancellationToken))
			{
				_logger.LogInformation("Topic '{TopicName}' does not exist. Creating...", topicName);

				var topicOptions = new CreateTopicOptions(topicName)
				{
					DefaultMessageTimeToLive = TimeSpan.FromDays(1),
					AutoDeleteOnIdle = TimeSpan.FromDays(1),
					EnableBatchedOperations = true,
				};
				topicOptions.AuthorizationRules.Add(new SharedAccessAuthorizationRule("allClaims"
					, new[] { AccessRights.Manage, AccessRights.Send, AccessRights.Listen }));

				try
				{
					await _adminClient.CreateTopicAsync(topicOptions, cancellationToken);
					_logger.LogInformation("Topic '{TopicName}' created.", topicName);
					created = true;
				}
				catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
				{
					// Topic was absent at check time but another process created it first.
					// Treat as newly created so the caller still performs subscription setup.
					_logger.LogTrace("Topic '{TopicName}' was created concurrently by another process.", topicName);
					created = true;
				}
			}
			else
			{
				_logger.LogTrace("Topic '{TopicName}' already exists in Service Bus.", topicName);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error ensuring topic '{TopicName}' exists.", topicName);
			throw;
		}

		// Mark this topic as verified
		_queueOrTopicsExist.TryAdd(topicName, true);
		return created;
	}

	public async Task EnsureQueueExistsAsync(string queueName, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
		_logger.LogTrace("Ensuring queue exists: {QueueName}", queueName);

		// Check if we already verified this queue exists (local cache)
		if (_queueOrTopicsExist.ContainsKey(queueName))
		{
			_logger.LogTrace("Queue '{QueueName}' already verified in cache.", queueName);
			return;
		}

		try
		{
			// Check if the queue exists in Azure Service Bus
			if (!await _adminClient.QueueExistsAsync(queueName, cancellationToken))
			{
				_logger.LogInformation("Queue '{QueueName}' does not exist. Creating...", queueName);

				var queueOptions = new CreateQueueOptions(queueName)
				{
					DefaultMessageTimeToLive = TimeSpan.FromDays(1),
					AutoDeleteOnIdle = TimeSpan.FromDays(1),
					EnableBatchedOperations = true,
				};
				queueOptions.AuthorizationRules.Add(new SharedAccessAuthorizationRule("allClaims"
					, new[] { AccessRights.Manage, AccessRights.Send, AccessRights.Listen }));
				try
				{
					await _adminClient.CreateQueueAsync(queueOptions, cancellationToken);
					_logger.LogInformation("Queue '{QueueName}' created.", queueName);
				}
				catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
				{
					_logger.LogTrace("Queue '{QueueName}' was created concurrently by another process.", queueName);
				}
			}
			else
			{
				_logger.LogTrace("Queue '{QueueName}' already exists in Service Bus.", queueName);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error ensuring queue '{QueueName}' exists.", queueName);
			throw;
		}

		// Mark this queue as verified
		_queueOrTopicsExist.TryAdd(queueName, true);
	}

	/// <summary>
	/// Ensures that a subscription exists for a topic in Azure Service Bus, creating it if necessary.
	/// </summary>
	/// <param name="topicName">The name of the topic.</param>
	/// <param name="subscriptionName">The name of the subscription.</param>
	/// <param name="notificationType">The notification type for filtering messages.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <param name="autoDeleteOnIdle">When set, overrides the default <c>AutoDeleteOnIdle</c> for the new subscription. Use a short value for ephemeral per-instance subscriptions.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task EnsureSubscriptionExistsAsync(
		string topicName,
		string subscriptionName,
		Type notificationType,
		CancellationToken cancellationToken = default,
		TimeSpan? autoDeleteOnIdle = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
		ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
		ArgumentNullException.ThrowIfNull(notificationType);

		var subscriptionKey = $"{topicName}/{subscriptionName}";

		_logger.LogTrace("Ensuring subscription exists: {TopicName}/{SubscriptionName}", topicName, subscriptionName);

		// Check if we already verified this subscription exists (local cache)
		if (_subscriptionsExist.ContainsKey(subscriptionKey))
		{
			_logger.LogTrace("Subscription '{SubscriptionKey}' already verified in cache.", subscriptionKey);
			return;
		}

		try
		{
			// Check if the subscription exists in Azure Service Bus
			if (!await _adminClient.SubscriptionExistsAsync(topicName, subscriptionName, cancellationToken))
			{
				_logger.LogInformation("Subscription '{SubscriptionKey}' does not exist. Creating...", subscriptionKey);

				var subscriptionOptions = new CreateSubscriptionOptions(topicName, subscriptionName)
				{
					// Filter messages to only receive notifications of the expected type
					DefaultMessageTimeToLive = TimeSpan.FromDays(1),
					AutoDeleteOnIdle = autoDeleteOnIdle ?? TimeSpan.FromDays(1),
					EnableBatchedOperations = true,
				};

				try
				{
					await _adminClient.CreateSubscriptionAsync(subscriptionOptions, cancellationToken);
					_logger.LogInformation("Subscription '{SubscriptionKey}' created.", subscriptionKey);
				}
				catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
				{
					_logger.LogTrace("Subscription '{SubscriptionKey}' was created concurrently by another process.", subscriptionKey);
				}
			}
			else
			{
				_logger.LogTrace("Subscription '{SubscriptionKey}' already exists in Service Bus.", subscriptionKey);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error ensuring subscription '{SubscriptionKey}' exists.", subscriptionKey);
			throw;
		}

		// Mark this subscription as verified
		_subscriptionsExist.TryAdd(subscriptionKey, true);
	}
}
