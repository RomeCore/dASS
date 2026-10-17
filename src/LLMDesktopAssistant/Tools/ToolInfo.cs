using System.Collections.Immutable;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Executors;
using LLMDesktopAssistant.Tools.Specifiers;
using RCLargeLanguageModels.Tools;

namespace LLMDesktopAssistant.Tools
{
	/// <summary>
	/// The chat world's tool descriptor. It carries its six executors — built once from the module's
	/// methods — so a chat tool can run in an agent task by handing this descriptor over unchanged.
	/// </summary>
	/// <remarks>
	/// Tools used to live in two parallel worlds: plain <see cref="ToolInfo"/> produced by tool modules,
	/// and the separately managed "meta-tools" (the <c>Tools/Meta/*</c> subsystem, born in d859a35 on
	/// 2026-04-09 and buried on 2026-09-12). There is only one world now - every tool is an addon,
	/// possibly with a scripting engine behind it. See the epitaph in
	/// <c>Tools/Scripting/ScriptableToolParser.cs</c>.
	/// </remarks>
	public class ToolInfo : AddonChangedBase<ToolInfo, ToolChange>, IToolDescriptor
	{
		private JsonObject? _argumentSchema;
		/// <summary>
		/// A JSON object that defines the schema of the arguments for the tool.
		/// </summary>
		public JsonObject ArgumentSchema
		{
			get => _argumentSchema ??= new JsonObject
			{
				["type"] = "object",
				["additionalProperties"] = false
			};
			set => SetProperty(ref _argumentSchema, value);
		}

		/// <summary>
		/// Whether the tool result can be compacted by the context checkpoint machinery.
		/// </summary>
		public bool CanBeCompacted
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// A JSON object that defines the schema of the structured output for the tool.
		/// Can be null if tool does not produces structured output.
		/// </summary>
		public JsonObject? OutputSchema
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Gets a <see cref="FunctionTool"/> instance that represents this tool.
		/// Used for API registration purposes; it cannot be executed directly.
		/// </summary>
		public FunctionTool NativeTool => new(Name, Description, ArgumentSchema,
			(_, _) => throw new NotSupportedException(
				"This function tool is not meant to be executed directly. Use the main executor instead."));

		private IToolMainExecutor? _main;
		/// <summary>
		/// The main executor. Always present: a tool without a bound executor reports the broken addon
		/// instead of throwing.
		/// </summary>
		public IToolMainExecutor Main
		{
			get => _main ??= DelegateMainExecutor.FromResult((_, ctx, _) =>
			{
				ctx.Call.ResultContent = $"Tool '{Name}' has no executor (it is probably a broken addon file).";
				return ToolExecutionResult.Failed;
			});
			set => SetProperty(ref _main, value);
		}

		private IToolPreviewExecutor? _preview;
		/// <summary>
		/// The preview executor, or <see langword="null"/> when the tool has no preview.
		/// </summary>
		public IToolPreviewExecutor? Preview
		{
			get => _preview;
			set => SetProperty(ref _preview, value);
		}

		private IToolStreamingAnalyzer? _streaming;
		/// <summary>
		/// The streaming analyzer, or <see langword="null"/> when partial arguments are not analysed.
		/// </summary>
		public IToolStreamingAnalyzer? Streaming
		{
			get => _streaming;
			set => SetProperty(ref _streaming, value);
		}

		private IToolSpecifierAnalyzer? _specifier;
		/// <summary>
		/// The specifier analyzer, or <see langword="null"/> when the tool declares no specifiers.
		/// </summary>
		public IToolSpecifierAnalyzer? Specifier
		{
			get => _specifier;
			set => SetProperty(ref _specifier, value);
		}

		private IToolSynchronizationAnalyzer? _syncAnalyzer;
		/// <summary>
		/// The synchronization analyzer that derives per-invocation keys from the arguments, or
		/// <see langword="null"/> when only the static keys apply.
		/// </summary>
		public IToolSynchronizationAnalyzer? SyncAnalyzer
		{
			get => _syncAnalyzer;
			set => SetProperty(ref _syncAnalyzer, value);
		}

		private IToolApprovalProvider? _approval;
		/// <summary>
		/// The tool's own approval interface, or <see langword="null"/> when the generic consent panel
		/// handles the decision.
		/// </summary>
		public IToolApprovalProvider? Approval
		{
			get => _approval;
			set => SetProperty(ref _approval, value);
		}

		private IToolExecutionContextFactory? _contextFactory;
		/// <summary>
		/// Creates the context for this tool's home scope. Stamped on the per-chat clone before the
		/// addon is frozen.
		/// </summary>
		public IToolExecutionContextFactory? ContextFactory
		{
			get => _contextFactory;
			set => SetProperty(ref _contextFactory, value);
		}

