using System.Collections.Concurrent;
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
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task EnsureTopicExistsAsync(string topicName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);

        _logger.LogTrace("Ensuring topic exists: {TopicName}", topicName);

        // Check if we already verified this topic exists (local cache)
        if (_queueOrTopicsExist.ContainsKey(topicName))
        {
            _logger.LogTrace("Topic '{TopicName}' already verified in cache.", topicName);
            return;
        }

        try
        {
            // Check if the topic exists in Azure Service Bus
            if (!await _adminClient.TopicExistsAsync(topicName, cancellationToken).ConfigureAwait(false))
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

                await _adminClient.CreateTopicAsync(topicOptions, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Topic '{TopicName}' created.", topicName);
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
            if (!await _adminClient.QueueExistsAsync(queueName, cancellationToken).ConfigureAwait(false))
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
                await _adminClient.CreateQueueAsync(queueOptions, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Queue '{QueueName}' created.", queueName);
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
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task EnsureSubscriptionExistsAsync(
        string topicName,
        string subscriptionName,
        Type notificationType,
        CancellationToken cancellationToken = default)
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
            if (!await _adminClient.SubscriptionExistsAsync(topicName, subscriptionName, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation("Subscription '{SubscriptionKey}' does not exist. Creating...", subscriptionKey);

                var subscriptionOptions = new CreateSubscriptionOptions(topicName, subscriptionName)
                {
                    // Filter messages to only receive notifications of the expected type
                    DefaultMessageTimeToLive = TimeSpan.FromDays(1),
                    AutoDeleteOnIdle = TimeSpan.FromDays(1),
                    EnableBatchedOperations = true,
                };

                await _adminClient.CreateSubscriptionAsync(subscriptionOptions, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Subscription '{SubscriptionKey}' created.", subscriptionKey);
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
