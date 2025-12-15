using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus.Administration;

namespace ChannelMediator.AzureBus;

/// <summary>
/// Manages Azure Service Bus entities (topics and subscriptions) creation and existence verification.
/// </summary>
internal sealed class AzureServiceBusEntityManager
{
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ConcurrentDictionary<string, bool> _queueOrTopicsExist = new();
    private readonly ConcurrentDictionary<string, bool> _subscriptionsExist = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureServiceBusEntityManager"/> class.
    /// </summary>
    /// <param name="adminClient">The Service Bus administration client.</param>
    public AzureServiceBusEntityManager(ServiceBusAdministrationClient adminClient)
    {
        _adminClient = adminClient ?? throw new ArgumentNullException(nameof(adminClient));
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

        // Check if we already verified this topic exists (local cache)
        if (_queueOrTopicsExist.ContainsKey(topicName))
        {
            return;
        }

        // Check if the topic exists in Azure Service Bus
        if (!await _adminClient.TopicExistsAsync(topicName, cancellationToken).ConfigureAwait(false))
        {
            var topicOptions = new CreateTopicOptions(topicName)
            {
                DefaultMessageTimeToLive = TimeSpan.FromDays(1),
                AutoDeleteOnIdle = TimeSpan.FromDays(1),
                EnableBatchedOperations = true,
            };
            topicOptions.AuthorizationRules.Add(new SharedAccessAuthorizationRule("allClaims"
                , new[] { AccessRights.Manage, AccessRights.Send, AccessRights.Listen }));

            await _adminClient.CreateTopicAsync(topicOptions, cancellationToken).ConfigureAwait(false);
        }

        // Mark this topic as verified
        _queueOrTopicsExist.TryAdd(topicName, true);
    }

    public async Task EnsureQueueExistsAsync(string queueName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        // Check if we already verified this queue exists (local cache)
        if (_queueOrTopicsExist.ContainsKey(queueName))
        {
            return;
        }
        // Check if the queue exists in Azure Service Bus
        if (!await _adminClient.QueueExistsAsync(queueName, cancellationToken).ConfigureAwait(false))
        {
            var queueOptions = new CreateQueueOptions(queueName)
            {
                DefaultMessageTimeToLive = TimeSpan.FromDays(1),
                AutoDeleteOnIdle = TimeSpan.FromDays(1),
                EnableBatchedOperations = true,
            };
            queueOptions.AuthorizationRules.Add(new SharedAccessAuthorizationRule("allClaims"
                , new[] { AccessRights.Manage, AccessRights.Send, AccessRights.Listen }));
            await _adminClient.CreateQueueAsync(queueOptions, cancellationToken).ConfigureAwait(false);
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

        // Check if we already verified this subscription exists (local cache)
        if (_subscriptionsExist.ContainsKey(subscriptionKey))
        {
            return;
        }

        // Check if the subscription exists in Azure Service Bus
        if (!await _adminClient.SubscriptionExistsAsync(topicName, subscriptionName, cancellationToken).ConfigureAwait(false))
        {
            var subscriptionOptions = new CreateSubscriptionOptions(topicName, subscriptionName)
            {
                // Filter messages to only receive notifications of the expected type
                DefaultMessageTimeToLive = TimeSpan.FromDays(1),
                AutoDeleteOnIdle = TimeSpan.FromDays(1),
                EnableBatchedOperations = true,
            };

            await _adminClient.CreateSubscriptionAsync(subscriptionOptions, cancellationToken).ConfigureAwait(false);
        }

        // Mark this subscription as verified
        _subscriptionsExist.TryAdd(subscriptionKey, true);
    }
}