		/// <summary>
		/// Gets or sets the default expected behaviour of the tool.
		/// </summary>
		public ToolBehaviour DefaultExpectedBehaviour
		{
			get => field;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The names of the arguments a specifier is matched against.
		/// </summary>
		public ImmutableList<string> SpecifierParameters
		{
			get => field;
			set => SetProperty(ref field, value);
		} = [];

		/// <summary>
		/// Which policy decisions the tool handles itself instead of the confirmation service.
		/// </summary>
		public ToolPolicyDecision HandledDecisions
		{
			get => field;
			set => SetProperty(ref field, value);
		}

		private ToolApprovalLevel? _approvalLevel;
		/// <summary>
		/// A value indicating whether the tool requires user confirmation before execution.
		/// By default, uses the agent's default approval level.
		/// </summary>
		public ToolApprovalLevel? ApprovalLevel
		{
			get => _approvalLevel;
			set => SetProperty(ref _approvalLevel, value);
		}

		ToolApprovalLevel IToolDescriptor.ApprovalLevel => ApprovalLevel ?? ToolApprovalLevel.PolicyBased;

		private ToolPolicyMask? _policyMask;
		/// <summary>
		/// The individual policy mask that overrides the agent's policy for this tool.
		/// <see cref="ToolPolicyMask.DisallowedBehaviours"/> always disallow the tool,
		/// <see cref="ToolPolicyMask.AutoApproveBehaviours"/> always approve it.
		/// Applied only for policy-based approval levels.
		/// </summary>
		public ToolPolicyMask? PolicyMask
		{
			get => _policyMask;
			set => SetProperty(ref _policyMask, value);
		}

		private ImmutableList<ToolSyncKey> _staticSyncKeys = [];
		/// <summary>
		/// Tool-wide synchronization keys, claimed by every invocation of the tool.
		/// </summary>
		public ImmutableList<ToolSyncKey> StaticSyncKeys
		{
			get => _staticSyncKeys;
			set => SetProperty(ref _staticSyncKeys, value);
		}

		private ApprovalHelperConfig? _approvalHelper;
		/// <summary>
		/// The approval-helper configuration, or <see langword="null"/> when the helper is not configured.
		/// </summary>
		public ApprovalHelperConfig? ApprovalHelper
		{
			get => _approvalHelper;
			set => SetProperty(ref _approvalHelper, value);
		}

		private SpecifierBehaviourUnionMode? _specifierUnionMode;
		/// <summary>
		/// The specifier behaviour union mode of the tool.
		/// Null indicates that the default mode (<see cref="SpecifierBehaviourUnionMode.CombineSoft"/>) is used.
		/// </summary>
		public SpecifierBehaviourUnionMode? SpecifierUnionMode
		{
			get => _specifierUnionMode;
			set => SetProperty(ref _specifierUnionMode, value);
		}

		private SpecifierAggregationMode _specifierAggregationMode = SpecifierAggregationMode.Prioritized;
		/// <summary>
		/// The specifier aggregation mode of the tool.
		/// </summary>
		public SpecifierAggregationMode SpecifierAggregationMode
		{
			get => _specifierAggregationMode;
			set => SetProperty(ref _specifierAggregationMode, value);
		}

		private ImmutableList<ToolSpecifierRule> _specifiers = [];
		/// <summary>
		/// The specifier rules of the tool.
		/// </summary>
		public ImmutableList<ToolSpecifierRule> Specifiers
		{
			get => _specifiers;
			set => SetProperty(ref _specifiers, value);
		}

		/// <summary>
		/// The source of the tool. Defaults to "native".
		/// </summary>
		public ToolSource ToolSource
		{
			get; 
			set => SetProperty(ref field, value);
		} = ToolSource.Native;

		/// <summary>
		/// The language of the script if the tool based on script.
		/// </summary>
		public ScriptLanguageType? ScriptLanguage
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Creates a new instance of the <see cref="ToolInfo"/> class with the specified executor and initialization information.
		/// </summary>
		/// <param name="info">The initialization information for the tool. This includes various properties such as name, description, and category.</param>
		/// <returns>The newly created, frozen <see cref="ToolInfo"/> instance.</returns>
		/// <exception cref="InvalidOperationException">Thrown when the description getter is not provided in the initialization information.</exception>
		public static ToolInfo Create(ToolInitializationInfo info)
		{
			ToolName.EnsureValid(info.Name);

			if (info.DescriptionGetter == null)
				throw new InvalidOperationException(
					$"Description of the {nameof(ToolInitializationInfo)} must be set before tool creation.");

			var (argumentSchema, parameters) = ToolMethodBinder.Describe(info.Executor);

			var main = ToolMethodBinder.BindMain(info.Executor.Target, info.Executor.Method);
			var preview = info.PreviewExecutor is { } previewDelegate
				? ToolMethodBinder.BindPreview(previewDelegate.Target, previewDelegate.Method, parameters)
				: null;
			var streaming = info.StreamingAnalyzer is { } streamingDelegate
				? ToolMethodBinder.BindStreaming(streamingDelegate.Target, streamingDelegate.Method, parameters)
				: null;
			var specifier = info.SpecifierAnalyzer is { } specifierDelegate
				? ToolMethodBinder.BindSpecifier(specifierDelegate.Target, specifierDelegate.Method, parameters)
				: null;
			var syncAnalyzer = info.SyncAnalyzer is { } syncDelegate
				? ToolMethodBinder.BindSyncAnalyzer(syncDelegate.Target, syncDelegate.Method, parameters)
				: null;
			var approval = info.Approval is { } approvalDelegate
				? ToolMethodBinder.BindApproval(approvalDelegate.Target, approvalDelegate.Method,
					info.ApprovalRequestedDataSchema, parameters)
				: null;

			info.ModifyArgumentSchema?.Invoke(argumentSchema);

			var tool = info.Clone();

			tool.ArgumentSchema = argumentSchema;
			tool.Main = main;
			tool.Preview = preview;
			tool.Streaming = streaming;
			tool.Specifier = specifier;
			tool.SyncAnalyzer = syncAnalyzer;
			tool.Approval = approval;

			tool.Freeze();

			return tool;
		}
	}
}
