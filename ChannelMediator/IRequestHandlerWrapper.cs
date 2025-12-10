namespace ChannelMediator;

internal interface IRequestHandlerWrapper
{
	Type RequestType { get; }

	ValueTask<object> HandleAsync(object request, CancellationToken cancellationToken);
}
