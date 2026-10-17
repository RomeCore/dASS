using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Domain;
using Microsoft.Extensions.DependencyInjection;
using RCLargeLanguageModels.Tasks;
using RCLargeLanguageModels.Tools;
using DomainToolCall = LLMDesktopAssistant.LLM.Domain.ToolCall;

namespace LLMDesktopAssistant.Desktop.Tests.ToolModules
{
	/// <summary>
	/// Builds a real chat execution context for a tool inside the Desktop test project.
	/// </summary>
	internal static class ToolTestContext
	{
		internal sealed class Session
		{
			public required DomainToolCall Call { get; init; }

			public required IToolExecutionContext Context { get; init; }
		}

		public static Session Create(ToolInfo tool, JsonNode? args = null)
		{
			var services = new ServiceCollection();
			services.AddSingleton<IChatSettingsService, ChatSettingsService>();
			var chat = new Chat(services.BuildServiceProvider());

			var message = new AssistantMessage
			{
				AgentStageId = Guid.Empty,
				SenderAgentId = Guid.Empty,
				Status = AssistantMessageStatus.Success
			};
			chat.Messages.Add(message);

			var call = new DomainToolCall
			{
				ToolCallId = ToolCallId.Generate(),
				ToolName = tool.Name,
				Arguments = args?.ToJsonString() ?? "{}"
			};
			message.ToolCalls.Add(call);

			var parameters = new ToolContextCreationParams
			{
				Call = call,
				Descriptor = tool,
				Source = ToolInvocationSource.ChatRun,
				Host = message,
				RunningInUi = false,
				Confirmation = new DeniedConfirmationSink()
			};

			return new Session
			{
				Call = call,
				Context = new ChatToolExecutionContext(chat, message, parameters, null)
			};
		}

		private sealed class DeniedConfirmationSink : IConfirmationSink
		{
			public Task<ToolApprovalResult> RequestAsync(IToolExecutionContext ctx, CancellationToken ct) =>
				Task.FromResult(ToolApprovalResult.DeniedOnce("Tests do not confirm tool executions."));
		}
	}
}
