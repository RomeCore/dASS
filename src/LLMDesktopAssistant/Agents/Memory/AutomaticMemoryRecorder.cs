using System.Text;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Providers;
using Serilog;

namespace LLMDesktopAssistant.Agents.Memory
{
	/// <summary>
	/// Automatically records the current round of the conversation into the agent's memory blocks
	/// after the agent execution chain has finished. Runs a dedicated recording agent that searches
	/// existing facts and stores new facts and an episodic log via delegate tools.
	/// </summary>
	[ChatService(typeof(IChatExecutionHook))]
	public class AutomaticMemoryRecorder(
		IMemoryFactStore memoryFactStore,
		IMemoryLogStore memoryLogStore,
		IAgentTaskExecutor agentTaskExecutor,
		IModelManager modelManager,
		ITemplateLibraryAccessor templates,
		IMessageVisibilityService messageVisibility
	) : IChatExecutionHook
	{
		/// <inheritdoc />
		public int Order => 0;

		/// <inheritdoc />
		public async Task OnAgentExecutionFinishedAsync(ChatAgentExecutionHookContext context, CancellationToken cancellationToken = default)
		{
			try
			{
				await RecordAsync(context);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Automatic memory recording failed: {Error}", ex.Message);
			}
		}

		private async Task RecordAsync(ChatAgentExecutionHookContext context)
		{
			var chat = context.Chat;
			var chatSettings = chat.Services.GetRequiredService<IChatSettingsService>().Settings;
			var memoryOptions = chatSettings.Memory.GetEffectiveMemoryOptions();
			if (!memoryOptions.AutomaticRecordingEnabled || !memoryOptions.EnableMemory)
				return;

			var agent = context.Agent;
			if (!agent.Memory.EnableMemory)
				return;

			var model = modelManager.TryGetModel(memoryOptions.RecordingModel);
			if (model is null)
				return;

			var attachments = agent.Memory.GetEnabledBlocks(chatSettings)
				.Where(b => b.Attachment.AllowsWriting())
				.ToList();
			var factBlocks = attachments.Where(b => b.Block.FactsEnabled)
				.Select(b => b.Block).ToList();
			var logBlocks = attachments.Where(b => b.Block.LogsEnabled)
				.Select(b => b.Block).ToList();
			if (factBlocks.Count == 0 && logBlocks.Count == 0)
				return;

			var input = BuildRecordingInput(chat, context);
			if (string.IsNullOrWhiteSpace(input))
				return;

			var template = templates.GetMessagesTemplate("memory_recording_prompt");
			var messages = template.RenderToAgent(new
			{
				blocks = attachments.Select(b => new
				{
					name = b.Block.Name,
					can_read = b.Attachment.AllowsReading(),
					can_write = b.Attachment.AllowsWriting(),
					facts_enabled = b.Block.FactsEnabled,
					logs_enabled = b.Block.LogsEnabled,
					description = b.Block.Description
				}).ToArray(),
				input
			});

			var tools = new AgentMemoryTools(memoryFactStore, memoryLogStore, chat.Id, factBlocks, logBlocks,
					FindSourceMessageId(chat, context.Responses))
				.CreateRecorderTools();

			var task = agentTaskExecutor.Execute(new AgentTaskLaunchParameters
			{
				TaskName = LocalizationManager.LocalizeStatic("memory.recording.task"),
				TriggeredChat = chat,
				Model = model,
				InitialMessages = [.. messages],
				Tools = tools,
				TimeOut = TimeSpan.FromMinutes(3),
				CompletionExpiryTime = TimeSpan.FromMinutes(3)
			});

			await task;
		}

		private string BuildRecordingInput(Chat chat, ChatAgentExecutionHookContext context)
		{
			var rounds = MessagesInterface.GroupMessagesIntoRounds(chat.Messages, 1);
			if (rounds.Count == 0)
				return string.Empty;

			var lastRound = rounds[^1];
			var responseSet = context.Responses.ToHashSet();
			var sb = new StringBuilder();

			foreach (var branched in lastRound)
			{
				switch (branched.Message)
				{
					case UserMessage userMessage when !string.IsNullOrWhiteSpace(userMessage.Content)
						&& messageVisibility.CheckVisibility(branched, context.Agent).EffectiveVisible:
						sb.Append("User: ").AppendLine(userMessage.Content);
						break;
					case AssistantMessage assistantMessage when !string.IsNullOrWhiteSpace(assistantMessage.Content)
						&& responseSet.Contains(assistantMessage):
						sb.Append("Assistant: ").AppendLine(assistantMessage.Content);
						break;
				}
			}

			return sb.ToString().Trim();
		}

		private static int FindSourceMessageId(Chat chat, ImmutableList<AssistantMessage> responses)
		{
			if (responses.Count == 0)
				return 0;

			var lastResponse = responses[^1];
			var branched = chat.Messages.FirstOrDefault(m => ReferenceEquals(m.Message, lastResponse));
			return branched?.MessageId ?? 0;
		}
	}
}
