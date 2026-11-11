namespace ChannelMediator;

internal interface IRequestEnvelope
{
	ValueTask DispatchAsync(FrozenDictionary<System.Type, IRequestHandlerWrapper> handlers, CancellationToken dispatcherToken);

	void Cancel(CancellationToken cancellationToken);
}
