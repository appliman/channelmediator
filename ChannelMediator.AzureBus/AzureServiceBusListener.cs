using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace ChannelMediator.AzureBus;

/// <summary>
/// Listens to Azure Service Bus messages and dispatches them to notification handlers.
/// </summary>
public sealed class AzureServiceBusListener : IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly AzureServiceBusOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly JsonSerializerOptions _jsonOptions;
    private ServiceBusProcessor? _processor;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureServiceBusListener"/> class.
    /// </summary>
    /// <param name="client">The Service Bus client.</param>
    /// <param name="options">The configuration options.</param>
    /// <param name="serviceProvider">The service provider for resolving handlers.</param>
    public AzureServiceBusListener(
        ServiceBusClient client,
        AzureServiceBusOptions options,
        IServiceProvider serviceProvider)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Starts listening for messages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _processor = CreateProcessor();
        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        await _processor.StartProcessingAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops listening for messages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private ServiceBusProcessor CreateProcessor()
    {
        var processorOptions = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = _options.MaxConcurrentCalls,
            AutoCompleteMessages = _options.AutoCompleteMessages,
            MaxAutoLockRenewalDuration = _options.MaxAutoLockRenewalDuration
        };

        var subscription = $"{_options.Prefix}";
        return _client.CreateProcessor(_options.sub, processorOptions);
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var notificationTypeName = args.Message.ApplicationProperties.TryGetValue("NotificationType", out var typeNameObj)
            ? typeNameObj as string
            : null;

        if (string.IsNullOrWhiteSpace(notificationTypeName))
        {
            // Log warning: Missing notification type
            return;
        }

        var notificationType = Type.GetType(notificationTypeName);
        if (notificationType is null)
        {
            // Log warning: Unknown notification type
            return;
        }

        var notification = JsonSerializer.Deserialize(args.Message.Body.ToArray(), notificationType, _jsonOptions);
        if (notification is null)
        {
            // Log warning: Failed to deserialize notification
            return;
        }

        await DispatchNotificationAsync(notification, notificationType, args.CancellationToken).ConfigureAwait(false);
    }

    private async Task DispatchNotificationAsync(object notification, Type notificationType, CancellationToken cancellationToken)
    {
        // Get the mediator and publish the notification
        var mediator = _serviceProvider.GetService(typeof(IMediator)) as IMediator;
        if (mediator is null)
        {
            throw new InvalidOperationException("IMediator is not registered in the service provider.");
        }

        await mediator.Publish(notification, cancellationToken).ConfigureAwait(false);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        // Log the error
        // In a production scenario, you would use ILogger here
        Console.Error.WriteLine($"Service Bus error: {args.Exception.Message}");
        Console.Error.WriteLine($"Error source: {args.ErrorSource}");
        Console.Error.WriteLine($"Entity path: {args.EntityPath}");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_processor is not null)
        {
            _processor.ProcessMessageAsync -= ProcessMessageAsync;
            _processor.ProcessErrorAsync -= ProcessErrorAsync;
            await _processor.DisposeAsync().ConfigureAwait(false);
        }
    }
}
