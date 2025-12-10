using System.Diagnostics;
using ChannelMediator;
using ChannelMediator.Contracts;

namespace ChannelMediatorConsole;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
	where TRequest : IRequest<TResponse>
{
	public async ValueTask<TResponse> HandleAsync(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken)
	{
		var requestName = typeof(TRequest).Name;
		Console.WriteLine($"[BEHAVIOR] Handling {requestName}...");

		var sw = Stopwatch.StartNew();

		try
		{
			var response = await next();
			sw.Stop();

			Console.WriteLine($"[BEHAVIOR] {requestName} handled successfully in {sw.ElapsedMilliseconds}ms");
			return response;
		}
		catch (Exception ex)
		{
			sw.Stop();
			Console.WriteLine($"[BEHAVIOR] {requestName} failed after {sw.ElapsedMilliseconds}ms: {ex.Message}");
			throw;
		}
	}
}

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
	where TRequest : IRequest<TResponse>
{
	public async ValueTask<TResponse> HandleAsync(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken)
	{
		Console.WriteLine($"[VALIDATION] Validating {typeof(TRequest).Name}...");

		if (request is AddToCartRequest addToCart && string.IsNullOrWhiteSpace(addToCart.ProductCode))
		{
			throw new ArgumentException("ProductCode cannot be empty", nameof(addToCart.ProductCode));
		}

		Console.WriteLine($"[VALIDATION] {typeof(TRequest).Name} is valid");

		return await next();
	}
}

/// <summary>
/// Global behavior that tracks performance metrics for all requests.
/// This behavior is applied to ALL request handlers automatically.
/// </summary>
public class PerformanceMonitoringBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>, IPipelineBehavior
	where TRequest : IRequest<TResponse>
{
	public async ValueTask<TResponse> HandleAsync(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken)
	{
		var requestName = typeof(TRequest).Name;
		var startTime = DateTime.UtcNow;
		
		Console.WriteLine($"[PERF-MONITOR] Request {requestName} started at {startTime:HH:mm:ss.fff}");

		try
		{
			var response = await next();
			var duration = DateTime.UtcNow - startTime;
			
			var emoji = duration.TotalMilliseconds switch
			{
				< 50 => "🚀",
				< 100 => "✅",
				< 500 => "⚠️",
				_ => "🐌"
			};

			Console.WriteLine($"[PERF-MONITOR] {emoji} Request {requestName} completed in {duration.TotalMilliseconds:F2}ms");
			
			return response;
		}
		catch (Exception ex)
		{
			var duration = DateTime.UtcNow - startTime;
			Console.WriteLine($"[PERF-MONITOR] ❌ Request {requestName} failed after {duration.TotalMilliseconds:F2}ms: {ex.Message}");
			throw;
		}
	}
}

/// <summary>
/// Global behavior that adds correlation ID tracking for all requests.
/// </summary>
public class CorrelationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>, IPipelineBehavior
	where TRequest : IRequest<TResponse>
{
	public async ValueTask<TResponse> HandleAsync(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString("N")[..8];
		Console.WriteLine($"[CORRELATION] [{correlationId}] Processing {typeof(TRequest).Name}");

		try
		{
			var response = await next();
			Console.WriteLine($"[CORRELATION] [{correlationId}] Completed successfully");
			return response;
		}
		catch (Exception)
		{
			Console.WriteLine($"[CORRELATION] [{correlationId}] Failed with error");
			throw;
		}
	}
}
