using System.Diagnostics.Metrics;

namespace ChannelMediator;

internal static class MediatorMetrics
{
	private static readonly Meter Meter = new("ChannelMediator");
	internal static readonly UpDownCounter<long> Pending = Meter.CreateUpDownCounter<long>(
		"channelmediator.requests.pending", "{request}", "Requests waiting for queue admission or execution.");
	internal static readonly UpDownCounter<long> Active = Meter.CreateUpDownCounter<long>(
		"channelmediator.requests.active", "{request}", "Handlers executing, including nested requests.");
	internal static readonly Histogram<double> QueueDuration = Meter.CreateHistogram<double>(
		"channelmediator.requests.queue.duration", "s", "Time waiting for admission and execution, including canceled requests.");
	internal static readonly Histogram<double> ExecutionDuration = Meter.CreateHistogram<double>(
		"channelmediator.requests.execution.duration", "s", "Handler and pipeline duration including scope disposal.");
}
