using LLMDesktopAssistant.Tools.Execution.Contexts;

namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// Everything an entry point hands to the dispatcher to start one invocation.
	/// </summary>
	/// <remarks>
	/// <see cref="Descriptor"/> is optional on purpose - a call whose
	/// tool has disappeared from the toolset still has to be processed, and the dispatcher substitutes
	/// <see cref="MissingToolDescriptor"/>.
	/// </remarks>
	public sealed class ToolInvocationRequest
	{
		/// <summary>
		/// Which orchestrator runs the invocation. The source also decides the streaming mode and how
		/// much of the approval pipeline is applied.
		/// </summary>
		public required ToolInvocationSource Source { get; init; }

		/// <summary>
		/// The call being processed. It carries the arguments, the status and - after the run - the
		/// result, so it is the only object the caller needs to observe.
		/// </summary>
		public required IToolCall Call { get; init; }

		/// <summary>
		/// The tool, or <see langword="null"/> when it does not exist. The dispatcher keeps the pipeline
		/// uniform by substituting <see cref="MissingToolDescriptor"/>.
		/// </summary>
		public IToolDescriptor? Descriptor { get; init; }

		/// <summary>
		/// The confirmation surface of the invoking world. The dispatcher passes it into the context
		/// factory; the stage services never construct a sink themselves.
		/// </summary>
		public required IConfirmationSink Confirmation { get; init; }

		/// <summary>
		/// The factory that builds the invocation context. It is resolved by the entry point, because the
		/// world a call belongs to is a property of the entry point, not of the source tag: the chat entry
		/// passes the descriptor's stamped factory (or the chat fallback), a script passes
		/// <c>ScriptToolContextFactory</c>, a task passes <c>TaskToolContextFactory</c>.
		/// </summary>
		public required IToolExecutionContextFactory ContextFactory { get; init; }

		/// <summary>
		/// The message that owns the sibling calls, when there is one. Cascade and the approval helper
		/// read it; the core does not need anything else from the host.
		/// </summary>
		public IToolCallHost? Host { get; init; }

		/// <summary>
		/// The model's argument stream, when the invocation has one. A restart, a command or a script
		/// passes <see langword="null"/> and the streaming stage becomes a no-op.
		/// </summary>
		public IToolArgumentStream? ArgumentStream { get; init; }

		/// <summary>
		/// The effective behaviours of this invocation, assembled by the source (the agent policy, the
		/// task's launch parameters, or the trust model of a user action).
		/// </summary>
		public ToolPolicyMask EffectiveBehaviours { get; init; }

		/// <summary>
		/// Whether a user interface is available. A task or a script that runs headless sets this to
		/// <see langword="false"/>.
		/// </summary>
		public bool RunningInUi { get; init; }
	}
}
