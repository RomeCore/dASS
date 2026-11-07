using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Tools.Execution.Hooks
{
	/// <summary>
	/// What a <c>tool.start.{source}</c> hook returns: its opinion about the invocation and, optionally, the
	/// argument set that replaces the model's.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A start hook can only refuse or let the call through: it runs before the policy stage, so
	/// <see cref="ToolHookDecision.Allow"/> and <see cref="ToolHookDecision.Ask"/> both mean "no objection"
	/// and nothing more. The difference between the two refusals is what the user is told -
	/// <see cref="ToolHookDecision.Disallow"/> records a turned-down call, <see cref="ToolHookDecision.Error"/>
	/// records a call that could not be carried out.
	/// </para>
	/// <para>
	/// The verdict is a chain value, and the chain replaces rather than merges: each sequential hook gets
	/// the previous verdict as <c>previous</c> and its return value becomes the new one in full. A hook
	/// that has nothing to add must therefore return <c>previous</c> untouched (or nothing at all, which
	/// means "no opinion"); returning an empty document resets whatever an earlier hook decided.
	/// </para>
	/// </remarks>
	public sealed class ToolPreUseHookVerdict
	{
		/// <summary>
		/// The hook's opinion, or <see langword="null"/> for "no opinion".
		/// </summary>
		public ToolHookDecision? Decision { get; init; }

		/// <summary>
		/// Why the hook refused the invitation. It becomes the model-facing result, because settlement
		/// reports an empty result as "Tool failed with no result", which would tell the model the
		/// opposite of what happened. Required for a refusal, ignored otherwise.
		/// </summary>
		public string? Reason { get; init; }

		/// <summary>
		/// The argument set that replaces the effective one for this invocation, or <see langword="null"/>
		/// to keep the model's. Whether it overrides the persisted intent or only the ephemeral arguments
		/// depends on <see cref="PersistsArguments"/>.
		/// </summary>
		public JsonNode? Arguments { get; init; }

		/// <summary>
		/// Whether <see cref="Arguments"/> replaces the model's persisted intent as well, or only the
		/// ephemeral argument set the tool runs with.
		/// </summary>
		public bool PersistsArguments { get; init; }
	}
}
