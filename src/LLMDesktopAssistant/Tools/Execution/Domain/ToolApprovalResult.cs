using System.Text.Json.Nodes;
using LLMDesktopAssistant.Tools.Consents;

namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// The decision of the confirmation step, together with the memorization the user selected.
	/// </summary>
	/// <remarks>
	/// The result carries its own memorization on purpose: the confirmation step memorizes exactly what
	/// the user chose, without a second value travelling beside it.
	/// </remarks>
	public sealed class ToolApprovalResult
	{
		/// <summary>
		/// Whether the user approved the invocation.
		/// </summary>
		public required bool Approved { get; init; }

		/// <summary>
		/// How long the decision is remembered. <see cref="ToolApprovalMemorization.Once"/> memorizes
		/// nothing.
		/// </summary>
		public required ToolApprovalMemorization Memorization { get; init; }

		/// <summary>
		/// A note the user attached to the decision, or <see langword="null"/>. It is appended to the
		/// result the model sees.
		/// </summary>
		public string? Notes { get; init; }

		/// <summary>
		/// Substitutes the effective arguments for this invocation, or <see langword="null"/> to keep the
		/// arguments the model emitted. The channel is selected by <see cref="OverrideModelIntent"/>; the
		/// confirmation stage validates the resulting set before execution.
		/// </summary>
		public JsonNode? EditedArguments { get; init; }

		/// <summary>
		/// When <see langword="true"/>, <see cref="EditedArguments"/> replaces the persisted
		/// <c>call.Arguments</c> (the model's intent is substituted). When <see langword="false"/>, the edit
		/// goes to the ephemeral <c>ctx.ArgumentsOverride</c> (the hook channel).
		/// </summary>
		public bool OverrideModelIntent { get; init; }

		/// <summary>
		/// The data the approval surface collected according to the provider's requested schema, or
		/// <see langword="null"/>. Delivered on <c>ctx.ApprovalData</c>.
		/// </summary>
		public JsonNode? ApprovalData { get; init; }

		/// <summary>
		/// Creates an approval that is not remembered.
		/// </summary>
		/// <param name="notes">An optional note for the model.</param>
		public static ToolApprovalResult ApprovedOnce(string? notes = null) => new()
		{
			Approved = true,
			Memorization = ToolApprovalMemorization.Once,
			Notes = notes
		};

		/// <summary>
		/// Creates a denial that is not remembered.
		/// </summary>
		/// <param name="notes">An optional note for the model.</param>
		public static ToolApprovalResult DeniedOnce(string? notes = null) => new()
		{
			Approved = false,
			Memorization = ToolApprovalMemorization.Once,
			Notes = notes
		};
	}
}
