namespace ChannelMediatorMinimalApiSample.Handlers;

public class MySecondHandler
	: IRequestHandler<MySecondRequest, MySecondResult>
{
	public Task<MySecondResult> Handle(MySecondRequest request, CancellationToken cancellationToken)
	{
		var result = new MySecondResult();
		result.Name = request.Name.ToUpper();
		return Task.FromResult(result);
	}
}
