using Microsoft.Extensions.Hosting;

namespace ChannelMediator.AzureBus;

/// <summary>
/// Background service that manages the Azure Service Bus listener lifecycle.
/// </summary>
public sealed class AzureServiceBusHostedService : IHostedService, IAsyncDisposable
{
    private readonly AzureServiceBusListener _listener;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureServiceBusHostedService"/> class.
    /// </summary>
    /// <param name="listener">The Azure Service Bus listener.</param>
    public AzureServiceBusHostedService(AzureServiceBusListener listener)
    {
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _listener.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _listener.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _listener.DisposeAsync().ConfigureAwait(false);
    }
}
