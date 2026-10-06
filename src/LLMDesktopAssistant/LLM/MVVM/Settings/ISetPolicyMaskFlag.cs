using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.LLM.MVVM.Settings
{
	/// <summary>
	/// Implemented by settings view models that render a behaviour-policy toggles section
	/// (the agent tool settings and the chat sub-agent settings).
	/// </summary>
	public interface ISetPolicyMaskFlag
	{
		/// <summary>
		/// Sets the policy mask override for the specified behaviour flag of the tool/settings.
		/// </summary>
		/// <param name="flag">The behaviour flag to override.</param>
		/// <param name="state"><see langword="true"/> - auto-approve, <see langword="false"/> - disallowed, <see langword="null"/> - ask.</param>
		public void SetPolicyMaskFlag(ToolBehaviour flag, bool? state);
	}
}
