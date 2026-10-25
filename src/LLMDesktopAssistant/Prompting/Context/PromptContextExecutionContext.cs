using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Prompting.Context
{
	/// <summary>
	/// The context a prompt context provider receives: the effective chat context, the addon the provider belongs
	/// to, the chat and the chat-scoped services.
	/// </summary>
	/// <remarks>
	/// Every <c>IPrompt*Provider</c> takes this type - native sections, live tails, supersede sections and their
	/// delta and stamp providers alike - so the provider family has no asymmetry. The effective chat context is
	/// <em>contained</em>, not inherited: this object is "the effective context as seen by one provider", and the
	/// effective context stays a value it holds.
	/// </remarks>
	public sealed class PromptContextExecutionContext
	{
		/// <summary>
		/// The chat the context belongs to.
		/// </summary>
		public required Chat Chat { get; init; }

		/// <summary>
		/// The chat-scoped service provider.
		/// </summary>
		public required IServiceProvider Services { get; init; }

		/// <summary>
		/// The effective chat context the provider works on.
		/// </summary>
		public required EffectiveChatContext Context { get; init; }

		/// <summary>
		/// The context addon this provider belongs to.
		/// </summary>
		public required PromptContextInfo PromptContext { get; init; }
	}
}
