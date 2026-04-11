using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

namespace ChannelMediator.RabbitMQ;

/// <summary>
/// Background service that manages all registered queue readers.
/// </summary>
internal sealed class QueueReadersHostedService : IHostedService, IAsyncDisposable
{
	private readonly IServiceProvider _serviceProvider;
	private readonly List<QueueReader> _readers = [];
	private readonly ILogger _logger;
	private bool _disposed;

	public QueueReadersHostedService(
		IServiceProvider serviceProvider,
		ILogger<QueueReadersHostedService> logger)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		var options = _serviceProvider.GetRequiredService<RabbitMqOptions>();
		if (options.ProcessMode == RabbitMqMode.Mock)
		{
			return;
		}

		var connection = _serviceProvider.GetRequiredService<IConnection>();
		var entityManager = _serviceProvider.GetRequiredService<RabbitMqEntityManager>();

		foreach (var readerOptions in QueueReaderRegistry.GetRegisteredOptions())
		{
			var reader = new QueueReader(connection, entityManager, readerOptions, _serviceProvider, _logger);
			_readers.Add(reader);
			await reader.StartAsync(cancellationToken);
		}
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		foreach (var reader in _readers)
		{
			await reader.StopAsync(cancellationToken);
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		foreach (var reader in _readers)
		{
			await reader.DisposeAsync();
		}

		_readers.Clear();
	}
}
