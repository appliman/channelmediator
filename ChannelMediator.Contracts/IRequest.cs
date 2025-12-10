namespace ChannelMediator.Contracts;

public interface IRequest<out TResponse>
{
}

/// <summary>
/// Marker interface for requests that don't return a value (commands).
/// </summary>
public interface IRequest : IRequest<Unit>
{
}
