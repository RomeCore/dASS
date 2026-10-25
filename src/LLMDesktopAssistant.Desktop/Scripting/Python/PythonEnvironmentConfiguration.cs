using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Utils.Json;

namespace LLMDesktopAssistant.Desktop.Scripting.Python
{
	[JsonDerived(typeof(AdditionalEnvironmentSetting), "python")]
	public class PythonEnvironmentConfiguration : AdditionalEnvironmentSetting
	{
		private string? _venvActivateScriptPath;
		/// <summary>
		/// The path to the script that activates the python virtual environment used by the native python tools
		/// (<c>execute-python</c>, <c>execute-python_venv_shell</c>).
		/// </summary>
		public string? VenvActivateScriptPath
		{
			get => _venvActivateScriptPath;
			set => SetProperty(ref _venvActivateScriptPath, value);
		}

		private string? _addonVenvActivateScriptPath;
		/// <summary>
		/// The path to the script that activates the python virtual environment used by scriptable addons
		/// (tools, slash commands and prompt contexts). If null, <see cref="VenvActivateScriptPath"/> is used.
		/// </summary>
		public string? AddonVenvActivateScriptPath
		{
			get => _addonVenvActivateScriptPath;
			set => SetProperty(ref _addonVenvActivateScriptPath, value);
		}
	}
}
