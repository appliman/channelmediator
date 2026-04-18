namespace ChannelMediatorMinimalApiSample.Handlers;

public class MyHandler
	: IRequestHandler<MyFirstRequest, MyFirstResult>
{
	public Task<MyFirstResult> Handle(MyFirstRequest request, CancellationToken cancellationToken)
	{
		var result = new MyFirstResult();
		result.Value = request.Value * 2;
		return Task.FromResult(result);
	}
}
