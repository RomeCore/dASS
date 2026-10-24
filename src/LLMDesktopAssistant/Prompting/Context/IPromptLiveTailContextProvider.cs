using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Prompting.Context
{
	/// <summary>
	/// A live-tail section: a part of the prompt context that is captured and rendered for the
	/// current generation only. Live tails are never persisted, so they can carry any transient
	/// information that must not pollute the chat history.
	/// Use <see cref="PromptLiveTailContextBase"/> as the default implementation.
	/// </summary>
	public interface IPromptLiveTailContextProvider : IPromptContextProvider
	{
		/// <summary>
		/// Provides a live-tail context for the given chat context.
		/// This context is not persisted, it is only used for the current generation of a response.
		/// </summary>
		/// <param name="context">The chat context to provide the live-tail context for.</param>
		/// <param name="cancellationToken">The cancellation token that aborts the provider (for example, a script timeout).</param>
		/// <returns>The live-tail context for the given chat context.</returns>
		Task<string?> ProvideAsync(PromptContextExecutionContext context, CancellationToken cancellationToken = default);
	}
}
