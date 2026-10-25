using System.ComponentModel;
using System.IO;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services.Instances;

namespace LLMDesktopAssistant.Desktop.Scripting.PowerShell
{
	/// <summary>
	/// The settings block of the PowerShell environment: the executable to run scripts with, and what discovery picked
	/// when the field is empty.
	/// </summary>
	[ViewModelFor(typeof(PowerShellEnvironmentConfigurationView))]
	public class PowerShellEnvironmentConfigurationViewModel : NotifyPropertyChanged
	{
		private readonly IExplorerOpener _explorerOpener;
		private readonly IPowerShellInterpreterLocator _interpreterLocator;

		/// <summary>
		/// The configuration this block edits.
		/// </summary>
		public PowerShellEnvironmentConfiguration Configuration { get; }

		/// <summary>
		/// Opens the picker for the PowerShell executable.
		/// </summary>
		public IRelayCommand SelectExecutableCommand { get; }

		/// <summary>
		/// Reveals the PowerShell executable in the file explorer.
		/// </summary>
		public IRelayCommand OpenExecutableCommand { get; }

		/// <summary>
		/// The executable that would run a script right now, or the reason there is none.
		/// </summary>
		public string ResolvedText => _interpreterLocator.Resolve(Configuration.ExecutablePath) switch
		{
			{ Found: true } interpreter => interpreter.Path!,
			{ Problem: { } problem } => problem,
			_ => string.Empty
		};

		/// <summary>
		/// Initializes a new instance of the <see cref="PowerShellEnvironmentConfigurationViewModel"/> class.
		/// </summary>
		/// <param name="configuration">The configuration to edit.</param>
		/// <param name="interpreterLocator">The locator that answers what discovery picked.</param>
		/// <param name="explorerOpener">The service that reveals files in the file explorer.</param>
		public PowerShellEnvironmentConfigurationViewModel(PowerShellEnvironmentConfiguration configuration,
			IPowerShellInterpreterLocator interpreterLocator, IExplorerOpener explorerOpener)
		{
			Configuration = configuration;
			_interpreterLocator = interpreterLocator;
			_explorerOpener = explorerOpener;

			Configuration.PropertyChanged += OnConfigurationChanged;
			SelectExecutableCommand = new AsyncRelayCommand(SelectExecutable);
			OpenExecutableCommand = new RelayCommand(OpenExecutable);
		}

		private void OnConfigurationChanged(object? sender, PropertyChangedEventArgs args)
		{
			if (args.PropertyName is nameof(PowerShellEnvironmentConfiguration.ExecutablePath))
				RaisePropertyChanged(nameof(ResolvedText));
		}

		private async Task SelectExecutable()
		{
			var result = await App.MainTopLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = LocalizationManager.LocalizeStatic("env.powershell.select_executable"),
				FileTypeFilter = [
					new FilePickerFileType("Executables") { Patterns = ["*.exe"] },
					new FilePickerFileType("All files") { Patterns = ["*"] }
				],
				AllowMultiple = false
			});

			if (result.Count > 0)
				Configuration.ExecutablePath = result[0].Path.LocalPath;
		}

		private void OpenExecutable()
		{
			var path = Configuration.ExecutablePath;
			if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
				_explorerOpener?.ShowFileInExplorer(path);
		}
	}
}
