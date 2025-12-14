using ChannelMediator.AzureBus;

namespace ChannelMediator;

/// <summary>
/// Extension methods for IMediator to support Azure Service Bus global publishing.
/// </summary>
public static class MediatorExtensions
{
    private static IGlobalPublisher? _globalPublisher;
    private static readonly object _lock = new();

    /// <summary>
    /// Sets the global publisher instance. This is called internally during service configuration.
    /// </summary>
    /// <param name="globalPublisher">The global publisher instance.</param>
    internal static void SetGlobalPublisher(IGlobalPublisher globalPublisher)
    {
        lock (_lock)
        {
            _globalPublisher = globalPublisher;
        }
    }

    /// <summary>
    /// Publishes a notification globally to Azure Service Bus.
    /// The notification is sent to the topic registered for its type via AddAzureBusTopicReader.
    /// </summary>
    /// <typeparam name="TNotification">The type of the notification.</typeparam>
    /// <param name="mediator">The mediator instance.</param>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when Azure Service Bus is not configured or no topic is registered for the notification type.
    /// </exception>
    public static Task GlobalPublish<TNotification>(this IMediator mediator, TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(notification);

        var publisher = _globalPublisher 
            ?? throw new InvalidOperationException(
                "GlobalPublisher is not configured. Ensure UseAzureServiceBus() has been called during service configuration.");

        return publisher.PublishAsync(notification, cancellationToken);
    }
}
