using System.Text.Json.Nodes;
using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.Tools.Execution.Hooks
{
	/// <summary>
	/// What a <c>tool.pre-use</c> hook returns: its opinion about the invocation and, optionally, the
	/// argument set that replaces the model's.
	/// </summary>
	public sealed class ToolPreUseHookVerdict
	{
		/// <summary>
		/// The hook's opinion, or <see langword="null"/> for "no opinion".
		/// </summary>
		public ToolPolicyDecision? Decision { get; init; }

		/// <summary>
		/// Why the hook vetoed the invocation. It becomes the model-facing result, because settlement
		/// reports an empty result as "Tool failed with no result", which would tell the model the
		/// opposite of what happened. Required for a veto, ignored otherwise.
		/// </summary>
		public string? Reason { get; init; }

		/// <summary>
		/// The argument set that replaces the effective one for this invocation, or <see langword="null"/>
		/// to keep the model's. Whether to override effective or persistent arguments depends on the
		/// <see cref="OverridesModelArguments"/> flag.
		/// </summary>
		public JsonNode? Arguments { get; init; }

		/// <summary>
		/// Whether the <see cref="Arguments"/> property should override arguments in both chat storage
		/// ans model's intent or only ephemeral tool call arguments.
		/// </summary>
		public bool PersistsArguments { get; init; }
	}
}
