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
