using Microsoft.Extensions.Hosting;

namespace ChannelMediator.AzureBus;

/// <summary>
/// Background service that manages the lifecycle of a <see cref="TopicSubscriptionReader"/>.
/// </summary>
internal sealed class TopicSubscriptionReaderHostedService : IHostedService, IAsyncDisposable
{
    private readonly TopicSubscriptionReader _reader;

    /// <summary>
    /// Initializes a new instance of the <see cref="TopicSubscriptionReaderHostedService"/> class.
    /// </summary>
    /// <param name="reader">The topic subscription reader.</param>
    public TopicSubscriptionReaderHostedService(TopicSubscriptionReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _reader.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _reader.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _reader.DisposeAsync().ConfigureAwait(false);
    }
}
