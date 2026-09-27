using System.ComponentModel;
using System.Text;
using Avalonia.Threading;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.Domain;

using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Providers;
using RCLargeLanguageModels.Metadata;
using Serilog;

namespace LLMDesktopAssistant.LLM.Services
{
	[ChatService(typeof(IChatSummarizationService))]
	[ChatService(typeof(IChatExecutionHook))]
	public class ChatSummarizationService(
		Chat chat,
		IChatSettingsService chatSettings,
		IChatMessageQuoteRenderer quoteRenderer,
		ITemplateLibraryAccessor templates,
		IAgentTaskExecutor agentTaskExecutor,
		IModelManager modelManager
		) : IChatSummarizationService, IChatExecutionHook
	{
		/// <inheritdoc />
		public Task OnAgentResponseCompletedAsync(ChatAgentResponseExecutionHookContext context, CancellationToken cancellationToken = default)
		{
			return context.UsageMetadata != null
				? TrySummarizeChatAsync(context.UsageMetadata, cancellationToken)
				: Task.CompletedTask;
		}

		/// <inheritdoc />
		public int Order => 10;
		public async Task TrySummarizeChatAsync(IUsageMetadata lastUsageMetadata, CancellationToken cancellationToken = default)
		{
			var effectiveOptions = chatSettings.Settings.Memory.GetEffectiveSummarization();
			var summarizationLLM = modelManager.TryGetModel(effectiveOptions.SummarizerModel);

			try
			{
				if (!effectiveOptions.AutoSummarizationEnabled)
					return;

				var totalTokensUsed = lastUsageMetadata.TotalTokens;
				if (totalTokensUsed < effectiveOptions.SummarizationTriggerTokens)
					return;

				// If the summarization LLM is not available, do not summarize
				if (summarizationLLM == null)
					return;

				var lastRoundsToIgnore = effectiveOptions.IgnoreLastRounds;

				var rounds = GroupMessagesIntoRounds(chat.Messages.Select(m => m.Message).ToList());
				var roundsToSummarize = rounds.Take(Math.Max(0, rounds.Count - lastRoundsToIgnore)).ToList();
				if (roundsToSummarize.Count == 0)
					return;

				var lastMessageInRange = roundsToSummarize.Last().Last();
				await SummarizeMessageWithPreviousMessagesAsync(lastMessageInRange, cancellationToken);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to summarize chat: {Error}", ex.Message);
			}
		}

		/// <summary>
		/// Groups messages into rounds.
		/// A round = [one or more consecutive user messages] + [one or more consecutive assistant messages].
		/// </summary>
		private static List<List<ChatMessage>> GroupMessagesIntoRounds(IReadOnlyList<ChatMessage> messages)
		{
			var rounds = new List<List<ChatMessage>>();
			if (messages.Count == 0)
				return rounds;

			List<ChatMessage>? currentRound = null;
			bool? lastWasUser = null;

			foreach (var branched in messages)
			{
				bool isUser = branched is Domain.UserMessage;
				bool isAssistant = branched is Domain.AssistantMessage;

				if (isUser)
				{
					// Start a new round if previous was assistant, or first message is user
					if (lastWasUser == false || lastWasUser == null)
					{
						currentRound = [branched];
						rounds.Add(currentRound);
					}
					else
					{
						currentRound?.Add(branched);
					}
					lastWasUser = true;
				}
				else if (isAssistant)
				{
					if (lastWasUser == true || lastWasUser == null)
					{
						currentRound = [branched];
						rounds.Add(currentRound);
					}
					else
					{
						currentRound?.Add(branched);
					}
					lastWasUser = false;
				}
			}

			return rounds;
		}

		public async Task SummarizeMessageWithPreviousMessagesAsync(ChatMessage message, CancellationToken cancellationToken = default)
		{
			var effectiveOptions = chatSettings.Settings.Memory.GetEffectiveSummarization();
			var summarizationLLM = modelManager.TryGetModel(effectiveOptions.SummarizerModel);

			try
			{
				// If the summarization LLM is not available, do not summarize
				if (summarizationLLM == null)
					return;

				Log.Information("Started summarization process.");

				var summarizerTemplate = templates.GetTextTemplate("summarization_prompt");
				var summarizerPrompt = summarizerTemplate.Render();
				var summarizerInput = BuildSummarizerInput(message);

				var summarizationTask = agentTaskExecutor.Execute(new AgentTaskLaunchParameters
				{
					TaskName = LocalizationManager.LocalizeStatic("chat.summarization.task"),
					TriggeredChat = chat,
					Model = summarizationLLM,
					InitialMessages = [
						new AgentSystemMessage { Content = summarizerPrompt },
						new AgentUserMessage { Content = summarizerInput }
					]
				}, cancellationToken);

				var checkpoint = message.AdditionalData.TryGet<ContextCheckpoint>();
				if (checkpoint == null)
				{
					checkpoint = new ContextCheckpoint();
					message.AdditionalData.Add(checkpoint);
				}

				// Cut kinds are mutually exclusive: the summary replaces any shield on this message.
				checkpoint.Kind = (checkpoint.Kind & ~ContextCheckpointKind.Shield) | ContextCheckpointKind.Summary;
				checkpoint.Context = summarizationTask.LastGeneratedContent ?? string.Empty;
				checkpoint.IsCompletedAndEnabled = false;
				PropertyChangedEventHandler summaryChanged = (s, e) =>
				{
					if (e.PropertyName is nameof(summarizationTask.LastGeneratedContent))
						Dispatcher.UIThread.Post(() =>
						{
							checkpoint.Context = summarizationTask.LastGeneratedContent ?? string.Empty;
						});
				};
				summarizationTask.PropertyChanged += summaryChanged;

				await summarizationTask;
				summarizationTask.PropertyChanged -= summaryChanged;
				checkpoint.IsCompletedAndEnabled = true;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to summarize chat: {Error}", ex.Message);
			}
		}

		private string BuildSummarizerInput(ChatMessage targetMessage)
		{
			var sb = new StringBuilder();
			sb.AppendLine("Summarize this conversation:");

			var parts = new List<string>();
			string? latestSummary = null;
			bool encounteredUserMessage = false, foundTargetMessage = false;

			for (int i = chat.Messages.Count - 1; i >= 0; i--)
			{
				var message = chat.Messages[i].Message;

				if (!foundTargetMessage)
				{
					if (targetMessage == message)
						foundTargetMessage = true;
					else
						continue;
				}

				if (message.AdditionalData.TryGet<ContextCheckpoint>(out var shieldCheckpoint) &&
					shieldCheckpoint.IsCompletedAndEnabled &&
					shieldCheckpoint.Kind.HasFlag(ContextCheckpointKind.Shield))
					break;

				if (message is Domain.UserMessage userMessage)
				{
					encounteredUserMessage = true;
					if (latestSummary != null)
					{
						parts.Insert(0, quoteRenderer.Render(message).Content);
						break;
					}
				}

				if (targetMessage != message &&
					message.AdditionalData.TryGet<ContextCheckpoint>(out var summaryCheckpoint) &&
					summaryCheckpoint.IsCompletedAndEnabled &&
					summaryCheckpoint.Kind.HasFlag(ContextCheckpointKind.Summary))
				{
					latestSummary = summaryCheckpoint.Context;
					if (encounteredUserMessage)
						break;
				}

				if (latestSummary == null)
				{
					parts.Insert(0, quoteRenderer.Render(message).Content);
				}
			}

			if (!foundTargetMessage)
				throw new InvalidOperationException("Target message not found in chat history.");

			sb.Append("Latest summary: ").AppendLine(latestSummary);
			foreach (var part in parts)
				sb.AppendLine(part);

			return sb.ToString();
		}
	}
}
