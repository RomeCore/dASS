using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Tools.Consents;

namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// The per-invocation context every tool and every stage service takes. It owns the mutable state
	/// of one invocation; the stage services themselves are stateless.
	/// </summary>
	/// <remarks>
	/// A context is created once per invocation from <see cref="ToolInvocationRequest"/> through the
	/// descriptor's factory. The chat-specific message and message id do not live here - a chat tool
	/// reaches them by casting the concrete context.
	/// </remarks>
	public interface IToolExecutionContext
	{
		/// <summary>
		/// The call being processed. It is the single place the tool writes its output into.
		/// </summary>
		IToolCall Call { get; }

		/// <summary>
		/// The tool being executed. Never <see langword="null"/>: a missing tool is represented by
		/// <see cref="MissingToolDescriptor"/>, so no stage has to null-check it.
		/// </summary>
		IToolDescriptor Descriptor { get; }

		/// <summary>
		/// The message that owns the sibling calls - used by cascade and the approval helper - or
		/// <see langword="null"/> when the invocation has no message host.
		/// </summary>
		IToolCallHost? Host { get; }

		/// <summary>
		/// The confirmation surface of the invoking world: the confirmation stage asks the user through it
		/// and receives the decision together with the memorization. Never <see langword="null"/>.
		/// </summary>
		IConfirmationSink Confirmation { get; }

		/// <summary>
		/// The tool's own channel between its stages (for example, an artifact pre-computed by preview
		/// and committed by execution), or <see langword="null"/>.
		/// </summary>
		object? SharedContext { get; set; }

		/// <summary>
		/// The chat the invocation belongs to, or <see langword="null"/> for a task that was not launched
		/// from a chat.
		/// </summary>
		Chat? Chat { get; }

		/// <summary>
		/// The most-scoped service provider available to this invocation - the chat scope, the triggered
		/// chat's scope, or the root. <c>[Inject]</c> parameters are resolved from here.
		/// </summary>
		IServiceProvider Services { get; }

		/// <summary>
		/// Lets a tool persist its consent decisions, or <see langword="null"/> when the invocation cannot
		/// memorize.
		/// </summary>
		ToolConsentMemorizationContext? Consent { get; }

		/// <summary>
		/// The source that launched the invocation. It selects the orchestrator and the amount of
		/// approval the invocation goes through.
		/// </summary>
		ToolInvocationSource Source { get; }

		/// <summary>
		/// Whether a user interface is available. A tool must not ask for interaction when this is
		/// <see langword="false"/>.
		/// </summary>
		bool RunningInUi { get; }

		/// <summary>
		/// The pipeline's decision. Written by the policy stage, read by confirmation and by a tool that
		/// handles the decision itself.
		/// </summary>
		ToolPolicyDecision PolicyDecision { get; set; }

		/// <summary>
		/// Which decisions the tool handles itself for this invocation. Initialized from
		/// <see cref="IToolDescriptor.HandledDecisions"/> and overridden by the preview stage when it
		/// returns <see cref="PreviewToolExecutionResult.HandledDecisions"/>.
		/// </summary>
		ToolPolicyDecision HandledDecisions { get; set; }

		/// <summary>
		/// The decision's source and explanation, or <see langword="null"/> when no decision was made.
		/// </summary>
		ToolPolicyReason? PolicyReason { get; set; }

		/// <summary>
		/// The data the approval surface collected for this invocation, or <see langword="null"/>. Written
		/// by the confirmation stage from <see cref="ToolApprovalResult.ApprovalData"/> and read by the tool.
		/// </summary>
		JsonNode? ApprovalData { get; set; }

		/// <summary>
		/// The effective behaviours of this invocation, set at creation from the source.
		/// </summary>
		ToolPolicyMask EffectiveBehaviours { get; }

		/// <summary>
		/// The neutral argument stream, when the invocation has one. Drained by the streaming stage.
		/// </summary>
		IToolArgumentStream? ArgumentStream { get; }

		/// <summary>
		/// An ephemeral argument substitution - the hook channel. It is validated at install and never
		/// persisted.
		/// </summary>
		JsonNode? ArgumentsOverride { get; set; }

		/// <summary>
		/// The single accessor every stage reads the effective arguments through.
		/// </summary>
		IToolArgumentsAccessor Arguments { get; }
	}
}
