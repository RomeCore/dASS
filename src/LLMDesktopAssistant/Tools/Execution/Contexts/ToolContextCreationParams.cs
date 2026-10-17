using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Contexts
{
	/// <summary>
	/// Everything a concrete context needs, assembled by the orchestrator from the invocation source.
	/// </summary>
	/// <remarks>
	/// The orchestrator fills the source-specific values (host, sink, behaviours, stream) and the
	/// factory merges them with the descriptor's home scope. The factory never mutates the parameters;
	/// it only reads them.
	/// </remarks>
	public sealed class ToolContextCreationParams
	{
		/// <summary>
		/// The call being processed.
		/// </summary>
		public required IToolCall Call { get; init; }

		/// <summary>
		/// The descriptor, already substituted with <see cref="MissingToolDescriptor"/> when the tool does
		/// not exist. Never <see langword="null"/>.
		/// </summary>
		public required IToolDescriptor Descriptor { get; init; }

		/// <summary>
		/// The source that launched the invocation.
		/// </summary>
		public ToolInvocationSource Source { get; init; }

		/// <summary>
		/// The message that owns the sibling calls, when the source has one.
		/// </summary>
		public IToolCallHost? Host { get; init; }

		/// <summary>
		/// Whether a user interface is available.
		/// </summary>
		public bool RunningInUi { get; init; }

		/// <summary>
		/// The confirmation surface of the invoking world.
		/// </summary>
		public required IConfirmationSink Confirmation { get; init; }

		/// <summary>
		/// The effective behaviours of the invocation.
		/// </summary>
		public ToolPolicyMask EffectiveBehaviours { get; init; }

		/// <summary>
		/// The model's argument stream, when the invocation has one.
		/// </summary>
		public IToolArgumentStream? ArgumentStream { get; init; }
	}
}
