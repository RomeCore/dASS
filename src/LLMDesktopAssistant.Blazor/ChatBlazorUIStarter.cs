using LLMDesktopAssistant.Blazor.Services;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Users;
using LLMDesktopAssistant.Utils;
using Microsoft.AspNetCore.Components.Authorization;
using Serilog;

namespace LLMDesktopAssistant.Blazor
{
	[ChatService(typeof(IChatWebUIStarter))]
	public class ChatBlazorUIStarter(
		IServiceProvider chatServices
	) : NotifyPropertyChanged, IChatWebUIStarter
	{
		private WebApplication? _webApp;
		private SemaphoreSlim _stateSemaphore = new SemaphoreSlim(1, 1);

		private bool _isRunning = false;
		public bool IsRunning
		{
			get => _isRunning;
			private set => SetProperty(ref _isRunning, value);
		}

		public void Start(WebUIStartupSettings settings)
		{
			if (IsRunning)
				throw new InvalidOperationException("Blazor Chat UI is already running.");
			if (_stateSemaphore.CurrentCount == 0)
				throw new InvalidOperationException("Blazor UI is already starting or stopping.");
			_stateSemaphore.Wait();

			Log.Information("Starting Blazor Chat UI...");

			try
			{
				var builder = WebApplication.CreateBuilder(new WebApplicationOptions
				{
					WebRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
					ContentRootPath = Directory.GetCurrentDirectory(),
#if DEBUG
					EnvironmentName = Environments.Development,
#else
					EnvironmentName = Environments.Production,
#endif
				});

				// Add existing services
				var services = chatServices;
				var chatServiceCollection = services.GetRequiredKeyedService<IServiceCollection>(ServiceKeys.ChatServices);
				var allServices = chatServiceCollection
					.Where(s => s.Lifetime is not ServiceLifetime.Transient && !s.ServiceType.IsGenericTypeDefinition)
					.SelectMany(s => chatServices.GetServices(s.ServiceType)
						.Select(si => (s.ServiceType, si)))
					.Distinct()
					.ToList();

				foreach (var (serviceType, instance) in allServices)
					if (instance != null)
						builder.Services.AddSingleton(serviceType, instance);

				foreach (var serviceDescriptor in chatServiceCollection)
					if (serviceDescriptor.Lifetime is ServiceLifetime.Transient || serviceDescriptor.ServiceType.IsGenericTypeDefinition)
						builder.Services.Add(serviceDescriptor);

				// Add startup settings
				builder.Services.AddSingleton(settings);

				builder.Services.AddServerSideBlazor(options =>
				{
					options.DisconnectedCircuitRetentionPeriod = WebUIStaticConfiguration.DisconnectTimeout;
					options.JSInteropDefaultCallTimeout = WebUIStaticConfiguration.DisconnectTimeout;
				});

				// Add services to the container.
				builder.Services.AddRazorComponents()
					.AddInteractiveServerComponents();

				builder.Services.AddControllers()
					.AddApplicationPart(typeof(ChatBlazorUIStarter).Assembly);

				builder.Services.AddHttpClient();
				builder.Services.AddHttpContextAccessor();

				// Register WebUI authentication services
				builder.Services.AddAuthorization();
				builder.Services.AddScoped<WebUIAuthenticationStateProvider>();
				builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
					sp.GetRequiredService<WebUIAuthenticationStateProvider>());

				// Register WebUI-scoped App services
				foreach (var configurator in ReflectionUtility.GetTypesWithAttribute<ServiceConfigurator, ServiceConfiguratorAttribute>())
				{
					if (configurator.Attribute.Scope == ServiceScope.WebUI)
						configurator.Type.Instantiate<ServiceConfigurator>().Configure(builder.Services);
				}

				// Add Cookie authentication scheme
				var authBuilder = builder.Services.AddAuthentication(opts =>
				{

				})
					.AddCookie(WebUIStaticConfiguration.CookiesScheme, options =>
					{
						options.LoginPath = "/login";
						options.ExpireTimeSpan = WebUIStaticConfiguration.LoginExpiryTimeSpan;
						options.SlidingExpiration = true;
					});

				// builder.Services.AddCascadingAuthenticationState();

				var app = builder.Build();
				_webApp = app;

				// Configure the HTTP request pipeline.
				if (!app.Environment.IsDevelopment())
				{
					app.UseExceptionHandler("/Error");
					app.UseHsts();
				}

				app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

				app.UseAuthentication();
				app.UseAuthorization();

				app.UseAntiforgery();

				app.MapStaticAssets("LLMDesktopAssistant.Blazor.staticwebassets.endpoints.json");

				app.MapControllers();

				app.MapRazorComponents<Components.App>()
					.AddInteractiveServerRenderMode();

				app.RunAsync(settings.EndpointUrl);

				// Activate the services
				app.Services.GetRequiredService<IGenerationReadinessService>();

				IsRunning = true;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to start Blazor Chat UI.");
			}
			finally
			{
				_stateSemaphore.Release();
			}
		}

		public void Stop()
		{
			if (!IsRunning)
				throw new InvalidOperationException("Blazor Chat UI is not running.");
			if (_stateSemaphore.CurrentCount == 0)
				throw new InvalidOperationException("Blazor Chat UI is already starting or stopping.");
			_stateSemaphore.Wait();

			Log.Information("Stopping Blazor Chat UI...");

			try
			{
				_webApp?.StopAsync();
				_webApp = null;
				IsRunning = false;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to stop Blazor Chat UI.");
			}
			finally
			{
				_stateSemaphore.Release();
			}
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing && _webApp != null)
				Stop();
		}
	}
}
