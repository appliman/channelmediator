namespace ChannelMediator.RabbitMQ;

/// <summary>
/// Defines the processing mode for RabbitMQ integration.
/// </summary>
public enum RabbitMqMode
{
    /// <summary>
    /// Live mode connects to a real RabbitMQ broker.
    /// </summary>
    Live,

    /// <summary>
    /// Mock mode routes messages in-process via the local mediator.
    /// </summary>
    Mock
}
