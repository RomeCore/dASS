using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Utils;
using Serilog;

namespace LLMDesktopAssistant.Services
{
	/// <summary>
	/// The manager for application services. All services are statically registered and can be retrieved by their type.
	/// </summary>
	public static class ServiceRegistry
	{
		private static bool _initialized = false;
		private static IServiceProvider _serviceProvider = null!;
		private static readonly List<Action<IServiceProvider>> _onInitializedCallbacks = [];

		/// <summary>
		/// Gets the service provider for the application. This is used to resolve services by their type.
		/// </summary>
		public static IServiceProvider Provider => _serviceProvider;

		/// <summary>
		/// Registers a callback that runs as soon as the container exists: immediately when the
		/// registry is already initialized, otherwise once initialization completes.
		/// </summary>
		/// <param name="callback">The callback that receives the ready service provider.</param>
		public static void OnInitialized(Action<IServiceProvider> callback)
		{
			ArgumentNullException.ThrowIfNull(callback);

			lock (_onInitializedCallbacks)
			{
				if (_initialized)
				{
					callback(_serviceProvider);
					return;
				}

				_onInitializedCallbacks.Add(callback);
			}
		}

		/// <summary>
		/// Add services from <see cref="ServiceRegistry"/> to the provided collection.
		/// </summary>
		/// <param name="services">The collection of services to add.</param>
		/// <returns>The provided collection with services added.</returns>
		public static IServiceCollection AddAppServices(this IServiceCollection services)
		{
			CheckInitialized();

			var originalCollection = _serviceProvider.GetRequiredKeyedService<IServiceCollection>(ServiceKeys.AppServices);
			var singletonTypes = new HashSet<Type>();

			foreach (var service in originalCollection)
			{
				if (service.Lifetime is ServiceLifetime.Scoped)
					continue;
				if (service.Lifetime is ServiceLifetime.Transient || service.ServiceType.ContainsGenericParameters)
					services.Add(service);
				else if (singletonTypes.Add(service.ServiceType))
					foreach (var singletonService in Provider.GetServices(service.ServiceType))
						if (singletonService is not null)
							services.AddSingleton(service.ServiceType, singletonService);
			}

			return services;
		}

		private static void CheckInitialized()
		{
			if (!_initialized)
				throw new InvalidOperationException("ServiceRegistry is not initialized.");
		}

		/// <summary>
		/// Initializes all registered services and constructs them eagerly.
		/// </summary>
		/// <param name="services">The service instances to register first.</param>
		/// <param name="configureServices">The callback that adds the application services.</param>
		public static void Initialize(IEnumerable<object> services, Action<IServiceCollection> configureServices)
		{
			Build(services, configureServices);
			WarmUp();
		}

		/// <summary>
		/// Registers every discovered service and builds the container without constructing anything.
		/// </summary>
		/// <param name="services">The service instances to register first.</param>
		/// <param name="configureServices">The callback that adds the application services.</param>
		public static void Build(IEnumerable<object> services, Action<IServiceCollection> configureServices)
		{
			if (_initialized)
				throw new InvalidOperationException("ModuleManager is already initialized.");

			var collection = new ServiceCollection();

			collection.AddKeyedSingleton<IServiceCollection>(ServiceKeys.AppServices, collection);

			foreach (var service in services)
			{
				collection.AddSingleton(service);
			}

			configureServices?.Invoke(collection);

			foreach (var service in ReflectionUtility.GetTypesWithAttributes<object, ServiceAttribute>()
				.OrderBy(s => s.Attributes.OfType<ServiceAttribute>().Min(a => a.Order)))
			{
				foreach (var attribute in service.Attributes)
				{
					var serviceType = attribute.ServiceType ?? service.Type;
					collection.AddSingleton(serviceType, service.Type);
				}
			}

			foreach (var configurator in ReflectionUtility.GetTypesWithAttribute<ServiceConfigurator, ServiceConfiguratorAttribute>())
			{
				if (configurator.Attribute.Scope == ServiceScope.App)
					configurator.Type.Instantiate<ServiceConfigurator>().Configure(collection);
			}

			collection.DeduplicateServices();

			_serviceProvider = collection.BuildServiceProvider();
			_initialized = true;

			Log.Information("ServiceRegistry built with {Count} App services.", collection.Count);
		}

		/// <summary>
		/// Constructs every registered service in the order of their dependency weight, so the
		/// cheapest services are resolved first. Already constructed singletons are skipped by the
		/// container, which makes a second call harmless.
		/// </summary>
		/// <param name="onProgress">
		/// Called after each service with the descriptor, its zero-based position and the total count.
		/// </param>
		public static void WarmUp(Action<ServiceDescriptor, int, int>? onProgress = null)
		{
			CheckInitialized();

			var collection = _serviceProvider.GetRequiredKeyedService<IServiceCollection>(ServiceKeys.AppServices);
			var planned = collection
				.Where(descriptor => !descriptor.ServiceType.ContainsGenericParameters)
				.OrderBy(GetConstructorParameterCount)
				.ToList();

			for (var index = 0; index < planned.Count; index++)
			{
				var descriptor = planned[index];
				_serviceProvider.GetServices(descriptor.ServiceType);
				onProgress?.Invoke(descriptor, index, planned.Count);
			}

			Log.Information("ServiceRegistry warmed up {Count} App services.", planned.Count);

			Action<IServiceProvider>[] callbacks;

			lock (_onInitializedCallbacks)
			{
				callbacks = [.. _onInitializedCallbacks];
				_onInitializedCallbacks.Clear();
			}

			foreach (var callback in callbacks)
				callback(_serviceProvider);
		}

		/// <summary>
		/// The weight used to order the warm-up: the parameter count of the fattest constructor,
		/// because that is the one the container picks. Registrations without an implementation type
		/// (instances and factories) carry no visible dependencies and go last.
		/// </summary>
		private static int GetConstructorParameterCount(ServiceDescriptor descriptor)
		{
			var implementationType = descriptor.ImplementationType;

			if (implementationType is null)
				return int.MaxValue;

			return implementationType.GetConstructors()
				.Select(constructor => constructor.GetParameters().Length)
				.DefaultIfEmpty(0)
				.Max();
		}
	}
}
