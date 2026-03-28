using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

namespace ChannelMediator.RabbitMQ;

/// <summary>
/// Manages RabbitMQ entities (exchanges, queues, bindings) creation and existence verification.
/// </summary>
internal sealed class RabbitMqEntityManager
{
    private readonly IConnection _connection;
    private readonly ILogger<RabbitMqEntityManager> _logger;
    private readonly ConcurrentDictionary<string, bool> _exchangesExist = new();
    private readonly ConcurrentDictionary<string, bool> _queuesExist = new();
    private readonly ConcurrentDictionary<string, bool> _bindingsExist = new();

    public RabbitMqEntityManager(IConnection connection, ILogger<RabbitMqEntityManager> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Ensures that a fanout exchange exists, creating it if necessary.
    /// </summary>
    /// <param name="exchangeName">The name of the exchange.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task EnsureExchangeExistsAsync(string exchangeName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeName);

        if (_exchangesExist.ContainsKey(exchangeName))
        {
            return;
        }

        try
        {
            await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            await channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: ExchangeType.Fanout,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Exchange '{ExchangeName}' declared (fanout, durable).", exchangeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring exchange '{ExchangeName}' exists.", exchangeName);
            throw;
        }

        _exchangesExist.TryAdd(exchangeName, true);
    }

    /// <summary>
    /// Ensures that a queue exists, creating it if necessary.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task EnsureQueueExistsAsync(string queueName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        if (_queuesExist.ContainsKey(queueName))
        {
            return;
        }

        try
        {
            await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Queue '{QueueName}' declared (durable).", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring queue '{QueueName}' exists.", queueName);
            throw;
        }

        _queuesExist.TryAdd(queueName, true);
    }

    /// <summary>
    /// Ensures that a queue is bound to an exchange with the given routing key.
    /// </summary>
    /// <param name="exchangeName">The exchange name.</param>
    /// <param name="queueName">The queue name.</param>
    /// <param name="routingKey">The routing key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task EnsureBindingExistsAsync(
        string exchangeName,
        string queueName,
        string routingKey = "",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var bindingKey = $"{exchangeName}::{queueName}::{routingKey}";

        if (_bindingsExist.ContainsKey(bindingKey))
        {
            return;
        }

        try
        {
            await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            await channel.QueueBindAsync(
                queue: queueName,
                exchange: exchangeName,
                routingKey: routingKey,
                arguments: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Queue '{QueueName}' bound to exchange '{ExchangeName}' with routing key '{RoutingKey}'.",
                queueName, exchangeName, routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error binding queue '{QueueName}' to exchange '{ExchangeName}'.", queueName, exchangeName);
            throw;
        }

        _bindingsExist.TryAdd(bindingKey, true);
    }
}
