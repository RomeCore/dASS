using System.Net.Mail;
using System.Text;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Agents.Settings;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.LLM.Services.Tools;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Prompting.ContextExpanders;
using LLMDesktopAssistant.Prompting.Hooks;
using LLMDesktopAssistant.Prompting.Plugins;
using LLMDesktopAssistant.Users;
using LLTSharp;
using RCLargeLanguageModels.Messages;
using RCLargeLanguageModels.Messages.Attachments;
using RCLargeLanguageModels.Tools;
using Serilog;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	/// <inheritdoc cref="IAgentPromptComposer"/>
	[ChatService(typeof(IAgentPromptComposer))]
	public class AgentPromptComposer(
		Chat chat,
		IChatSettingsService chatSettings,
		IChatMessageQuoteRenderer messageQuoteRenderer,
		IAgentEffectiveMessagesProvider effectiveMessagesProvider,
		IEnumerable<IPromptBuildingHook> promptBuildingHooks,
		IToolsetCacheService toolsetCache,
		IPromptAnchoredSectionProcessor promptAnchoredSectionProcessor,
		IPromptSupersedeContextProcessor promptSupersedeContextProcessor,
		IAddonSetCollector<PromptContextInfo> promptContextCollector,
		IPromptDumpService promptDumpService
		) : IAgentPromptComposer
	{
		private const string summaryTag = "summary";
		private const string systemReminderTag = "system-reminder";

		/// <inheritdoc/>
		public AgentPromptBundle Build(ChatAgentDescriptor agent)
		{
			// The composer owns the toolset cache: it must be fresh for both the tool definitions
			// and the tool call resolution performed by the execution service.
			toolsetCache.Invalidate(agent);

			var effectiveContext = effectiveMessagesProvider.GetEffectiveMessages(agent);

			var hooks = promptBuildingHooks.OrderBy(h => h.Order).ToList();

			List<IMessage> result = [];
			var disabledCheckpoints = agent.Context.GetEffectiveDisabledFlags(chatSettings.Settings);

			var promptContextInfos = promptContextCollector.GetAddonsForAgent(agent);
			var promptContextProviders = promptContextInfos.Select(i => i.Provider).ToArray();

			var promptMode = agent.Context.PromptMode;
			var anchor = promptAnchoredSectionProcessor.Process(agent, effectiveContext, promptContextProviders.Anchored());
			promptSupersedeContextProcessor.Process(agent, effectiveContext, promptContextProviders.Supersede());

			SystemPromptSnapshot header;
			string headerSource;
			switch (promptMode)
			{
				case PromptContextMode.Static:
					var settings = agent.Context;
					if (settings.Snapshot is null)
					{
						settings.Snapshot = promptContextProviders.Anchored().RenderHeader(agent);
						Log.Information("Froze static system prompt snapshot for agent {AgentId}.", agent.Id);
					}

					header = settings.Snapshot;
					headerSource = "static";
					break;

				case PromptContextMode.Hybrid when anchor != null:
					header = anchor.Snapshot;
					headerSource = $"anchor#{anchor.Id}";
					break;

				default:
					header = promptContextProviders.Anchored().RenderHeader(agent);
					headerSource = "live";
					break;
			}

			Log.Debug("Prompt header for agent {AgentId}: mode={Mode}, source={Source}.", agent.Id, promptMode, headerSource);

			var summaryCheckpoint = effectiveContext.Checkpoints.LastOrDefault(c =>
				(c.Checkpoint.Kind & ~disabledCheckpoints).HasFlag(ContextCheckpointKind.Summary));

			result.Add(new SystemMessage(header.Text));
			if (summaryCheckpoint != null)
				result.Add(new RCLargeLanguageModels.Messages.UserMessage(Senders.User, $"""
					<{summaryTag}>
					{summaryCheckpoint.Checkpoint.Context}
					</{summaryTag}>
					"""));

			// SCM stamps: walk up beyond effective message history (but including one effective message)
			// to find the most recent stamp of each type.
			Dictionary<string, (int MsgId, int Order, PromptSupersedeStampBase Stamp)>? seenStamps = [];
			for (int i = effectiveContext.EffectiveMessagesStartIndex; i >= 0; i--)
			{
				var branchedMessage = chat.Messages[i];

				if (branchedMessage.Message is not Domain.AssistantMessage assistantMessage || assistantMessage.SenderAgentId != agent.Id)
					continue;
				if (assistantMessage.AdditionalData.TryGet<PromptSupersedeStampMessageData>() is not { } stampData)
					continue;

				foreach (var stamp in stampData.Stamps)
				{
					if (seenStamps.ContainsKey(stamp.Discriminator))
						continue;

					seenStamps[stamp.Discriminator] = (i, seenStamps.Count, stamp);
				}
			}

			for (int i = 0; i < effectiveContext.Messages.Count; i++)
			{
				var effectiveMessage = effectiveContext.Messages[i];
				var branchedMessage = effectiveMessage.BranchedMessage;

				IEnumerable<IMessage> messages;
				bool isPendingAssistant = false;
				if (branchedMessage.Message is Domain.AssistantMessage assistantMessage && !assistantMessage.IsCompleted)
				{
					isPendingAssistant = true;
					messages = [];
				}
				else
				{
					messages = ConvertMessageForAgent(effectiveMessage, agent);
				}

				foreach (var hook in hooks)
				{
					var editedMessages = hook.ModifyFinalContext(messages, branchedMessage, agent);
					if (editedMessages != null)
						messages = editedMessages;
				}

				if (branchedMessage.Message is Domain.AssistantMessage)
				{
					var systemReminderSb = new StringBuilder();
					systemReminderSb.Append($"<{systemReminderTag}>").Append('\n');
					int dataCounter = 0;

					// Process SCM anchor deltas.
					if (anchor is not null && branchedMessage.Message.AdditionalData.TryGet<PromptStateDeltaMessageData>() is { } deltas)
					{
						if (deltas.AnchorId == anchor.Id && !string.IsNullOrWhiteSpace(deltas.Snapshot))
						{
							systemReminderSb.Append(deltas.Snapshot).Append('\n');
							dataCounter++;
						}
					}

					// Process SCM supersede stamps.
					if (seenStamps != null)
					{
						// Render stamps if the message is the first message after cut.
						// This should include the stamps before the cut.
						foreach (var (_, _, stamp) in seenStamps.Values
							.OrderBy(s => s.MsgId)
							.ThenBy(s => s.Order))
						{
							systemReminderSb.Append(stamp.Snapshot).Append('\n');
							dataCounter++;
						}
						seenStamps = null;
					}
					else if (branchedMessage.Message.AdditionalData.TryGet<PromptSupersedeStampMessageData>() is { } stamps)
					{
						foreach (var stamp in stamps.Stamps)
						{
							systemReminderSb.Append(stamp.Snapshot).Append('\n');
							dataCounter++;
						}
					}

					// Process SCM live tails.
					if (isPendingAssistant)
					{
						var liveContextProviders = promptContextProviders.LiveTails().ToArray();
						if (liveContextProviders.Length > 0)
						{
							var sb = new StringBuilder();
							foreach (var provider in liveContextProviders)
							{
								var liveContext = provider.Provide(effectiveContext);
								if (!string.IsNullOrWhiteSpace(liveContext))
								{
									systemReminderSb.Append(liveContext).Append('\n');
									dataCounter++;
								}
							}
						}
					}

					systemReminderSb.Append($"</{systemReminderTag}>");

					if (dataCounter > 0)
					{
						if (result.Count > 0 && result[^1] is IToolMessage lastToolMessage)
						{
							// Replace the last tool result with the new one, appending the system reminder.
							// This helps to avoid interrupting the assistant's tool cycle.
							var lastToolResult = lastToolMessage.Result;
							var replacedToolResult = new RCLargeLanguageModels.Tools.ToolResult(lastToolResult.Status,
								$"""
								{lastToolResult.Content}
								{systemReminderSb}
								""", lastToolResult.Attachments);
							result[^1] = new RCLargeLanguageModels.Messages.ToolMessage(replacedToolResult,
								lastToolMessage.ToolCallId, lastToolMessage.ToolName);
						}
						else
						{
							result.Add(new RCLargeLanguageModels.Messages.UserMessage(systemReminderSb.ToString()));
						}
					}
				}

				result.AddRange(messages);
			}

			List<FunctionTool> tools = [.. header.Tools.Select(t => t.ToFunctionTool())];

			if (PromptDumpService.IsEnabled)
				promptDumpService.Dump(result, tools, $"mode={promptMode}; header={headerSource}");

			return new AgentPromptBundle(result, tools);
		}

		private List<IMessage> ConvertMessageForAgent(EffectiveMessage effectiveMessage,
			ChatAgentDescriptor agent)
		{
			var branchedMessage = effectiveMessage.BranchedMessage;
			var affectedCheckpoints = effectiveMessage.AffectedCheckpoints;
			var message = branchedMessage.Message;

			if (message is Domain.AssistantMessage assistantMessage && assistantMessage.SenderAgentId == agent.Id)
			{
				List<IToolCall> toolCalls = [];
				List<IMessage> messages = [];

				foreach (var toolCall in assistantMessage.ToolCalls)
				{
					toolCalls.Add(new FunctionToolCall(toolCall.ToolCallId, toolCall.ToolName, toolCall.Arguments ?? string.Empty));
					var status = ConvertToolStatus(toolCall.Status);
					bool toolCompacted = affectedCheckpoints.HasFlag(ContextCheckpointKind.ForcedToolCompaction)
						|| (toolCall.CanBeCompacted && affectedCheckpoints.HasFlag(ContextCheckpointKind.ToolCompaction));
					var resultContent = toolCompacted
						? GetCompactedToolResultContent(toolCall.Status)
						: toolCall.ResultContent ?? string.Empty;
					var toolResult = new ToolResult(status, resultContent,
						toolCompacted ? [] : toolCall.GetNativeAttachments());
					messages.Add(new ToolMessage(toolResult, toolCall.ToolCallId, toolCall.ToolName));
				}

				bool reasoningCompacted = affectedCheckpoints.HasFlag(ContextCheckpointKind.ReasoningCompaction);
				var result = new RCLargeLanguageModels.Messages.AssistantMessage(
					assistantMessage.Content ?? string.Empty,
					reasoningCompacted ? string.Empty : assistantMessage.ReasoningContent ?? string.Empty,
					toolCalls: toolCalls,
					attachments: assistantMessage.GetNativeAttachments());
				messages.Insert(0, result);

				return messages;
			}

			var renderedQuote = messageQuoteRenderer.Render(branchedMessage,
				effectiveMessage.Facets, effectiveMessage.Identity, effectiveMessage.AffectedCheckpoints);
			return [new RCLargeLanguageModels.Messages.UserMessage(Senders.User,
				renderedQuote.Content, renderedQuote.NativeAttachments)];
		}

		private static ToolResultStatus ConvertToolStatus(ToolStatus status) => status switch
		{
			ToolStatus.None => ToolResultStatus.NoResult,
			ToolStatus.WaitingForApproval => ToolResultStatus.NoResult,
			ToolStatus.Executing => ToolResultStatus.NoResult,
			ToolStatus.Success => ToolResultStatus.Success,
			ToolStatus.Error => ToolResultStatus.Error,
			ToolStatus.Cancelled => ToolResultStatus.Cancelled,
			_ => ToolResultStatus.NoResult
		};

		private static string GetCompactedToolResultContent(ToolStatus status) => status switch
		{
			ToolStatus.Success => "[TOOL RESULT WAS COMPACTED, SUCCESSFUL BEFORE]",
			ToolStatus.Error => "[TOOL RESULT WAS COMPACTED, FAULTED BEFORE]",
			ToolStatus.Cancelled => "[TOOL RESULT WAS COMPACTED, CANCELLED BEFORE]",
			_ => "[TOOL RESULT WAS COMPACTED]"
		};
	}
}
