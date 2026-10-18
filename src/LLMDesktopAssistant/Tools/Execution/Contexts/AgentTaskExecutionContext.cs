using System.Text.Json.Nodes;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;

namespace LLMDesktopAssistant.Tools.Execution.Contexts
{
	/// <summary>
	/// The agent-task world's execution context. The chat is optional: a task may run without one.
	/// </summary>
	/// <remarks>
	/// The task context has no message-id helper and no chat message in the neutral contract. The
	/// concrete <see cref="Message"/> is exposed for the task orchestrator's plumbing only.
	/// </remarks>
	public sealed class AgentTaskExecutionContext : IToolExecutionContext
	{
		/// <summary>
		/// Creates the task context.
		/// </summary>
		/// <param name="parameters">The source-supplied invocation inputs.</param>
		/// <param name="chat">The triggered chat, or <see langword="null"/>.</param>
		/// <param name="services">The task's service provider (triggered chat scope or the root).</param>
		/// <param name="consent">The consent memorization context, or <see langword="null"/>.</param>
		public AgentTaskExecutionContext(ToolContextCreationParams parameters, Chat? chat, IServiceProvider services,
			ToolConsentMemorizationContext? consent = null)
		{
			Call = parameters.Call;
			Descriptor = parameters.Descriptor;
			HandledDecisions = parameters.Descriptor.HandledDecisions;
			Host = parameters.Host;
			Confirmation = parameters.Confirmation;
			Chat = chat;
			Services = services;
			Consent = consent;
			Source = parameters.Source;
			RunningInUi = parameters.RunningInUi;
			EffectiveBehaviours = parameters.EffectiveBehaviours;
			ArgumentStream = parameters.ArgumentStream;
			Arguments = new ToolArgumentsAccessor(Call, () => ArgumentsOverride);
			Message = parameters.Host as AgentAssistantMessage;
		}

		/// <summary>
		/// The task's assistant message, when the host is one.
		/// </summary>
		public AgentAssistantMessage? Message { get; }

		/// <summary>
		/// The call being processed.
		/// </summary>
		public IToolCall Call { get; }

		/// <summary>
		/// The tool being executed.
		/// </summary>
		public IToolDescriptor Descriptor { get; }

		/// <summary>
		/// The message that owns the sibling calls.
		/// </summary>
		public IToolCallHost? Host { get; }

		/// <summary>
		/// The task world's confirmation surface.
		/// </summary>
		public IConfirmationSink Confirmation { get; }

		/// <summary>
		/// The tool's own channel between its stages.
		/// </summary>
		public object? SharedContext { get; set; }

		/// <summary>
		/// The triggered chat, or <see langword="null"/> when the task was not launched from one.
		/// </summary>
		public Chat? Chat { get; }

		/// <summary>
		/// The task's service provider. <c>[Inject]</c> parameters are resolved from here.
		/// </summary>
		public IServiceProvider Services { get; }

		/// <summary>
		/// Lets the tool persist its consent decisions, or <see langword="null"/>.
		/// </summary>
		public ToolConsentMemorizationContext? Consent { get; }

		/// <summary>
		/// The source that launched the invocation.
		/// </summary>
		public ToolInvocationSource Source { get; }

		/// <summary>
		/// Whether a user interface is available.
		/// </summary>
		public bool RunningInUi { get; }

		/// <summary>
		/// The pipeline's decision.
		/// </summary>
		public ToolPolicyDecision PolicyDecision { get; set; } = ToolPolicyDecision.None;

		/// <summary>
		/// Which decisions the tool handles itself for this invocation; initialized from the descriptor
		/// and overridable by preview.
		/// </summary>
		public ToolPolicyDecision HandledDecisions { get; set; }

		/// <summary>
		/// The decision's source and explanation.
		/// </summary>
		public ToolPolicyReason? PolicyReason { get; set; }

		/// <summary>
		/// The data the approval surface collected, or <see langword="null"/>.
		/// </summary>
		public JsonNode? ApprovalData { get; set; }

		/// <summary>
		/// The effective behaviours of the invocation.
		/// </summary>
		public ToolPolicyMask EffectiveBehaviours { get; }

		/// <summary>
		/// The model's argument stream, when the invocation has one.
		/// </summary>
		public IToolArgumentStream? ArgumentStream { get; }

		/// <summary>
		/// An ephemeral argument substitution — the hook channel.
		/// </summary>
		public JsonNode? ArgumentsOverride { get; set; }

		/// <summary>
		/// The effective-arguments accessor shared by every stage.
		/// </summary>
		public IToolArgumentsAccessor Arguments { get; }
	}
}
