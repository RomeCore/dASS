using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Prompting.Context
{
	/// <summary>
	/// Captures the stamp of a supersede section: the state that must be re-injected into the
	/// conversation when the previous stamp becomes superseded (stale).
	/// </summary>
	/// <typeparam name="TStamp">The type of the section stamp.</typeparam>
	public interface IPromptSupersedeStampProvider<TStamp>
		where TStamp : PromptSupersedeStampBase
	{
		/// <summary>
		/// Gets the new stamp of the section, or null when the previous stamp is still actual
		/// and nothing has to be re-injected.
		/// </summary>
		/// <param name="previousStamp">
		/// The last known stamp of this section, or null when the section was never stamped before.
		/// </param>
		/// <param name="context">The effective chat context to consider when calculating the stamp.</param>
		TStamp? CaptureStamp(TStamp? previousStamp, PromptContextExecutionContext context);
	}
}
