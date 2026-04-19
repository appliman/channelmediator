using System.Diagnostics;
using System.Runtime.CompilerServices;

using ChannelMediator;

namespace ChannelMediatorSampleConsole;

// ── Domain types ────────────────────────────────────────────────────────────

/// <summary>A single item in an order, returned one by one through a stream.</summary>
public record OrderItem(int Index, string ProductCode, decimal Price);

// ── Stream request ───────────────────────────────────────────────────────────

/// <summary>
/// Streams <see cref="Count"/> order items one at a time.
/// Implements <see cref="IStreamRequest{TResponse}"/> instead of
/// <see cref="IRequest{TResponse}"/> so the mediator uses <see cref="IMediator.CreateStream{TResponse}"/>.
/// </summary>
public record StreamOrderItemsRequest(int Count) : IStreamRequest<OrderItem>;

// ── Handler ──────────────────────────────────────────────────────────────────

public class StreamOrderItemsHandler : IStreamRequestHandler<StreamOrderItemsRequest, OrderItem>
{
	private static readonly string[] Products =
	[
		"laptop-pro",
		"mouse-elite",
		"keyboard-mx",
		"monitor-4k",
		"headset-pro"
	];

	public async IAsyncEnumerable<OrderItem> Handle(
		StreamOrderItemsRequest request,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		for (int i = 1; i <= request.Count; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// Simulate fetching one row from a DB / external service
			await Task.Delay(50, cancellationToken);

			yield return new OrderItem(
				Index: i,
				ProductCode: Products[(i - 1) % Products.Length],
				Price: Math.Round(9.99m * i, 2));
		}
	}
}

// ── Stream pipeline behavior ─────────────────────────────────────────────────

/// <summary>
/// Demonstrates an <see cref="IStreamPipelineBehavior{TRequest,TResponse}"/>:
/// logs stream start/end and measures total elapsed time.
/// </summary>
public class StreamTimingBehavior : IStreamPipelineBehavior<StreamOrderItemsRequest, OrderItem>
{
	public async IAsyncEnumerable<OrderItem> Handle(
		StreamOrderItemsRequest request,
		StreamHandlerDelegate<OrderItem> next,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var sw = Stopwatch.StartNew();
		int count = 0;

		Console.WriteLine($"[STREAM-BEHAVIOR] Stream started (expecting {request.Count} items)");

		await foreach (var item in next().WithCancellation(cancellationToken))
		{
			count++;
			yield return item;
		}

		sw.Stop();
		Console.WriteLine($"[STREAM-BEHAVIOR] Stream ended — {count} items in {sw.ElapsedMilliseconds} ms");
	}
}
