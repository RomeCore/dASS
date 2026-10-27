using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Dispatch;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Sinks;

namespace LLMDesktopAssistant.Tools.Execution
{
	/// <summary>
	/// The chat implementation of the user's per-call actions: a restart is one <c>ChatRestart</c> dispatch on the
	/// existing call, a cancellation is the call's own source.
	/// </summary>
	/// <remarks>
	/// The descriptor is resolved from the chat's <em>available</em> tools, the same surface the tool commands use: a
	/// user action is not an agent decision, so the per-agent toolset overrides do not apply and no policy or
	/// confirmation runs. A tool that has disappeared disables the restart instead of silently re-running nothing.
	/// </remarks>
	[ChatService(typeof(IChatToolCallService))]
	public sealed class ChatToolCallService(
		Chat chat,
		IAddonSetCollector<ToolInfo> toolset,
		IToolInvocationDispatcher dispatcher,
		IToolMemorizationService memorization,
		IAgentManagementService agentManager,
		IChatSettingsService chatSettings) : IChatToolCallService
	{
		/// <inheritdoc/>
		public bool CanCancel(IToolCall call) =>
			!call.Status.IsTerminal() && call.CancellationTokenSource is { IsCancellationRequested: false };

		/// <inheritdoc/>
		public void Cancel(IToolCall call)
		{
			if (CanCancel(call))
				call.CancellationTokenSource!.Cancel();
		}

		/// <inheritdoc/>
		public bool CanRestart(IToolCall call) => call.Status switch
		{
			ToolStatus.None or ToolStatus.Pending or ToolStatus.Queued or ToolStatus.PreExecuting
				or ToolStatus.WaitingForApproval or ToolStatus.Executing => false,
			_ => true
		} && FindTool(call.ToolName) is not null;

		/// <inheritdoc/>
		public async Task RestartAsync(ChatMessage host, IToolCall call, CancellationToken ct)
		{
			var tool = FindTool(call.ToolName);
			if (tool is null)
				return;

			await dispatcher.RunAsync(new ToolInvocationRequest
			{
				Source = ToolInvocationSource.ChatRestart,
				Call = call,
				Descriptor = tool,
				Confirmation = ConstantConfirmationSink.Approve,
				ContextFactory = new ChatToolContextFactory(chat, memorization, agentManager, chatSettings),
				Host = host,
				RunningInUi = true
			}, ct);
		}

		private ToolInfo? FindTool(string name) =>
			toolset.GetAvailableAddons().FirstOrDefault(tool => tool.Name == name);
	}
}
