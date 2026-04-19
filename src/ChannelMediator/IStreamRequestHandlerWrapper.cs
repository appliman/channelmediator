namespace ChannelMediator;

internal interface IStreamRequestHandlerWrapper
{
	Type RequestType { get; }

	IAsyncEnumerable<object?> HandleAsync(object request, CancellationToken cancellationToken);
}
