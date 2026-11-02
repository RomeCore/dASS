using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Startup;
using LLMDesktopAssistant.Startup.Stages;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace LLMDesktopAssistant
{
	public partial class App : Application
	{
		private static Action? _onFWIC; // Framework init completed
		private static bool _FWIC = false;
		private static Action? _onMainContentCreated;

		/// <summary>
		/// Gets the current version of the application.
		/// </summary>
		public static Version Version { get; } = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

		/// <summary>
		/// Adds the services the application needs on top of the discovered ones.
		/// </summary>
		/// <param name="services">The collection to fill.</param>
		protected virtual void ConfigureServices(IServiceCollection services)
		{
			services.AddSingleton(sp => Log.Logger);
			services.AddLogging(l => l.AddProvider(new SerilogLoggerProvider(Log.Logger)));
			services.AddTransient(sp =>
			{
				var factory = sp.GetRequiredService<ILoggerFactory>();
				return factory.CreateLogger("Uncategorized");
			});
		}

		/// <summary>
		/// Exposes <see cref="ConfigureServices"/> to the startup stage without making it public.
		/// </summary>
		/// <param name="services">The collection to fill.</param>
		internal void ConfigureServicesForStartup(IServiceCollection services) => ConfigureServices(services);

		/// <inheritdoc />
		public override void Initialize()
		{
			if (Design.IsDesignMode)
			{
				AvaloniaXamlLoader.Load(this);
				return;
			}

			// Directories, the logger and the localization singleton are already configured by
			// SharedInitialization; the reflection scan and the container are startup stages.
			AvaloniaXamlLoader.Load(this);
		}

		public static MainView? MainView { get; private set; }
		public static MainWindow? MainWindow { get; private set; }

		private static TopLevel? _mainTopLevel;
		public static TopLevel MainTopLevel => _mainTopLevel ??= TopLevel.GetTopLevel((Control?)MainWindow ?? MainView)!;

		/// <inheritdoc />
		public override void OnFrameworkInitializationCompleted()
		{
			var splash = CreateSplashContent();
			if (splash is not null)
				StartupSplashManager.Attach(splash);

			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				// The splash becomes the main window first, otherwise closing it would end
				// the application before the real window exists.
				if (splash is Window splashWindow)
				{
					desktop.MainWindow = splashWindow;
					splashWindow.Show();
				}
			}
			else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
			{
				if (splash is Control splashView)
					singleViewPlatform.MainView = splashView;
			}

			base.OnFrameworkInitializationCompleted();

			_ = RunStartupAsync();

			_FWIC = true;
			_onFWIC?.Invoke();
			_onFWIC = null;
		}

		/// <summary>
		/// Creates the splash content for the current lifetime, or <see langword="null"/> when the
		/// splash is disabled.
		/// </summary>
		/// <returns>A window on the desktop, a control in single-view mode, or <see langword="null"/>.</returns>
		protected virtual IStartupSplashHandle? CreateSplashContent() => null;

		/// <summary>
		/// Creates the stages of this host. The platform stages come first so that anything they
		/// register is visible to the assembly scan.
		/// </summary>
		/// <returns>The ordered stages.</returns>
		protected virtual IReadOnlyList<IStartupStage> CreateStartupStages()
		{
			return
			[
				.. SharedInitialization.Platform.CreatePlatformStages(),
				new ReflectionScanStage(),
				new ServiceRegistryStage(),
				new ChatServicesStage(),
			];
		}

		/// <summary>
		/// Creates the main view model and puts it on screen. Runs on the UI thread and returns only
		/// after the content has been rendered, so the splash never disappears into an empty window.
		/// </summary>
		protected virtual async Task ShowMainContentAsync()
		{
			var viewModel = new MainViewModel();

			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				var window = new MainWindow { DataContext = viewModel };
				MainWindowPlacement.Restore(window);
				MainWindowPlacement.Attach(window);

				var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

				// LayoutUpdated is the first point where the window content actually exists on screen;
				// Opened fires before that and would let the splash disappear into an empty frame.
				void OnRendered(object? sender, EventArgs e)
				{
					window.LayoutUpdated -= OnRendered;
					rendered.TrySetResult();
				}

				window.LayoutUpdated += OnRendered;

				MainWindow = window;
				desktop.MainWindow = window;
				window.Show();

				RaiseMainContentCreated();

				await rendered.Task;
			}
			else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
			{
				var view = new MainView { DataContext = viewModel };
				MainView = view;
				singleViewPlatform.MainView = view;

				RaiseMainContentCreated();
			}
		}

		/// <summary>
		/// Runs the whole startup: the stages, then the main content, then the splash fade-out.
		/// </summary>
		private async Task RunStartupAsync()
		{
			var stages = CreateStartupStages();
			var totalStages = stages.Count + 1; // the last step shows the main content

			try
			{
				StartupSplashManager.Begin(totalStages);

				await new StartupPipeline(stages).RunAsync().ConfigureAwait(false);

				StartupSplashManager.SetStage(stages.Count, Locale.GetKey("splash.stage.main_window"));

				await Dispatcher.UIThread.InvokeAsync(ShowMainContentAsync).ConfigureAwait(false);

				StartupSplashManager.SetStageProgress(1, Locale.GetKey("splash.stage.main_window"));

				// A splash that flashes for eighty milliseconds reads as a glitch, not as a splash.
				if (StartupSplashManager.IsAttached)
					await Task.Delay(MinimumSplashDuration).ConfigureAwait(false);

				StartupSplashManager.Complete();
			}
			catch (Exception exception)
			{
				Log.Fatal(exception, "Startup failed: {Message}", exception.Message);
				StartupSplashManager.Fail(exception);
			}
		}

		/// <summary>
		/// The shortest time the splash stays on screen, so it never looks like a flicker.
		/// </summary>
		private static readonly TimeSpan MinimumSplashDuration = TimeSpan.FromMilliseconds(200);

		/// <summary>
		/// Runs an action once the main window or view exists, immediately if it already does.
		/// Callbacks registered through <see cref="OnFrameworkInitializationCompleted"/> run too
		/// early for that: the startup stages have not produced the main content yet.
		/// </summary>
		/// <param name="action">The action to run.</param>
		public static void OnMainContentCreated(Action action)
		{
			if (MainWindow is not null || MainView is not null)
			{
				action();
				return;
			}

			_onMainContentCreated += action;
		}

		/// <summary>
		/// Runs the callbacks waiting for the main content and forgets them.
		/// </summary>
		private static void RaiseMainContentCreated()
		{
			var callbacks = _onMainContentCreated;
			_onMainContentCreated = null;
			callbacks?.Invoke();
		}

		/// <summary>
		/// Runs an action once the framework initialization has completed, immediately if it
		/// already has.
		/// </summary>
		/// <param name="action">The action to run.</param>
		public static void OnFrameworkInitializationCompleted(Action action)
		{
			if (_FWIC)
			{
				action();
				return;
			}

			_onFWIC += action;
		}
	}
}
