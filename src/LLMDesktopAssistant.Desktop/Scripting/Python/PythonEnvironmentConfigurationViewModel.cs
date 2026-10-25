using System.IO;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services.Instances;

namespace LLMDesktopAssistant.Desktop.Scripting.Python
{
	/// <summary>
	/// The settings block of the python environments: one venv for the native python tools and one for the
	/// scriptable addons, which falls back to the first one.
	/// </summary>
	[ViewModelFor(typeof(PythonEnvironmentConfigurationView))]
	public class PythonEnvironmentConfigurationViewModel : NotifyPropertyChanged
	{
		private readonly IExplorerOpener _explorerOpener;

		/// <summary>
		/// The configuration this block edits.
		/// </summary>
		public PythonEnvironmentConfiguration Configuration { get; }

		/// <summary>
		/// Opens the picker for the native tools' venv activation script.
		/// </summary>
		public IRelayCommand SelectVenvActivateScriptPathCommand { get; }

		/// <summary>
		/// Reveals the native tools' venv activation script in the file explorer.
		/// </summary>
		public IRelayCommand OpenVenvActivateScriptPathCommand { get; }

		/// <summary>
		/// Opens the picker for the addons' venv activation script.
		/// </summary>
		public IRelayCommand SelectAddonVenvActivateScriptPathCommand { get; }

		/// <summary>
		/// Reveals the addons' venv activation script in the file explorer.
		/// </summary>
		public IRelayCommand OpenAddonVenvActivateScriptPathCommand { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="PythonEnvironmentConfigurationViewModel"/> class.
		/// </summary>
		/// <param name="configuration">The configuration to edit.</param>
		/// <param name="explorerOpener">The service that reveals files in the file explorer.</param>
		public PythonEnvironmentConfigurationViewModel(PythonEnvironmentConfiguration configuration, IExplorerOpener explorerOpener)
		{
			_explorerOpener = explorerOpener;
			Configuration = configuration;

			SelectVenvActivateScriptPathCommand = new AsyncRelayCommand(SelectVenvActivateScriptPath);
			OpenVenvActivateScriptPathCommand = new RelayCommand(OpenVenvActivateScriptPath);
			SelectAddonVenvActivateScriptPathCommand = new AsyncRelayCommand(SelectAddonVenvActivateScriptPath);
			OpenAddonVenvActivateScriptPathCommand = new RelayCommand(OpenAddonVenvActivateScriptPath);
		}

		private async Task SelectVenvActivateScriptPath()
		{
			var result = await App.MainTopLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = LocalizationManager.LocalizeStatic("env.python.select_venv_script"),
				FileTypeFilter = [
					new FilePickerFileType("Batch files") { Patterns = ["*.bat"] },
					new FilePickerFileType("All files") { Patterns = ["*"] }
				],
				AllowMultiple = false
			});

			if (result.Count > 0)
			{
				Configuration.VenvActivateScriptPath = result[0].Path.LocalPath;
			}
		}

		private void OpenVenvActivateScriptPath()
		{
			if (!string.IsNullOrWhiteSpace(Configuration.VenvActivateScriptPath) &&
				File.Exists(Configuration.VenvActivateScriptPath))
			{
				_explorerOpener?.ShowFileInExplorer(Configuration.VenvActivateScriptPath);
			}
		}

		private async Task SelectAddonVenvActivateScriptPath()
		{
			var result = await App.MainTopLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = LocalizationManager.LocalizeStatic("env.python.select_addon_venv_script"),
				FileTypeFilter = [
					new FilePickerFileType("Batch files") { Patterns = ["*.bat"] },
					new FilePickerFileType("All files") { Patterns = ["*"] }
				],
				AllowMultiple = false
			});

			if (result.Count > 0)
			{
				Configuration.AddonVenvActivateScriptPath = result[0].Path.LocalPath;
			}
		}

		private void OpenAddonVenvActivateScriptPath()
		{
			if (!string.IsNullOrWhiteSpace(Configuration.AddonVenvActivateScriptPath) &&
				File.Exists(Configuration.AddonVenvActivateScriptPath))
			{
				_explorerOpener?.ShowFileInExplorer(Configuration.AddonVenvActivateScriptPath);
			}
		}
	}
}
