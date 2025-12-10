using System.Reflection;

namespace ChannelMediator;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddChannelMediator(
		this IServiceCollection services,
		Action<NotificationPublisherConfiguration>? configureNotifications = null,
		params Assembly[] assemblies)
	{
		var assembliesToScan = assemblies.Length > 0
			? assemblies
			: new[] { Assembly.GetCallingAssembly() };

		var notificationConfig = new NotificationPublisherConfiguration();
		configureNotifications?.Invoke(notificationConfig);

		RegisterHandlers(services, assembliesToScan);
		RegisterNotificationHandlers(services, assembliesToScan);

		services.AddSingleton<IMediator>(sp =>
		{
			var wrappers = sp.GetServices<IRequestHandlerWrapper>();
			var handlers = wrappers.ToDictionary(w => w.RequestType);

			var notificationWrappers = sp.GetServices<INotificationHandlerWrapper>();
			var notificationHandlers = notificationWrappers.ToDictionary(w => w.NotificationType);

			return new ChannelMediator(handlers, notificationHandlers, sp, notificationConfig);
		});

		return services;
	}

	public static IServiceCollection AddRequestHandler<TRequest, TResponse, THandler>(this IServiceCollection services)
		where TRequest : IRequest<TResponse>
		where THandler : class, IRequestHandler<TRequest, TResponse>
	{
		services.AddScoped<IRequestHandler<TRequest, TResponse>, THandler>();
		services.AddSingleton<IRequestHandlerWrapper>(sp =>
			new RequestHandlerWrapper<TRequest, TResponse>(sp));
		return services;
	}

	/// <summary>
	/// Registers a pipeline behavior for a specific request type.
	/// </summary>
	public static IServiceCollection AddPipelineBehavior<TRequest, TResponse, TBehavior>(this IServiceCollection services)
		where TRequest : IRequest<TResponse>
		where TBehavior : class, IPipelineBehavior<TRequest, TResponse>
	{
		services.AddScoped<IPipelineBehavior<TRequest, TResponse>, TBehavior>();
		return services;
	}

	/// <summary>
	/// Registers a pipeline behavior for a specific request type using a Type.
	/// </summary>
	public static IServiceCollection AddPipelineBehavior(this IServiceCollection services, Type behaviorType)
	{
		services.AddScoped(typeof(IPipelineBehavior<,>), behaviorType);
		return services;
	}

	/// <summary>
	/// Registers a global pipeline behavior that will be applied to all request handlers.
	/// The behavior type must implement IPipelineBehavior marker interface and be an open generic type.
	/// Example: typeof(LoggingBehavior<,>)
	/// </summary>
	public static IServiceCollection AddOpenPipelineBehavior(this IServiceCollection services, Type behaviorType)
	{
		if (!behaviorType.IsGenericTypeDefinition)
		{
			throw new ArgumentException("Behavior type must be an open generic type (e.g., typeof(MyBehavior<,>))", nameof(behaviorType));
		}

		var genericInterface = behaviorType.GetInterfaces()
			.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));

		if (genericInterface == null && !typeof(IPipelineBehavior).IsAssignableFrom(behaviorType))
		{
			throw new ArgumentException("Behavior type must implement IPipelineBehavior<,> or IPipelineBehavior marker interface", nameof(behaviorType));
		}

		services.AddScoped(typeof(IPipelineBehavior<,>), behaviorType);
		return services;
	}

	private static void RegisterHandlers(IServiceCollection services, Assembly[] assemblies)
	{
		var handlerInterfaceType = typeof(IRequestHandler<,>);

		foreach (var assembly in assemblies)
		{
			var handlerTypes = assembly.GetTypes()
				.Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition)
				.Where(t => t.GetInterfaces().Any(i =>
					i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterfaceType))
				.ToList();

			foreach (var handlerType in handlerTypes)
			{
				var handlerInterfaces = handlerType.GetInterfaces()
					.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterfaceType)
					.ToList();

				foreach (var handlerInterface in handlerInterfaces)
				{
					var genericArgs = handlerInterface.GetGenericArguments();
					var requestType = genericArgs[0];
					var responseType = genericArgs[1];

					services.AddScoped(handlerInterface, handlerType);

					var wrapperType = typeof(RequestHandlerWrapper<,>).MakeGenericType(requestType, responseType);
					services.AddSingleton(typeof(IRequestHandlerWrapper), sp =>
					{
                        return ActivatorUtilities.CreateInstance(sp, wrapperType);
                    });
				}
			}
		}
	}

	private static void RegisterNotificationHandlers(IServiceCollection services, Assembly[] assemblies)
	{
		var notificationHandlerInterfaceType = typeof(INotificationHandler<>);
		var notificationTypes = new HashSet<Type>();

		foreach (var assembly in assemblies)
		{
			var handlerTypes = assembly.GetTypes()
				.Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition)
				.Where(t => t.GetInterfaces().Any(i =>
					i.IsGenericType && i.GetGenericTypeDefinition() == notificationHandlerInterfaceType))
				.ToList();

			foreach (var handlerType in handlerTypes)
			{
				var handlerInterfaces = handlerType.GetInterfaces()
					.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == notificationHandlerInterfaceType)
					.ToList();

				foreach (var handlerInterface in handlerInterfaces)
				{
					var notificationType = handlerInterface.GetGenericArguments()[0];
					notificationTypes.Add(notificationType);

					services.AddScoped(handlerInterface, handlerType);
				}
			}
		}

		foreach (var notificationType in notificationTypes)
		{
			var wrapperType = typeof(NotificationHandlerWrapper<>).MakeGenericType(notificationType);
			services.AddSingleton(typeof(INotificationHandlerWrapper), sp =>
			{
                return ActivatorUtilities.CreateInstance(sp, wrapperType);
            });
		}
	}
}
