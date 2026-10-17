using System.Collections.Immutable;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Executors;
using LLMDesktopAssistant.Tools.Specifiers;

namespace LLMDesktopAssistant.Agents.Tasks
{
	/// <summary>
	/// The agent-task world's tool descriptor. The abstract methods remain the authoring surface; the
	/// carried executors below adapt them onto <see cref="IToolDescriptor"/>, so the task path and the
	/// chat path speak the same core.
	/// </summary>
	/// <remarks>
	/// The adaptation is transitional: P5 re-expresses the task path on the shared orchestrator and the
	/// abstract methods can then move into plain executables. Until then the executor wrappers keep
	/// both worlds on one contract without duplicating the task loop.
	/// </remarks>
	public abstract class AgentTool : IToolDescriptor
	{
		/// <summary>
		/// The name of the tool. This should be a unique identifier for the tool within the agent's context.
		/// </summary>
		public abstract string Name { get; }

		/// <summary>
		/// The display name of the tool. This is used in user interfaces and should be human-readable.
		/// </summary>
		public abstract string DisplayName { get; }

		/// <summary>
		/// A brief description of what the tool does. This should be helpful for agents to understand how to use the tool.
		/// </summary>
		public abstract string Description { get; }

		/// <summary>
		/// A JSON schema describing the expected arguments for the tool.
		/// </summary>
		public abstract JsonObject ArgumentSchema { get; }

		/// <summary>
		/// The default expected behaviour for this tool.
		/// </summary>
		public ToolBehaviour DefaultExpectedBehaviour { get; init; } = ToolBehaviour.None;

		/// <summary>
		/// The level of approval required for this tool to be executed.
		/// </summary>
		public ToolApprovalLevel ApprovalLevel { get; init; } = ToolApprovalLevel.PolicyBased;

		/// <summary>
		/// The individual policy mask that overrides the agent's policy for this tool.
		/// Applied only for policy-based approval levels.
		/// </summary>
		public ToolPolicyMask? PolicyMask { get; init; } = null;

		/// <summary>
		/// The specifier behaviour union mode of the tool.
		/// Null indicates that the default mode is used.
		/// </summary>
		public SpecifierBehaviourUnionMode? SpecifierUnionMode { get; init; } = null;

		/// <summary>
		/// The specifier aggregation mode of the tool.
		/// </summary>
		public SpecifierAggregationMode SpecifierAggregationMode { get; init; } = SpecifierAggregationMode.Prioritized;

		/// <summary>
		/// The specifier rules of the tool.
		/// </summary>
		public ImmutableList<ToolSpecifierRule> Specifiers { get; init; } = [];

		/// <summary>
		/// A list of all possible specifier parameters that can be used with the tool.
		/// </summary>
		public ImmutableList<string> SpecifierParameters { get; init; } = [];

		/// <summary>
		/// The task tool has no declared synchronization keys yet; the task path keeps its own
		/// parallelism bound.
		/// </summary>
		public ImmutableList<ToolSyncKey> StaticSyncKeys => [];

		/// <summary>
		/// The task tool does not handle policy decisions itself in this phase.
		/// </summary>
		public ToolPolicyDecision HandledDecisions => ToolPolicyDecision.None;

		/// <summary>
		/// No approval-helper configuration on the task side yet.
		/// </summary>
		public ApprovalHelperConfig? ApprovalHelper => null;

		/// <summary>
		/// The task tool has no chat-scoped context factory: the task orchestrator supplies the provider.
		/// </summary>
		public IToolExecutionContextFactory? ContextFactory => null;

		/// <summary>
		/// No synchronization analyzer on the task side yet.
		/// </summary>
		public IToolSynchronizationAnalyzer? SyncAnalyzer => null;

		/// <summary>
		/// The task tool has no streaming argument analyzer: the task stream is only drained.
		/// </summary>
		public IToolStreamingAnalyzer? Streaming => null;

		/// <summary>
		/// No tool-owned approval interface on the task side yet.
		/// </summary>
		public IToolApprovalProvider? Approval => null;

		private IToolMainExecutor? _main;
		/// <summary>
		/// The main executor, adapted from <see cref="ExecuteAsync"/>.
		/// </summary>
		public IToolMainExecutor Main => _main ??= new DelegateMainExecutor(async (args, ctx, ct) =>
		{
			var result = await ExecuteAsync(args, ctx.SharedContext, ct);
			ctx.Call.ResultContent = result.Content;
			foreach (var attachment in result.Attachments)
				ProjectAttachment(ctx.Call, attachment);

			return result.Success ? ToolExecutionResult.Succeeded : ToolExecutionResult.Failed;
		});

