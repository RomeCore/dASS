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

namespace LLMDesktopAssistant.Tests.Tools
{
	/// <summary>
	/// Builds a real chat execution context for a tool, so tests exercise the same context the pipeline
	/// uses instead of a hand-made dummy.
	/// </summary>
	internal static class ToolTestContext
	{
		/// <summary>
		/// One prepared invocation: the call and its context.
		/// </summary>
		internal sealed class Session
		{
			public required DomainToolCall Call { get; init; }

			public required IToolExecutionContext Context { get; init; }
		}

		/// <summary>
		/// Creates a real chat context with a chat, an assistant message and the call.
		/// </summary>
		/// <param name="tool">The descriptor the context is created for.</param>
		/// <param name="args">The model's arguments, or <see langword="null"/> for an empty set.</param>
		/// <returns>The call and the context.</returns>
		public static Session Create(ToolInfo tool, JsonNode? args = null)
		{
			var services = new ServiceCollection();
			services.AddSingleton<IChatSettingsService, ChatSettingsService>();
			var chat = new Chat(services.BuildServiceProvider());

			var message = new AssistantMessage
			{
				AgentStageId = Guid.Empty,
				SenderAgentId = Guid.Empty,
				CompletionToken = CompletionToken.Success
			};
			chat.Messages.Add(message);

			var call = new DomainToolCall
			{
				CompletionToken = CompletionToken.Success,
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

		/// <summary>
		/// Runs the tool's main executor and returns the resulting call.
		/// </summary>
		/// <param name="tool">The descriptor to execute.</param>
		/// <param name="args">The model's arguments, or <see langword="null"/>.</param>
		/// <returns>The call the tool wrote into.</returns>
		public static async Task<DomainToolCall> ExecuteAsync(ToolInfo tool, JsonNode? args = null)
		{
			var session = Create(tool, args);
			await tool.Main.ExecuteAsync(args, session.Context, CancellationToken.None);
			return session.Call;
		}

		private sealed class DeniedConfirmationSink : IConfirmationSink
		{
			public Task<ToolApprovalResult> RequestAsync(IToolExecutionContext ctx, CancellationToken ct) =>
				Task.FromResult(ToolApprovalResult.DeniedOnce("Tests do not confirm tool executions."));
		}
	}
}
