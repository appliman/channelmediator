namespace ChannelMediator;

/// <summary>
/// Represents a request that returns a response value.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the request.</typeparam>
public interface IRequest<out TResponse>
{
}

/// <summary>
/// Marker interface for requests that don't return a value (commands).
/// </summary>
public interface IRequest : IRequest<Unit>
{
}

/// <summary>
/// Represents a streaming request that returns an asynchronous sequence of response values.
/// </summary>
/// <typeparam name="TResponse">The type of each item yielded by the stream.</typeparam>
public interface IStreamRequest<out TResponse>
{
}
