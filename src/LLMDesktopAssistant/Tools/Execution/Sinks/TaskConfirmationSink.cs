using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Sinks
{
	/// <summary>
	/// The agent-task world's confirmation surface: it surfaces the call on the task card, awaits the
	/// user's decision and memorizes it.
	/// </summary>
	/// <remarks>
	/// The task world owns its own memorization because it is the only owner of the triggered chat and the
	/// triggered message the "always" decision is persisted against. The shared confirmation stage sees
	/// no consent context on the task context, so nothing is memorized twice.
	/// </remarks>
	public sealed class TaskConfirmationSink(
		AgentTask task,
		IChatExecutionStatusService? statusService,
		IToolMemorizationService memorization) : IConfirmationSink
	{
		/// <inheritdoc/>
		public async Task<ToolApprovalResult> RequestAsync(IToolExecutionContext ctx, CancellationToken ct)
		{
			if (ctx.Call is not AgentToolCall call)
				return ToolApprovalResult.DeniedOnce(
					"The task confirmation surface requires an AgentToolCall.");

			using var confirmation = statusService?.WithConfirmation();

			var request = new AgentToolCallConfirmationRequest
			{
				ToolCall = call,
				UserConfirmationSource = new TaskCompletionSource<ToolConsentResult>(
					TaskCreationOptions.RunContinuationsAsynchronously)
			};

			task.ToolCallConfirmationRequests.Add(request);
			try
			{
				call.Status = ToolStatus.WaitingForApproval;
				var consent = await request.UserConfirmationSource.Task.WaitAsync(ct);
				Memorize(call.ToolName, consent);

				return new ToolApprovalResult
				{
					Approved = consent.IsApproved,
					Memorization = consent.Memorization,
					Notes = consent.Notes
				};
			}
			finally
			{
				task.ToolCallConfirmationRequests.Remove(request);
			}
		}

		private void Memorize(string toolName, ToolConsentResult consent)
		{
			var chat = task.LaunchParameters.TriggeredChat;
			memorization.MemorizeConsent(chat, toolName, consent);

			if (consent.Memorization == ToolApprovalMemorization.Always && chat is not null)
				MemorizeAlwaysInToolset(chat, toolName, consent.IsApproved);
		}

		/// <summary>
		/// Persists an "always" decision into the triggered message's sender agent toolset.
		/// </summary>
		/// <param name="chat">The triggered chat.</param>
		/// <param name="toolName">The tool name.</param>
		/// <param name="approved">Whether the tool is always approved or always disallowed.</param>
		private void MemorizeAlwaysInToolset(Chat chat, string toolName, bool approved)
		{
			var senderAgentId = (task.LaunchParameters.TriggeredMessage as AssistantMessage)?.SenderAgentId;
			if (senderAgentId is null)
				return;

			var agentManager = chat.Services.GetService<IAgentManagementService>();
			var chatSettings = chat.Services.GetService<IChatSettingsService>();
			if (agentManager is null || chatSettings is null)
				return;

			var agent = agentManager.GetAgentDescriptor(senderAgentId.Value);
			ToolConsentPersister.MemorizeAlways(agent, chatSettings, toolName, approved);
		}
	}
}
