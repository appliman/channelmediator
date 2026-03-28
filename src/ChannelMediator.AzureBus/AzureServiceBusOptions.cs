using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChannelMediator.AzureBus;

/// <summary>
/// Configuration options for Azure Service Bus integration.
/// </summary>
public sealed class AzureServiceBusOptions
{
    internal AzureServiceBusOptions()
    {
    }

    /// <summary>
    /// Gets or sets the collection of service descriptors for dependency injection.
    /// </summary>
    /// <remarks>Use this property to register, configure, or retrieve services within the application's
    /// dependency injection container. Modifying this collection affects the services available throughout the
    /// application's lifetime.</remarks>
	public IServiceCollection Services { get; set; } = default!;

    /// <summary>
    /// Gets or sets the strategy used to publish notifications.
    /// </summary>
    /// <remarks>The strategy determines the order and manner in which notifications are dispatched. Changing
    /// this property affects how notification handlers are invoked, which may impact performance or delivery guarantees
    /// depending on the selected strategy.</remarks>
	public NotificationPublishStrategy Strategy { get; set; } = NotificationPublishStrategy.Sequential;

	/// <summary>
	/// Gets or sets the Azure Service Bus connection string.
	/// </summary>
	public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the prefix for queue and topic names.
    /// </summary>
    public string Prefix { get; set; } = null!;

	/// <summary>
	/// Gets or sets the name of the subscriber.
	/// </summary>
	public string TopicSubscriberName { get; set; } = Environment.MachineName.ToLower();

	/// <summary>
	/// Gets or sets the maximum number of concurrent calls to the message handler.
	/// Default is 1.
	/// </summary>
	public int MaxConcurrentCalls { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether to auto-complete messages after processing.
    /// Default is true.
    /// </summary>
    public bool AutoCompleteMessages { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum duration within which the lock will be renewed automatically.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan MaxAutoLockRenewalDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the mode used to process messages with Azure Service Bus.
    /// </summary>
    /// <remarks>Use this property to specify whether the service operates in live or another supported mode.
    /// Changing the mode may affect how messages are handled and delivered. Refer to the documentation for the
    /// available values in the AzureServiceBusMode enumeration.</remarks>
    public AzureServiceBusMode ProcessMode {  get; set; } = AzureServiceBusMode.Live;

	internal bool SubscribeToAllTopics { get; set; } = true;

	public void AddAllAzureBusTopicNotification()
	{
		if (string.IsNullOrWhiteSpace(TopicSubscriberName))
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(Prefix))
		{
			return;
		}

		SubscribeToAllTopics = true;
		Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, TopicSubscriptionReadersHostedService>());
	}

	/// <summary>
	/// Adds a topic subscription reader for a specific notification type with custom options.
	/// Creates a singleton reader per topic/subscription combination.
	/// If the topic or subscription doesn't exist in Azure, they will be created automatically.
	/// </summary>
	/// <typeparam name="TNotification">The notification type this reader handles.</typeparam>
	/// <param name="subscriptionName">The subscription name to read from.</param>
	/// <param name="configure">Action to configure reader options.</param>
	/// <returns>The configuration for chaining.</returns>
	public void AddAzureBusTopicNotificationReader<TNotification>(
		string subscriptionName,
		Action<TopicSubscriptionReaderOptions>? configure = null)
		where TNotification : INotification
	{
		ArgumentNullException.ThrowIfNull(subscriptionName);

		var topicName = typeof(TNotification).Name;
		topicName = AzureServiceBusNameBuilder.Build(Prefix, topicName);

		var readerOptions = new TopicSubscriptionReaderOptions
		{
			TopicName = topicName,
			SubscriptionName = subscriptionName.ToLower(),
			MessageType = typeof(TNotification)
		};

		configure?.Invoke(readerOptions);

		// Register the reader options in the registry during service configuration
		TopicSubscriptionReaderRegistry.Register(readerOptions);

		// Ensure the hosted service is registered (only once)
		Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, TopicSubscriptionReadersHostedService>());
	}

	public void AddAzureQueueRequestReader<TRequest>(
		string? queueName = null,
		Action<QueueReaderOptions>? configure = null)
		where TRequest : IRequest
	{
		queueName ??= typeof(TRequest).Name;
		queueName = AzureServiceBusNameBuilder.Build(Prefix, queueName);
		var readerOptions = new QueueReaderOptions
		{
			QueueName = queueName,
			RequestType = typeof(TRequest)
		};
		configure?.Invoke(readerOptions);

		// Register the reader options in the registry during service configuration
		QueueReaderRegistry.Register(readerOptions);

		// Ensure the hosted service is registered (only once)
		Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, QueueReadersHostedService>());
	}
}
