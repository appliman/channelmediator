using System.Collections.Concurrent;
using System.Diagnostics;

namespace ChannelMediator.RabbitMQ;

/// <summary>
/// Registry that keeps track of all configured topic subscription readers.
/// </summary>
internal sealed class TopicSubscriptionReaderRegistry
{
    private static readonly Lazy<TopicSubscriptionReaderRegistry> _lazyRegistry = new(() => new TopicSubscriptionReaderRegistry(), true);

    private readonly ConcurrentDictionary<string, TopicSubscriptionReaderOptions> _readers = new();
    private readonly ConcurrentDictionary<Type, TopicSubscriptionReaderOptions> _readersByNotificationType = new();

    /// <summary>
    /// Registers a new topic subscription reader configuration.
    /// </summary>
    /// <param name="options">The reader options.</param>
    public static void Register(TopicSubscriptionReaderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var key = CreateKey(options.ExchangeName, options.SubscriptionName);
        var registry = _lazyRegistry.Value;

        if (!registry._readers.TryAdd(key, options))
        {
            Trace.TraceWarning($"A TopicSubscriptionReader for exchange '{options.ExchangeName}' and subscription '{options.SubscriptionName}' is already registered.");
            return;
        }

        registry._readersByNotificationType.AddOrUpdate(options.MessageType, options, (_, _) => options);
    }

    /// <summary>
    /// Gets all registered reader configurations.
    /// </summary>
    public static IEnumerable<TopicSubscriptionReaderOptions> GetAll() => _lazyRegistry.Value._readers.Values;

    /// <summary>
    /// Gets the exchange name for a specific notification type.
    /// </summary>
    public static string? GetExchangeNameForNotificationType(Type notificationType)
    {
        return _lazyRegistry.Value._readersByNotificationType.TryGetValue(notificationType, out var options)
            ? options.ExchangeName
            : null;
    }

    private static string CreateKey(string exchangeName, string subscriptionName)
        => $"{exchangeName}::{subscriptionName}";
}
