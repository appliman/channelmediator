using System.Reflection;

namespace ChannelMediator;

/// <summary>
/// Provides dependency injection extensions for registering ChannelMediator services.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Adds ChannelMediator services and scans the supplied assemblies for handlers.
	/// </summary>
	/// <param name="services">The service collection to update.</param>
	/// <param name="configureNotifications">An optional action used to configure mediator registration.</param>
	/// <param name="assemblies">The assemblies to scan for handlers. When omitted, the calling assembly is used.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
	public static IServiceCollection AddChannelMediator(
		this IServiceCollection services,
		Action<ChannelMediatorConfiguration>? configureNotifications = null,
		params Assembly[] assemblies)
	{
		var assembliesToScan = assemblies.Length > 0
			? assemblies
			: new[] { Assembly.GetCallingAssembly() };

		var notificationConfig = new ChannelMediatorConfiguration();
		// Ensure Services is available to the configuration action so it can register services (e.g., Azure Service Bus)
		notificationConfig.Services = services;
		configureNotifications?.Invoke(notificationConfig);

		RegisterHandlers(services, assembliesToScan);
		RegisterNotificationHandlers(services, assembliesToScan);

		// Register the factory first (singleton) - shared configuration
		services.AddSingleton<IMediatorFactory>(sp =>
		{
			var wrappers = sp.GetServices<IRequestHandlerWrapper>();
			var handlers = wrappers.ToDictionary(w => w.RequestType);

			var notificationWrappers = sp.GetServices<INotificationHandlerWrapper>();
			var notificationHandlers = notificationWrappers.ToDictionary(w => w.NotificationType);

			return new MediatorFactory(handlers, notificationHandlers, sp, notificationConfig);
		});

		// Register IMediator using the factory (transient to avoid deadlocks with nested calls)
		services.AddTransient(sp =>
		{
			var factory = sp.GetRequiredService<IMediatorFactory>();
			return factory.CreateMediator();
		});

		return services;
	}

	/// <summary>
	/// Registers a request handler and its wrapper for mediator dispatch.
	/// </summary>
	/// <typeparam name="TRequest">The request type handled by the registration.</typeparam>
	/// <typeparam name="TResponse">The response type produced by the handler.</typeparam>
	/// <typeparam name="THandler">The concrete handler implementation.</typeparam>
	/// <param name="services">The service collection to update.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
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
	/// Example: <c>typeof(LoggingBehavior&lt;,&gt;)</c>
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
		var commandHandlerInterfaceType = typeof(IRequestHandler<>);
		var registeredRequestTypes = new HashSet<Type>();

		foreach (var assembly in assemblies)
		{
			var handlerTypes = assembly.GetTypes()
				.Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition)
				.Where(t => t.GetInterfaces().Any(i =>
					(i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterfaceType) ||
					(i.IsGenericType && i.GetGenericTypeDefinition() == commandHandlerInterfaceType)))
				.ToList();

			foreach (var handlerType in handlerTypes)
			{
				// Register IRequestHandler<TRequest, TResponse> handlers
				var handlerInterfaces = handlerType.GetInterfaces()
					.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterfaceType)
					.ToList();

				foreach (var handlerInterface in handlerInterfaces)
				{
					var genericArgs = handlerInterface.GetGenericArguments();
					var requestType = genericArgs[0];
					var responseType = genericArgs[1];

					if (!registeredRequestTypes.Add(requestType) || services.Any(sd => sd.ServiceType == handlerInterface))
					{
						continue;
					}

					services.AddScoped(handlerInterface, handlerType);

					var wrapperType = typeof(RequestHandlerWrapper<,>).MakeGenericType(requestType, responseType);
					services.AddSingleton(typeof(IRequestHandlerWrapper), sp =>
					{
						return ActivatorUtilities.CreateInstance(sp, wrapperType);
					});
				}

				// Register IRequestHandler<TRequest> handlers (commands without response)
				var commandHandlerInterfaces = handlerType.GetInterfaces()
					.Where(i => i.IsGenericType &&
							   i.GetGenericTypeDefinition() == commandHandlerInterfaceType &&
							   !handlerInterfaces.Any(h => h.GetGenericArguments()[0] == i.GetGenericArguments()[0]))
					.ToList();

				foreach (var commandHandlerInterface in commandHandlerInterfaces)
				{
					var requestType = commandHandlerInterface.GetGenericArguments()[0];
					var responseType = typeof(Unit);

					if (!registeredRequestTypes.Add(requestType) || services.Any(sd => sd.ServiceType == commandHandlerInterface))
					{
						continue;
					}

					// Register the command handler interface
					services.AddScoped(commandHandlerInterface, handlerType);

					// Also register as IRequestHandler<TRequest, Unit> so the wrapper can resolve it directly
					var queryHandlerInterface = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
					services.AddScoped(queryHandlerInterface, sp => sp.GetRequiredService(commandHandlerInterface));

					// Create a wrapper that bridges IRequestHandler<TRequest> to IRequestHandler<TRequest, Unit>
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
