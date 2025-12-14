using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChannelMediator.AzureBus;

/// <summary>
/// Extension methods for configuring Azure Service Bus integration with ChannelMediator.
/// </summary>
public static class ChannelMediatorExtensions
{
    /// <summary>
    /// Adds Azure Service Bus integration with the specified connection string.
    /// Uses default options with queue-based messaging.
    /// </summary>
    /// <param name="configuration">The ChannelMediator configuration.</param>
    /// <param name="connectionString">The Azure Service Bus connection string.</param>
    /// <returns>The configuration for chaining.</returns>
    public static AzureServiceBusOptions UseAzureServiceBus(this ChannelMediatorConfiguration configuration, string connectionString)
    {
        return configuration.UseAzureServiceBus(options =>
        {
            options.ConnectionString = connectionString;
        });
    }

    /// <summary>
    /// Adds Azure Service Bus integration with custom configuration.
    /// </summary>
    /// <param name="configuration">The ChannelMediator configuration.</param>
    /// <param name="configure">Action to configure Azure Service Bus options.</param>
    /// <returns>The configuration for chaining.</returns>
    public static AzureServiceBusOptions UseAzureServiceBus(this ChannelMediatorConfiguration configuration, Action<AzureServiceBusOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AzureServiceBusOptions();
        options.ChannelMediatorConfiguration = configuration;
        configure(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ArgumentException("ConnectionString must be specified.", nameof(configure));
        }

        configuration.Services.TryAddSingleton(options);

        // Register ServiceBusClient as singleton
        configuration.Services.TryAddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<AzureServiceBusOptions>();
            return new ServiceBusClient(opts.ConnectionString);
        });

        // Register ServiceBusAdministrationClient for creating topics/subscriptions
        configuration.Services.TryAddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<AzureServiceBusOptions>();
            return new ServiceBusAdministrationClient(opts.ConnectionString);
        });

        // Register the entity manager for managing topics and subscriptions
        configuration.Services.TryAddSingleton<AzureServiceBusEntityManager>();

        // Register the global publisher
        configuration.Services.TryAddSingleton<IGlobalPublisher, AzureServiceBusPublisher>();

        // Register the initializer hosted service to set up the static accessor
        configuration.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, GlobalPublisherInitializerHostedService>());

        return options;
    }

    /// <summary>
    /// Adds a topic subscription reader for a specific notification type with custom options.
    /// Creates a singleton reader per topic/subscription combination.
    /// If the topic or subscription doesn't exist in Azure, they will be created automatically.
    /// </summary>
    /// <typeparam name="TNotification">The notification type this reader handles.</typeparam>
    /// <param name="configuration">The ChannelMediator configuration.</param>
    /// <param name="topicName">The topic name to read from.</param>
    /// <param name="subscriptionName">The subscription name to read from.</param>
    /// <param name="configure">Action to configure reader options.</param>
    /// <returns>The configuration for chaining.</returns>
    public static AzureServiceBusOptions AddAzureBusTopicReader<TNotification>(
        this AzureServiceBusOptions options,
        string? topicName = null,
        Action<TopicSubscriptionReaderOptions>? configure = null)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(options);

        topicName ??= typeof(TNotification).Name;
        topicName = $"channel-mediator-{topicName}".ToLower();
        if (options.Prefix is not null)
        {
            topicName = $"{options.Prefix}-{topicName}".ToLower();
        }

        var readerOptions = new TopicSubscriptionReaderOptions
        {
            TopicName = topicName,
            SubscriptionName = options.SubscriptionName,
            NotificationType = typeof(TNotification)
        };

        configure?.Invoke(readerOptions);

        // Register the reader options in the registry during service configuration
        TopicSubscriptionReaderRegistry.Register(readerOptions);

        // Ensure the hosted service is registered (only once)
        options.ChannelMediatorConfiguration.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, TopicSubscriptionReadersHostedService>());

        return options;
    }
}
