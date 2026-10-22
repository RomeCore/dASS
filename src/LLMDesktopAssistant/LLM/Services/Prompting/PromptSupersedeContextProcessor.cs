using System.Text;
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
			IEnumerable<IPromptSupersedeContextProvider> contextProviders, CancellationToken cancellationToken = default)
		{
			if (chat.Messages.Count == 0 ||
				chat.Messages[^1].Message is not AssistantMessage { IsCompleted: false } pendingAssistantMessage)
				throw new InvalidOperationException("Expected a pending assistant message, but none was found.");

			Dictionary<string, IPromptSupersedeContextProvider> uncheckedProviders = contextProviders.ToDictionary(c => c.Discriminator);
			Dictionary<string, PromptSupersedeStampBase> lastStamps = [];

			// Since prompt supersede stamps can survive any compactions, we are not using the effective context.
			for (int i = chat.Messages.Count - 2; i >= 0; i--)
			{
				var branchedMessage = chat.Messages[i];

				if (branchedMessage.Message is not AssistantMessage assistantMessage || assistantMessage.SenderAgentId != agent.Id)
					continue;
				if (assistantMessage.AdditionalData.TryGet<PromptSupersedeStampMessageData>() is not { } stampData)
					continue;

				bool foundAll = false;
				foreach (var stamp in stampData.Stamps)
				{
					if (uncheckedProviders.TryGetValue(stamp.Discriminator, out var provider))
					{
						lastStamps.Add(stamp.Discriminator, stamp);
						uncheckedProviders.Remove(stamp.Discriminator);

						if (uncheckedProviders.Count == 0)
						{
							foundAll = true;
							break;
						}
					}
				}

				if (foundAll)
					break;
			}

			var stamps = new List<PromptSupersedeStampBase>();

			foreach (var provider in contextProviders)
			{
				var stamp = await provider.GetNewStampAsync(lastStamps.GetValueOrDefault(provider.Discriminator),
					effectiveContext, cancellationToken);
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
				// Rebuilding the same pending message must not stack a second copy of the stamps.
				pendingAssistantMessage.AdditionalData.TryReplace(new PromptSupersedeStampMessageData
				{
					Stamps = [.. stamps]
				});
			}
		}
	}
}
