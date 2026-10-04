using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Tools.Consents;
using RCLargeLanguageModels.Tasks;
using RCLargeLanguageModels.Tools;

namespace LLMDesktopAssistant.Tools
{
	/// <summary>
	/// The context in which a tool is executed.
	/// This used to provide additional information about the execution environment of a tool.
	/// </summary>
	public class ToolExecutionContext
	{
		/// <summary>
		/// The chat instance where tool is being executed.
		/// </summary>
		public required Chat Chat { get; init; }

		/// <summary>
		/// The message that contains tool call that being executed.
		/// </summary>
		public required ChatMessage Message { get; init; }

		/// <summary>
		/// Finds the message id of the message that contains tool call. If not found, returns 0.
		/// </summary>
		/// <returns>The message id of the message.</returns>
		public int FindMessageId() => Chat.Messages.FirstOrDefault(m => m.Message == Message)?.MessageId ?? 0;

		/// <summary>
		/// The tool call that is being executed.
		/// </summary>
		public required ToolCall Call { get; init; }

		/// <summary>
		/// Information about the tool that is being executed.
		/// </summary>
		public required ToolInfo Info { get; init; }

		/// <summary>
		/// Whether the tool is running in a user interface.
		/// </summary>
		public required bool RunningInUI { get; init; }

		/// <summary>
		/// The decision made by the tool execution pipeline.
		/// For non-UI contexts, this will be <see cref="ToolPolicyDecision.None"/> at most times.
		/// For UI contexts and without a specific self-handled decisions 
		/// (see <see cref="ToolInfo.DefaultSelfHandledDecisions"/> and <see cref="PreviewToolExecutionResult.SelfHandledDecisions"/>)
		/// this will be <see cref="ToolPolicyDecision.Approve"/>.
		/// For UI contexts with a specific self-handled decisions this will be the decision made by the tool execution pipeline.
		/// </summary>
		public required ToolPolicyDecision PolicyDecision { get; init; } = ToolPolicyDecision.None;

		/// <summary>
		/// The shared context that can be used to pass data between streaming, preview and main execution calls.
		/// </summary>
		public object? SharedContext { get; set; }

		/// <summary>
		/// The consent memorization context for self-handled confirmations, or <see langword="null"/>.
		/// Lets tools persist user consent decisions ("remember") for subsequent executions.
		/// </summary>
		public ToolConsentMemorizationContext? ConsentContext { get; init; }

		/// <summary>
		/// Creates a dummy tool execution context. Useful when the original execution context is not available.
		/// </summary>
		public static ToolExecutionContext CreateDummy(ToolInfo tool, JsonNode? args, Chat? chat)
		{
			var ct = new CompletionToken();
			var toolCall = new ToolCall
			{
				ToolName = tool.Name,

				CompletionToken = ct,
				ToolCallId = ToolCallId.Generate(),
				Arguments = args?.ToJsonString() ?? "{}"
			};
			var message = new AssistantMessage
			{
				CreatedAt = DateTime.Now,
				AgentStageId = Guid.Empty,
				SenderAgentId = Guid.Empty,
				CompletionToken = ct
			};
			message.ToolCalls.Add(toolCall);

			return new ToolExecutionContext
			{
				Chat = chat ?? CreateDummyChat(),
				Call = toolCall,
				Message = message,
				Info = tool,
				RunningInUI = false,
				PolicyDecision = ToolPolicyDecision.None
			};
		}

		private static Chat CreateDummyChat()
		{
			var services = new ServiceCollection();
			services.AddSingleton<IChatSettingsService, ChatSettingsService>();
			return new Chat(services.BuildServiceProvider());
		}
	}
}