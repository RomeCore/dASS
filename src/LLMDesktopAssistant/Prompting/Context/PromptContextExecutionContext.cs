using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Prompting.Context
{
	/// <summary>
	/// The context a prompt context provider receives: the effective chat context, the addon the provider belongs
	/// to, the chat and the chat-scoped services.
	/// </summary>
	/// <remarks>
	/// Every <c>IPrompt*Provider</c> takes this type — native sections, live tails, supersede sections and their
	/// delta and stamp providers alike — so the provider family has no asymmetry. The effective chat context is
	/// <em>contained</em>, not inherited: this object is "the effective context as seen by one provider", and the
	/// effective context stays a value it holds.
	/// </remarks>
	public sealed class PromptContextExecutionContext
	{
		/// <summary>The effective chat context the provider works on.</summary>
		public required EffectiveChatContext Context { get; init; }

		/// <summary>
		/// The context addon this provider belongs to, or <see langword="null"/> for a provider that is not
		/// addon-backed. In practice every collected prompt context is a <see cref="PromptContextInfo"/> addon,
		/// native and scriptable alike.
		/// </summary>
		public required PromptContextInfo? Addon { get; init; }

		/// <summary>The chat the context belongs to.</summary>
		public required Chat Chat { get; init; }

		/// <summary>The chat-scoped service provider.</summary>
		public required IServiceProvider Services { get; init; }

		/// <summary>
		/// Projects an effective chat context onto the execution context of one provider, carrying the addon it
		/// belongs to and the chat-scoped services.
		/// </summary>
		public static PromptContextExecutionContext From(EffectiveChatContext context, PromptContextInfo? addon,
			IServiceProvider services) => new()
			{
				Context = context,
				Addon = addon,
				Chat = context.Chat,
				Services = services
			};
	}
}
