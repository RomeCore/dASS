using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Tools.Execution.Hooks
{
	/// <summary>
	/// What a <c>tool.confirm.{source}.{pending}</c> hook returns: the answer given in the user's place and,
	/// optionally, the argument set the call should run with.
	/// </summary>
	/// <remarks>
	/// A <see langword="null"/> decision keeps the pipeline's own one. A refusal is final - it asks neither
	/// the user nor the tool's approval surface - while an approval or an ask replaces the pending decision,
	/// which needs the layer named in <see cref="Override"/> when that decision was a denial.
	/// </remarks>
	public sealed class ToolConfirmHookVerdict
	{
		/// <summary>
		/// The hook's answer, or <see langword="null"/> for "no opinion".
		/// </summary>
		public ToolHookDecision? Decision { get; init; }

		/// <summary>
		/// Why the hook answered this way; it becomes the model-facing result of a refusal.
		/// </summary>
		public string? Reason { get; init; }

		/// <summary>
		/// The argument set that replaces the effective one, or <see langword="null"/> to keep the model's.
		/// </summary>
		public JsonNode? Arguments { get; init; }

		/// <summary>
		/// Whether <see cref="Arguments"/> replaces the model's persisted intent as well.
		/// </summary>
		public bool PersistsArguments { get; init; }

		/// <summary>
		/// The deepest layer this verdict may pierce. Defaults to <see cref="ConfirmOverride.Policy"/>.
		/// </summary>
		public ConfirmOverride Override { get; init; }
	}
}
