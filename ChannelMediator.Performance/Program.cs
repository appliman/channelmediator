using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ChannelMediator;
using Microsoft.Extensions.DependencyInjection;

// Define a simple request/response types local to this program
public record PerfRequest(byte[] Payload) : IRequest<PerfResponse>;
public record PerfResponse(int Length);

// Handler
public class PerfHandler : IRequestHandler<PerfRequest, PerfResponse>
{
    public ValueTask<PerfResponse> HandleAsync(PerfRequest request, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new PerfResponse(request.Payload.Length));
    }
}

[MemoryDiagnoser]
public class MediatorBenchmarks
{
    private IMediator _mediator = null!;
    private int _messageSize = 256;
    private int _messageCount = 1000;
    private int _concurrentSenders = 8;

    [Params(64, 256, 1024)]
    public int MessageSizeParam { get; set; }

    [Params(1, 8)]
    public int ConcurrencyParam { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _messageSize = MessageSizeParam;
        _concurrentSenders = ConcurrencyParam;

        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(MediatorBenchmarks).Assembly);
        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();

        // Register handler type in the scanned assemblies so AddChannelMediator can find it
        // (handlers in this assembly will be discovered by AddChannelMediator during registration)
    }

    [Benchmark]
    public async Task InvokeManyAsync()
    {
        int perSender = _messageCount / _concurrentSenders;
        var tasks = new List<Task>();
        for (int s = 0; s < _concurrentSenders; s++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < perSender; i++)
                {
                    var payload = new byte[_messageSize];
                    var request = new PerfRequest(payload);
                    var res = await _mediator.InvokeAsync(request).ConfigureAwait(false);
                    if (res.Length != _messageSize) throw new InvalidOperationException("Invalid response");
                }
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<MediatorBenchmarks>();
    }
}