		private IToolPreviewExecutor? _preview;
		/// <summary>
		/// The preview executor, adapted from <see cref="PreExecuteAsync"/>.
		/// </summary>
		public IToolPreviewExecutor Preview => _preview ??= new DelegatingPreviewExecutor(this);

		private IToolSpecifierAnalyzer? _specifier;
		/// <summary>
		/// The specifier analyzer, adapted from <see cref="AnalyzeSpecifier"/>.
		/// </summary>
		public IToolSpecifierAnalyzer Specifier => _specifier ??= new DelegatingSpecifierAnalyzer(this);

		/// <summary>
		/// Preview-executes the tool with the provided arguments to determine it's behaviour or interrupt the execution.
		/// </summary>
		/// <param name="arguments">The arguments to pass to the tool. This can be null if the tool does not require any arguments.</param>
		/// <param name="cancellationToken">A cancellation token to allow for graceful termination of the operation.</param>
		/// <returns>A task that represents the asynchronous operation and returns a preview result.</returns>
		public virtual Task<AgentToolCallPreResult> PreExecuteAsync(JsonNode? arguments, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(new AgentToolCallPreResult
			{
				ExpectedBehaviour = DefaultExpectedBehaviour
			});
		}

		/// <summary>
		/// Analyzes a specifier against the tool arguments and returns the match result.
		/// The default implementation always returns <see cref="SpecifierMatchResult.NoMatch"/>.
		/// </summary>
		/// <param name="specifier">The parsed specifier to analyze. Cannot be <see langword="null"/>.</param>
		/// <param name="args">The tool arguments, or <see langword="null"/>.</param>
		/// <param name="context">The tool execution context. Cannot be <see langword="null"/>.</param>
		/// <returns>The match result of the specifier against the arguments.</returns>
		public virtual SpecifierMatchResult AnalyzeSpecifier(Specifier specifier, JsonNode? args, IToolExecutionContext context)
		{
			return SpecifierMatchResult.NoMatch;
		}

		/// <summary>
		/// Executes the tool with the given parameters. The method should handle any necessary logic and return a result.
		/// </summary>
		/// <param name="arguments">The arguments to pass to the tool. This can be null if the tool does not require any arguments.</param>
		/// <param name="sharedContext">A context object that taken from preview execution result.</param>
		/// <param name="cancellationToken">A cancellation token to allow for graceful termination of the operation.</param>
		/// <returns>A task that represents the asynchronous operation and returns the result of the tool execution.</returns>
		public abstract Task<AgentToolCallResult> ExecuteAsync(JsonNode? arguments, object? sharedContext, CancellationToken cancellationToken = default);

		private static void ProjectAttachment(IToolCall call, AgentAttachment attachment)
		{
			var kind = attachment.Type switch
			{
				AgentAttachmentType.Image => ToolAttachmentKind.Image,
				AgentAttachmentType.Audio => ToolAttachmentKind.Audio,
				AgentAttachmentType.Video => ToolAttachmentKind.Video,
				_ => throw new ArgumentOutOfRangeException(nameof(attachment), attachment.Type, "Unknown attachment type.")
			};

			call.AddAttachment(new ToolAttachment { Kind = kind, Url = attachment.Url });
		}

		private sealed class DelegatingPreviewExecutor(AgentTool tool) : IToolPreviewExecutor
		{
			public async Task<PreviewToolExecutionResult> PreviewAsync(JsonNode? args, IToolExecutionContext ctx,
				CancellationToken ct)
			{
				var result = await tool.PreExecuteAsync(args, ct);
				ctx.SharedContext = result.SharedContext;

				if (result.InterruptingContent is not null)
					ctx.Call.ResultContent = result.InterruptingContent;

				foreach (var attachment in result.InterruptingAttachments)
					ProjectAttachment(ctx.Call, attachment);

				return new PreviewToolExecutionResult
				{
					InterruptingSuccess = result.InterruptingSuccess,
					ExpectedBehaviour = result.ExpectedBehaviour
				};
			}
		}

		private sealed class DelegatingSpecifierAnalyzer(AgentTool tool) : IToolSpecifierAnalyzer
		{
			public SpecifierMatchResult Analyze(Specifier specifier, JsonNode? args, IToolExecutionContext ctx) =>
				tool.AnalyzeSpecifier(specifier, args, ctx);
		}
	}
}
