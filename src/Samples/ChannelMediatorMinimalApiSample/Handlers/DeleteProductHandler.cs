namespace ChannelMediatorMinimalApiSample.Handlers;

public class DeleteProductHandler : IRequestHandler<DeleteProductRequest, bool>
{
    public Task<bool> Handle(DeleteProductRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }
}
