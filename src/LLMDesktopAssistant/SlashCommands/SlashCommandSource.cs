namespace LLMDesktopAssistant.SlashCommands
{
	/// <summary>
	/// The kind of source a slash command originates from.
	/// </summary>
	/// <remarks>
	/// Descriptive: it says *what* a command comes from (a skill, a sub-agent, a script, ...) and drives the source
	/// chip of the card and the command fingerprint, while the ordering authority stays
	/// <see cref="AddonChangedBase{Self,TChange}.OverrideOrder"/>, derived from it through
	/// <see cref="SlashCommandOrderTiers.ForSource"/>.
	/// </remarks>
	public enum SlashCommandSource
	{
		/// <summary>The source is unknown: a command with no source, or a token that did not resolve.</summary>
		Unknown = 0,

		/// <summary>A built-in native command.</summary>
		Native = 1,

		/// <summary>A command provided by a script.</summary>
		Script = 2,

		/// <summary>A command derived from a skill.</summary>
		Skill = 3,

		/// <summary>A command derived from a sub-agent.</summary>
		SubAgent = 4,

		/// <summary>A command derived from a tool.</summary>
		Tool = 5
	}
}
