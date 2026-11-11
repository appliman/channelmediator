using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Threading.Channels;

namespace ChannelMediator.Tests;

[CollectionDefinition("Mediator metrics", DisableParallelization = true)]
public class MediatorMetricsCollection;

[Collection("Mediator metrics")]
public class MediatorMetricsTests
{
    [Fact]
    public async Task WhenRequestsFinishOrFail_ThenMetricsAreBalancedAndDurationsRecorded()
    {
        using var measurements = new Measurements();
        await using var provider = CreateProvider();
        var mediator = (Mediator)provider.GetRequiredService<IMediator>();
        await mediator.Send(new MeasuredRequest());
        await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Send(new MeasuredRequest(Fail: true)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Send(new MissingRequest()));
        await mediator.DisposeAsync();

        Assert.Equal(0, measurements.Counters["channelmediator.requests.pending"]);
        Assert.Equal(0, measurements.Counters["channelmediator.requests.active"]);
        Assert.Equal(3, measurements.Histograms["channelmediator.requests.queue.duration"]);
        Assert.Equal(2, measurements.Histograms["channelmediator.requests.execution.duration"]);
    }

    [Fact]
    public async Task WhenShutdownRejectsPendingWork_ThenPendingMetricsReturnToZero()
    {
        using var measurements = new Measurements();
        await using var provider = CreateProvider();
        var mediator = (Mediator)provider.GetRequiredService<IMediator>();
        var head = new BackpressureTests.ControlledRequest();
        var active = mediator.Send(head);
        await head.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        var queued = mediator.Send(new MeasuredRequest(), cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        var waiting = mediator.Send(new MeasuredRequest());
        await mediator.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => active);
        await Assert.ThrowsAsync<ChannelClosedException>(() => waiting);

        Assert.Equal(0, measurements.Counters["channelmediator.requests.pending"]);
        Assert.Equal(0, measurements.Counters["channelmediator.requests.active"]);
        Assert.Equal(3, measurements.Histograms["channelmediator.requests.queue.duration"]);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddChannelMediator(c => c.QueueCapacity = 1, typeof(MeasuredHandler).Assembly);
        var result = services.BuildServiceProvider();
        return result;
    }

    public sealed record MeasuredRequest(bool Fail = false) : IRequest<int>;
    public sealed record MissingRequest : IRequest<int>;
    public sealed class MeasuredHandler : IRequestHandler<MeasuredRequest, int>
    {
        public Task<int> Handle(MeasuredRequest request, CancellationToken cancellationToken)
        {
            if (request.Fail)
            {
                throw new InvalidOperationException("Test failure");
            }
            var result = Task.FromResult(42);
            return result;
        }
    }

    private sealed class Measurements : IDisposable
    {
        private readonly MeterListener _listener = new();
        public ConcurrentDictionary<string, long> Counters { get; } = new();
        public ConcurrentDictionary<string, int> Histograms { get; } = new();

        public Measurements()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "ChannelMediator")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
                Counters.AddOrUpdate(instrument.Name, value, (_, previous) => previous + value));
            _listener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
            {
                Assert.True(value >= 0);
                Histograms.AddOrUpdate(instrument.Name, 1, (_, previous) => previous + 1);
            });
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }
}
