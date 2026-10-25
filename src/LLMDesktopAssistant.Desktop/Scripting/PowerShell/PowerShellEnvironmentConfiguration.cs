using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Utils.Json;

namespace LLMDesktopAssistant.Desktop.Scripting.PowerShell
{
	/// <summary>
	/// The PowerShell block of the chat's environment settings.
	/// </summary>
	/// <remarks>
	/// One field, and empty means "discover it". The field exists because the two PowerShell lines behave differently
	/// in ways a script author can feel - 5.1 reads a BOM-less file as ANSI and cannot pass a hashtable - so pinning
	/// the interpreter is a way to make an addon behave the same way on every machine.
	/// </remarks>
	[JsonDerived(typeof(AdditionalEnvironmentSetting), "powershell")]
	public class PowerShellEnvironmentConfiguration : AdditionalEnvironmentSetting
	{
		/// <summary>
		/// The PowerShell executable to run scripts with. Empty means discovery (<c>pwsh</c>, then Windows PowerShell 5.1).
		/// </summary>
		public string? ExecutablePath
		{
			get;
			set => SetProperty(ref field, value);
		}
	}
}
