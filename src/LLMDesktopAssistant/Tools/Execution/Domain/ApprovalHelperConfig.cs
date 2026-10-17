namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// How the agentic approval helper behaves for a tool.
	/// </summary>
	public enum ApprovalHelperMode
	{
		/// <summary>
		/// The helper is off; the user decides alone.
		/// </summary>
		Manual,

		/// <summary>
		/// The helper analyses the call and advises the user, who still decides.
		/// </summary>
		Assistant,

		/// <summary>
		/// The helper decides on its own unless it lacks context; then it escalates to the user.
		/// </summary>
		Worker
	}

	/// <summary>
	/// Per-tool configuration of the agentic approval helper. The helper itself is a later phase; the
	/// descriptor already carries the configuration so the contract does not change when it lands.
	/// </summary>
	public sealed class ApprovalHelperConfig
	{
		/// <summary>
		/// How much the helper is allowed to decide. Defaults to <see cref="ApprovalHelperMode.Manual"/>.
		/// </summary>
		public ApprovalHelperMode Mode { get; init; } = ApprovalHelperMode.Manual;

		/// <summary>
		/// How long the helper waits before it starts analysing, so it does not spend a model call on a
		/// confirmation the user is about to answer anyway.
		/// </summary>
		public TimeSpan LeadDelay { get; init; }

		/// <summary>
		/// How long the helper waits after the last change before it finalises its verdict.
		/// </summary>
		public TimeSpan SettleDelay { get; init; }

		/// <summary>
		/// Extra instructions appended to the helper's prompt for this tool, or <see langword="null"/>.
		/// </summary>
		public string? ExtraPrompt { get; init; }
	}
}
