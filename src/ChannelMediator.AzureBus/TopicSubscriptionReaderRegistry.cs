using System.Collections.Concurrent;
using System.Diagnostics;

namespace ChannelMediator.AzureBus;

/// <summary>
/// Registry that keeps track of all configured topic subscription readers.
/// This is used to ensure each topic/subscription combination has only one reader.
/// </summary>
internal sealed class TopicSubscriptionReaderRegistry
{
    private static Lazy<TopicSubscriptionReaderRegistry> _lazyRegistry = new Lazy<TopicSubscriptionReaderRegistry>(() =>
    {
        var registry = new TopicSubscriptionReaderRegistry();
        return registry;
    }, true);

    private readonly ConcurrentDictionary<string, TopicSubscriptionReaderOptions> _readers = new();
    private readonly ConcurrentDictionary<Type, TopicSubscriptionReaderOptions> _readersByNotificationType = new();

    /// <summary>
    /// Registers a new topic subscription reader configuration.
    /// </summary>
    /// <param name="options">The reader options.</param>
    public static void Register(TopicSubscriptionReaderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var key = CreateKey(options.TopicName, options.SubscriptionName);

		var registry = _lazyRegistry.Value;
		if (!registry._readers.TryAdd(key, options))
		{
			Trace.TraceWarning($"A TopicSubscriptionReader for topic '{options.TopicName}' and subscription '{options.SubscriptionName}' is already registered.");
            return;
		}

        registry._readersByNotificationType.AddOrUpdate(options.MessageType, options, (_, _) => options);
    }

    /// <summary>
    /// Gets all registered reader configurations.
    /// </summary>
    /// <returns>All registered reader options.</returns>
    public static IEnumerable<TopicSubscriptionReaderOptions> GetAll() => _lazyRegistry.Value._readers.Values;

    /// <summary>
    /// Gets the count of registered readers.
    /// </summary>
    public int Count => _readers.Count;

    /// <summary>
    /// Checks if a reader is registered for the specified topic and subscription.
    /// </summary>
    /// <param name="topicName">The topic name.</param>
    /// <param name="subscriptionName">The subscription name.</param>
    /// <returns>True if a reader is registered; otherwise, false.</returns>
    public bool IsRegistered(string topicName, string subscriptionName)
    {
        var key = CreateKey(topicName, subscriptionName);
        return _readers.ContainsKey(key);
    }

    /// <summary>
    /// Tries to get the reader options for a specific notification type.
    /// </summary>
    /// <param name="notificationType">The notification type.</param>
    /// <param name="options">The reader options if found.</param>
    /// <returns>True if a reader is registered for the notification type; otherwise, false.</returns>
    public bool TryGetByNotificationType(Type notificationType, out TopicSubscriptionReaderOptions? options)
    {
        return _readersByNotificationType.TryGetValue(notificationType, out options);
    }

    /// <summary>
    /// Gets the topic name for a specific notification type.
    /// </summary>
    /// <param name="notificationType">The notification type.</param>
    /// <returns>The topic name if found; otherwise, null.</returns>
    public static string? GetTopicNameForNotificationType(Type notificationType)
    {
        var topicName = _lazyRegistry.Value._readersByNotificationType.TryGetValue(notificationType, out var options)
            ? options.TopicName
            : null;

        return topicName;
    }

    private static string CreateKey(string topicName, string subscriptionName) 
        => $"{topicName}::{subscriptionName}";
}
