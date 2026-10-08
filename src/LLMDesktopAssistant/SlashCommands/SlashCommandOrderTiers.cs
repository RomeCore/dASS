namespace LLMDesktopAssistant.SlashCommands
{
	/// <summary>
	/// The source-priority tiers encoded in the <c>OverrideOrder</c> of a command: when a name is contended, the
	/// command from the higher tier wins.
	/// </summary>
	public static class SlashCommandOrderTiers
	{
		/// <summary>
		/// Commands derived from other addons (skills, sub-agents, tools).
		/// </summary>
		public const int Derived = 0;

		/// <summary>
		/// Commands provided by scripts (reserved).
		/// </summary>
		public const int Scriptable = 1000;

		/// <summary>
		/// Built-in native commands (reserved).
		/// </summary>
		public const int Native = 2000;

		/// <summary>
		/// Maps a source kind onto its tier: native commands beat scriptable ones, which beat derived ones (skills,
		/// sub-agents, tools). An unknown source is treated as derived.
		/// </summary>
		public static int ForSource(SlashCommandSource source) => source switch
		{
			SlashCommandSource.Native => Native,
			SlashCommandSource.Script => Scriptable,
			_ => Derived
		};
	}
}
