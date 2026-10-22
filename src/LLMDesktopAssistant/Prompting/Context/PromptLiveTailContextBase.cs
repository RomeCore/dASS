using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Prompting.Context
{
	/// <summary>
	/// Base class for live-tail contexts.
	/// </summary>
	public abstract class PromptLiveTailContextBase : IPromptLiveTailContextProvider
	{
		/// <inheritdoc/>
		public abstract Task<string?> ProvideAsync(EffectiveChatContext context,
			CancellationToken cancellationToken = default);
	}
}
