using System.Text;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Prompting.Hooks;
using LLMDesktopAssistant.Providers;
using LLMDesktopAssistant.Tools;
using Material.Icons;
using Serilog;

namespace LLMDesktopAssistant.Agents.Memory
{
	/// <summary>
	/// Automatically retrieves relevant memory before the agent generates its first response of the round.
	/// Runs a dedicated retrieval agent that searches facts and episodic logs via delegate tools,
	/// then attaches the resulting digest to the pending assistant message with
	/// <see cref="AttachedMessageMode.AgentPrivate"/> mode so that only the agent itself sees it.
	/// </summary>
	[ChatService(typeof(IChatExecutionHook))]
	public class AutomaticMemoryReader(
		IMemoryFactStore memoryFactStore,
		IMemoryLogStore memoryLogStore,
		IAgentTaskExecutor agentTaskExecutor,
		IModelManager modelManager,
		ITemplateLibraryAccessor templates,
		IMessageVisibilityService messageVisibility,
		IChatStatusService statusService
	) : IChatExecutionHook
	{
		/// <inheritdoc />
		public int Order => 0;

		/// <inheritdoc />
		public async Task OnResponsePrepareAsync(ChatPrepareExecutionHookContext context, CancellationToken cancellationToken = default)
		{
			try
			{
				await RetrieveAsync(context, cancellationToken);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Automatic memory retrieval failed: {Error}", ex.Message);
			}
		}

		private async Task RetrieveAsync(ChatPrepareExecutionHookContext context, CancellationToken cancellationToken)
		{
			if (context.Cycle != 0)
				return;

			var chat = context.Chat;
			var memoryOptions = chat.Services.GetRequiredService<IChatSettingsService>().Settings.Memory.GetEffectiveMemoryOptions();
			if (!memoryOptions.AutomaticRetrievalEnabled || !memoryOptions.EnableMemory)
				return;

			var agent = context.Agent;
			if (!agent.Memory.EnableMemory)
				return;

			var model = modelManager.TryGetModel(memoryOptions.RetrievalModel);
			if (model is null)
				return;

			var attachments = agent.Memory.GetEnabledBlocks(chat.Services.GetRequiredService<IChatSettingsService>().Settings)
				.Where(b => b.Attachment.AllowsReading())
				.ToList();
			var factBlocks = attachments.Where(b => b.Block.FactsEnabled)
				.Select(b => b.Block).ToList();
			var logBlocks = attachments.Where(b => b.Block.LogsEnabled)
				.Select(b => b.Block).ToList();
			if (factBlocks.Count == 0 && logBlocks.Count == 0)
				return;

			var input = BuildRetrievalInput(chat, context);
			if (string.IsNullOrWhiteSpace(input))
				return;

			statusService.Icon = MaterialIconKind.Database;
			statusService.Text = LocalizationManager.LocalizeStatic("memory.status.retrieval");

			var template = templates.GetMessagesTemplate("memory_retrieval_prompt");
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

			var tools = new AgentMemoryTools(memoryFactStore, memoryLogStore, chat.Id, factBlocks, logBlocks, 0)
				.CreateReaderTools();

			var task = agentTaskExecutor.Execute(new AgentTaskLaunchParameters
			{
				TaskName = LocalizationManager.LocalizeStatic("memory.retrieval.task"),
				TriggeredChat = chat,
				Model = model,
				InitialMessages = [.. messages],
				Tools = tools,
				TimeOut = TimeSpan.FromMinutes(3),
				CompletionExpiryTime = TimeSpan.FromMinutes(3)
			}, cancellationToken);

			await task;

			var digest = task.LastGeneratedContent;
			if (string.IsNullOrWhiteSpace(digest))
				return;

			context.Response.AdditionalData.Add(new AttachedMessageAdditionalViewModel
			{
				Mode = AttachedMessageMode.AgentPrivate,
				Content = $"""
					<memory_digest>
					{digest}
					</memory_digest>
					"""
			});
		}

		private string BuildRetrievalInput(Chat chat, ChatPrepareExecutionHookContext context)
		{
			// The last round contains the pending (empty) response placeholder, so take up to two
			// fully visible rounds plus the current user messages.
			var rounds = MessagesInterface.GroupMessagesIntoRounds(chat.Messages, 2);
			var sb = new StringBuilder();

			foreach (var round in rounds)
			{
				foreach (var branched in round)
				{
					switch (branched.Message)
					{
						case UserMessage userMessage when messageVisibility.CheckVisibility(branched, context.Agent).EffectiveVisible:
							sb.Append("User: ").AppendLine(userMessage.Content);
							break;
						case AssistantMessage assistantMessage when !string.IsNullOrEmpty(assistantMessage.Content)
							&& messageVisibility.CheckVisibility(branched, context.Agent).EffectiveVisible:
							sb.Append("Assistant: ").AppendLine(assistantMessage.Content);
							break;
					}
				}
				sb.AppendLine();
			}

			return sb.ToString().Trim();
		}
	}
}
