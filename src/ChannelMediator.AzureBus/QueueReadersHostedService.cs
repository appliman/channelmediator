using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChannelMediator.AzureBus;

internal sealed class QueueReadersHostedService : IHostedService, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<QueueReader> _readers = new();
    private readonly ILogger _logger;

    private bool _disposed;
    public QueueReadersHostedService(
        IServiceProvider serviceProvider, 
        ILogger<QueueReadersHostedService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var client = _serviceProvider.GetRequiredService<Azure.Messaging.ServiceBus.ServiceBusClient>();
        var entityManager = _serviceProvider.GetRequiredService<AzureServiceBusEntityManager>();

        foreach (var options in QueueReaderRegistry.GetRegisteredOptions())
        {
            var reader = new QueueReader(client, entityManager, options, _serviceProvider, _logger);
            _readers.Add(reader);
            await reader.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var reader in _readers)
        {
            await reader.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        foreach (var reader in _readers)
        {
            await reader.DisposeAsync().ConfigureAwait(false);
        }
    }


}
