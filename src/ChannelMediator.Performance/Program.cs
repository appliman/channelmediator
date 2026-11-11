using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ChannelMediator;
using Microsoft.Extensions.DependencyInjection;

[MemoryDiagnoser]
public class MediatorBenchmarks
{
    private const int BatchSize = 256;
    private readonly PerfRequest _request = new();
    private readonly Task<PerfResponse>[] _pending = new Task<PerfResponse>[BatchSize];
    private ServiceProvider _provider = null!;
    private ServiceProvider _behaviorProvider = null!;
    private IMediator _mediator = null!;
    private IMediator _behaviorMediator = null!;
    private CancellationTokenSource _cancellation = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(MediatorBenchmarks).Assembly);
        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();

        var behaviorServices = new ServiceCollection();
        behaviorServices.AddChannelMediator(null, typeof(MediatorBenchmarks).Assembly);
        behaviorServices.AddScoped<IPipelineBehavior<PerfRequest, PerfResponse>, PerfBehavior>();
        _behaviorProvider = behaviorServices.BuildServiceProvider();
        _behaviorMediator = _behaviorProvider.GetRequiredService<IMediator>();
        _cancellation = new CancellationTokenSource();
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _behaviorProvider.DisposeAsync();
        await _provider.DisposeAsync();
        _cancellation.Dispose();
    }

    // Requests and responses are cached to isolate mediator overhead from payload allocation.
    [Benchmark(OperationsPerInvoke = BatchSize)]
    public async Task SequentialAsync()
    {
        for (var i = 0; i < BatchSize; i++)
        {
            await _mediator.Send(_request);
        }
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public async Task WithBehaviorAsync()
    {
        for (var i = 0; i < BatchSize; i++)
        {
            await _behaviorMediator.Send(_request);
        }
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public async Task WithCancellationTokenAsync()
    {
        for (var i = 0; i < BatchSize; i++)
        {
            await _mediator.Send(_request, _cancellation.Token);
        }
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public async Task BurstAsync()
    {
        for (var i = 0; i < BatchSize; i++)
        {
            _pending[i] = _mediator.Send(_request);
        }

        await Task.WhenAll(_pending);
    }
}

public sealed record PerfRequest : IRequest<PerfResponse>;
public sealed record PerfResponse;

public sealed class PerfHandler : IRequestHandler<PerfRequest, PerfResponse>
{
    private static readonly Task<PerfResponse> Response = Task.FromResult(new PerfResponse());

    public Task<PerfResponse> Handle(PerfRequest request, CancellationToken cancellationToken)
    {
        return Response;
    }
}

public sealed class PerfBehavior : IPipelineBehavior<PerfRequest, PerfResponse>
{
    public ValueTask<PerfResponse> HandleAsync(PerfRequest request, RequestHandlerDelegate<PerfResponse> next, CancellationToken cancellationToken)
    {
        var result = next();
        return result;
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
