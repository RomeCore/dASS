using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Prompting.Context
{
	/// <summary>
	/// A supersede section: a part of the prompt context that re-injects its own state into the
	/// conversation when the previously injected state becomes superseded (stale).
	/// Stamps survive any context compaction, so the section is always able to catch up with the
	/// messages the agent did not observe.
	/// Use <see cref="PromptSupersedeContextBase{TStamp}"/> as the default implementation.
	/// </summary>
	public interface IPromptSupersedeContextProvider : IPromptContextProvider
	{
		/// <summary>
		/// Gets the discriminator used to separate the stamp data from other prompt contexts.
		/// </summary>
		string Discriminator { get; }

		/// <summary>
		/// Gets the new stamp for the prompt context.
		/// </summary>
		/// <param name="previousStamp">The previous stamp associated with the prompt context. Can be null.</param>
		/// <param name="context">The effective chat context associated with the prompt context. Can be null.</param>
		/// <param name="cancellationToken">The cancellation token that aborts the provider (for example, a script timeout).</param>
		/// <returns>The new stamp for the prompt context. Can be null when no new stamp is needed.</returns>
		Task<PromptSupersedeStampBase?> GetNewStampAsync(PromptSupersedeStampBase? previousStamp,
			EffectiveChatContext context, CancellationToken cancellationToken = default);

		/// <summary>
		/// Renders the stamp into a compact text fragment that is injected into a system reminder.
		/// </summary>
		/// <param name="stamp">The stamp to render.</param>
		/// <returns>The rendered text fragment.</returns>
		string Render(PromptSupersedeStampBase stamp);
	}
}
