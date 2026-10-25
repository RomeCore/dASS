using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Utils.Json;

namespace LLMDesktopAssistant.Desktop.Scripting.Bash
{
	/// <summary>
	/// The bash block of the chat's environment settings.
	/// </summary>
	/// <remarks>
	/// One field, and empty means "discover it": a machine has one bash worth running scripts with, so there is
	/// nothing else to configure here.
	/// </remarks>
	[JsonDerived(typeof(AdditionalEnvironmentSetting), "bash")]
	public class BashEnvironmentConfiguration : AdditionalEnvironmentSetting
	{
		/// <summary>
		/// The bash executable to run scripts with. Empty means discovery (the configured path, Git Bash, then a
		/// non-WSL bash from <c>PATH</c>).
		/// </summary>
		public string? ExecutablePath
		{
			get;
			set => SetProperty(ref field, value);
		}
	}
}
