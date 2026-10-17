using System.Collections.Immutable;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Specifiers;

namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// The description of a call whose tool does not exist. Keeping a descriptor instead of a
	/// <see langword="null"/> lets the whole pipeline run uniformly: the streaming stage drains the
	/// arguments and the main executor reports the miss.
	/// </summary>
	/// <remarks>
	/// The descriptor is a singleton because it has no per-call state; the tool name is read from the
	/// call in the executor. Every optional capability is absent, and the approval level is
	/// <see cref="ToolApprovalLevel.AlwaysApprove"/> — asking the user to approve a tool that does not
	/// exist would be meaningless.
	/// </remarks>
	public sealed class MissingToolDescriptor : IToolDescriptor
	{
		/// <summary>
		/// The shared instance.
		/// </summary>
		public static MissingToolDescriptor Instance { get; } = new();

		private MissingToolDescriptor()
		{
		}

		/// <summary>
		/// Empty: the name that could not be found lives on the call.
		/// </summary>
		public string Name => string.Empty;

		/// <summary>
		/// A description that explains the synthetic nature of the descriptor.
		/// </summary>
		public string Description => "A tool that does not exist in the current toolset.";

		/// <summary>
		/// An open schema: the arguments are never validated for a tool that does not exist.
		/// </summary>
		public JsonObject ArgumentSchema { get; } = new()
		{
			["type"] = "object",
			["additionalProperties"] = true
		};

		/// <summary>
		/// No behaviour: the miss is never approved or denied by policy.
		/// </summary>
		public ToolBehaviour DefaultExpectedBehaviour => ToolBehaviour.None;

		/// <summary>
		/// Always approve: the pipeline must not ask about a tool that does not exist.
		/// </summary>
		public ToolApprovalLevel ApprovalLevel => ToolApprovalLevel.AlwaysApprove;

		/// <summary>
		/// No per-tool mask.
		/// </summary>
		public ToolPolicyMask? PolicyMask => null;

		/// <summary>
		/// No specifier rules.
		/// </summary>
		public ImmutableList<ToolSpecifierRule> Specifiers => [];

		/// <summary>
		/// No specifier union mode.
		/// </summary>
		public SpecifierBehaviourUnionMode? SpecifierUnionMode => null;

		/// <summary>
		/// The default aggregation mode; unused because there are no specifiers.
		/// </summary>
		public SpecifierAggregationMode SpecifierAggregationMode => SpecifierAggregationMode.Prioritized;

		/// <summary>
		/// No specifier parameters.
		/// </summary>
		public ImmutableList<string> SpecifierParameters => [];

		/// <summary>
		/// No static synchronization keys: a missing tool claims nothing.
		/// </summary>
		public ImmutableList<ToolSyncKey> StaticSyncKeys => [];

		/// <summary>
		/// The tool handles no decision itself.
		/// </summary>
		public ToolPolicyDecision HandledDecisions => ToolPolicyDecision.None;

		/// <summary>
		/// No approval-helper configuration.
		/// </summary>
		public ApprovalHelperConfig? ApprovalHelper => null;

		/// <summary>
		/// The executor that writes the "tool not found" error into the call.
		/// </summary>
		public IToolMainExecutor Main { get; } = new MissingMainExecutor();

		/// <summary>
		/// No preview.
		/// </summary>
		public IToolPreviewExecutor? Preview => null;

		/// <summary>
		/// No streaming analytics.
		/// </summary>
		public IToolStreamingAnalyzer? Streaming => null;

		/// <summary>
		/// No specifier analyzer.
		/// </summary>
		public IToolSpecifierAnalyzer? Specifier => null;

		/// <summary>
		/// No synchronization analyzer.
		/// </summary>
		public IToolSynchronizationAnalyzer? SyncAnalyzer => null;

		/// <summary>
		/// No approval provider.
		/// </summary>
		public IToolApprovalProvider? Approval => null;

		/// <summary>
		/// No factory: the dispatcher already built the context with this descriptor.
		/// </summary>
		public IToolExecutionContextFactory? ContextFactory => null;

		private sealed class MissingMainExecutor : IToolMainExecutor
		{
			public Task<ToolExecutionResult> ExecuteAsync(JsonNode? args, IToolExecutionContext ctx, CancellationToken ct)
			{
				ctx.Call.ResultContent = $"Error: Tool '{ctx.Call.ToolName}' not found.";
				return Task.FromResult(ToolExecutionResult.Failed);
			}
		}
	}
}
