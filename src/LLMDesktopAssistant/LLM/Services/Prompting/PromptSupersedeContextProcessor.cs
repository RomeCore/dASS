using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Prompting.Context;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	/// <inheritdoc cref="IPromptSupersedeContextProcessor"/>
	[ChatService(typeof(IPromptSupersedeContextProcessor))]
	public class PromptSupersedeContextProcessor(
		Chat chat
	) : IPromptSupersedeContextProcessor
	{
		public async Task ProcessAsync(ChatAgentDescriptor agent, EffectiveChatContext effectiveContext,
			IEnumerable<PromptContextInfo> contexts, CancellationToken cancellationToken = default)
		{
			if (chat.Messages.Count == 0 ||
				chat.Messages[^1].Message is not AssistantMessage { IsCompleted: false } pendingAssistantMessage)
				throw new InvalidOperationException("Expected a pending assistant message, but none was found.");

			var providers = contexts
				.Where(context => context.Provider is IPromptSupersedeContextProvider)
				.Select(context => (Addon: context, Provider: (IPromptSupersedeContextProvider)context.Provider))
				.ToArray();

			if (providers.Length == 0)
				return;

			Dictionary<string, PromptSupersedeStampBase> lastStamps = [];
			var discriminators = providers.Select(p => p.Provider.Discriminator).ToHashSet(StringComparer.Ordinal);

			// Since prompt supersede stamps can survive any compactions, we are not using the effective context.
			for (int i = chat.Messages.Count - 2; i >= 0; i--)
			{
				var branchedMessage = chat.Messages[i];

				if (branchedMessage.Message is not AssistantMessage assistantMessage || assistantMessage.SenderAgentId != agent.Id)
					continue;
				if (assistantMessage.AdditionalData.TryGet<PromptSupersedeStampMessageData>() is not { } stampData)
					continue;

				foreach (var stamp in stampData.Stamps)
				{
					if (discriminators.Contains(stamp.Discriminator))
						lastStamps.TryAdd(stamp.Discriminator, stamp);
				}

				if (lastStamps.Count == discriminators.Count)
					break;
			}

			var stamps = new List<PromptSupersedeStampBase>();

			foreach (var (addon, provider) in providers)
			{
				var stamp = await provider.GetNewStampAsync(lastStamps.GetValueOrDefault(provider.Discriminator),
					PromptContextExecutionContext.From(effectiveContext, addon, chat.Services), cancellationToken);
				if (stamp is not null)
				{
					stamp.Discriminator = provider.Discriminator;
					var rendered = provider.Render(stamp);
					stamp.Snapshot = rendered;
					stamps.Add(stamp);
				}
			}

			if (stamps.Count > 0)
			{
				// A message carries one container: rebuild it by merging, so a provider that reported "unchanged"
				// keeps its already announced stamp instead of being silently dropped by the rewrite.
				var existing = pendingAssistantMessage.AdditionalData.TryGet<PromptSupersedeStampMessageData>()?.Stamps ?? [];
				var merged = existing.Where(old => stamps.All(fresh => fresh.Discriminator != old.Discriminator))
					.Concat(stamps);

				pendingAssistantMessage.AdditionalData.TryReplace(new PromptSupersedeStampMessageData
				{
					Stamps = [.. merged]
				});
			}
		}
	}
}
