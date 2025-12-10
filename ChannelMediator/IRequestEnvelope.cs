namespace ChannelMediator;

internal interface IRequestEnvelope
{
	ValueTask DispatchAsync(IReadOnlyDictionary<System.Type, IRequestHandlerWrapper> handlers, CancellationToken dispatcherToken);
}
