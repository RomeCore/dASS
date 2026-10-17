using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Specifiers;
using LLMDesktopAssistant.Utils.Attachments;
using RCLargeLanguageModels.Tasks;
using RCLargeLanguageModels.Tools;
using DomainToolCall = LLMDesktopAssistant.LLM.Domain.ToolCall;

namespace LLMDesktopAssistant.Agents.Tasks
{
	/// <summary>
	/// The transitional adapter that lets a chat tool participate in an agent task. It delegates every
	/// capability to the chat descriptor's carried executors; P5 dissolves it into the task toolset
	/// projection.
	/// </summary>
	public class ChatAgentTool : AgentTool
	{
		/// <summary>
		/// The descriptor of the chat tool this adapter exposes.
		/// </summary>
		public ToolInfo ChatToolInfo { get; }

		/// <summary>
		/// The execution context supplied by the caller, or <see langword="null"/> when the adapter
		/// should create its own.
		/// </summary>
		public IToolExecutionContext? ExecutionContext { get; }

		public override string Name => ChatToolInfo.Name;

		public override string DisplayName => ChatToolInfo.NameKey.RawValue ?? ChatToolInfo.Name;

		public override string Description => ChatToolInfo.Description;

		public override JsonObject ArgumentSchema => ChatToolInfo.ArgumentSchema;

		/// <summary>
		/// Creates the adapter over a chat tool.
		/// </summary>
		/// <param name="chatToolInfo">The chat descriptor.</param>
		/// <param name="executionContext">The caller-supplied context, or <see langword="null"/>.</param>
		public ChatAgentTool(ToolInfo chatToolInfo, IToolExecutionContext? executionContext)
		{
			ChatToolInfo = chatToolInfo;
			ExecutionContext = executionContext;

			ApprovalLevel = chatToolInfo.ApprovalLevel ?? ToolApprovalLevel.PolicyBased;
			DefaultExpectedBehaviour = chatToolInfo.DefaultExpectedBehaviour;
			PolicyMask = chatToolInfo.PolicyMask;
			SpecifierUnionMode = chatToolInfo.SpecifierUnionMode;
			SpecifierAggregationMode = chatToolInfo.SpecifierAggregationMode;
			Specifiers = chatToolInfo.Specifiers;
			SpecifierParameters = chatToolInfo.SpecifierParameters;
		}

		/// <inheritdoc/>
		public override SpecifierMatchResult AnalyzeSpecifier(Specifier specifier, JsonNode? args,
			IToolExecutionContext context) =>
			ChatToolInfo.Specifier?.Analyze(specifier, args, context) ?? SpecifierMatchResult.NoMatch;

		private ChatToolExecutionContext CreateContext()
		{
			if (ExecutionContext is ChatToolExecutionContext chatContext)
				return chatContext;

			var dummyChat = new Chat(ServiceRegistry.Provider);
			var dummyMessage = new AssistantMessage
			{
				CreatedAt = DateTime.Now,
				AgentStageId = Guid.Empty,
				SenderAgentId = Guid.Empty
			};
			dummyChat.Messages.Add(dummyMessage);

			var dummyToolCall = new DomainToolCall
			{
				ToolCallId = ToolCallId.Generate(),
				ToolName = ChatToolInfo.Name
			};
			dummyMessage.ToolCalls.Add(dummyToolCall);

			var parameters = new ToolContextCreationParams
			{
				Call = dummyToolCall,
				Descriptor = ChatToolInfo,
				Source = ToolInvocationSource.AgentTask,
				Host = dummyMessage,
				RunningInUi = false,
				Confirmation = new RejectingConfirmationSink()
			};

			return new ChatToolExecutionContext(dummyChat, dummyMessage, parameters, null);
		}

		private static ImmutableList<AgentAttachment> ReadAttachments(
			LLMDesktopAssistant.Tools.Execution.Domain.IToolCall call)
		{
			if (call is not DomainToolCall chatCall)
				return [];

			return [.. chatCall.AdditionalData
				.OfType<INativeChatAttachment>()
				.Select(a => AgentNativeAttachment.TryFromNative(a.Native))
				.Where(a => a is not null)!];
		}

		/// <inheritdoc/>
		public override async Task<AgentToolCallPreResult> PreExecuteAsync(JsonNode? arguments,
			CancellationToken cancellationToken = default)
		{
			var ctx = CreateContext();
			var preview = ChatToolInfo.Preview;

			if (preview is null)
			{
				return new AgentToolCallPreResult
				{
					ExpectedBehaviour = ChatToolInfo.DefaultExpectedBehaviour,
					SharedContext = ctx
				};
			}

			var result = await preview.PreviewAsync(arguments, ctx, cancellationToken);

			return new AgentToolCallPreResult
			{
				InterruptingSuccess = result.InterruptingSuccess,
				InterruptingContent = result.InterruptingSuccess is not null ? ctx.Call.ResultContent : null,
				InterruptingAttachments = ReadAttachments(ctx.Call),
				ExpectedBehaviour = result.ExpectedBehaviour ?? ChatToolInfo.DefaultExpectedBehaviour,
				SharedContext = ctx
			};
		}

		/// <inheritdoc/>
		public override async Task<AgentToolCallResult> ExecuteAsync(JsonNode? arguments, object? sharedContext,
			CancellationToken cancellationToken = default)
		{
			var ctx = sharedContext as IToolExecutionContext ?? CreateContext();
			var result = await ChatToolInfo.Main.ExecuteAsync(arguments, ctx, cancellationToken);

			return new AgentToolCallResult
			{
				Success = result.Success,
				Content = ctx.Call.ResultContent ?? string.Empty,
				Attachments = ReadAttachments(ctx.Call)
			};
		}

		/// <summary>
		/// The task executor owns confirmation; this sink is never expected to be reached.
		/// </summary>
		private sealed class RejectingConfirmationSink : IConfirmationSink
		{
			public Task<ToolApprovalResult> RequestAsync(IToolExecutionContext ctx, CancellationToken ct) =>
				Task.FromResult(ToolApprovalResult.DeniedOnce(
					"The agent task handles confirmation outside the tool."));
		}
	}
}
