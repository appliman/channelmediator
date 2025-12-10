namespace ChannelMediator;

public interface IRequestHandlerWrapper
{
	Type RequestType { get; }

	ValueTask<object> HandleAsync(object request, CancellationToken cancellationToken);
}
