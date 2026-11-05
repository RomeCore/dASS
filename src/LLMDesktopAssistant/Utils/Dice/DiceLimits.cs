namespace LLMDesktopAssistant.Utils.Dice
{
	/// <summary>
	/// The hard caps that keep a dice expression from turning into a long-running computation.
	/// </summary>
	public static class DiceLimits
	{
		/// <summary>
		/// The maximum number of dice a single group may roll.
		/// </summary>
		public const int MaxDicePerGroup = 1000;

		/// <summary>
		/// The maximum number of dice in a group's pool once explosions and rerolls are accounted for.
		/// </summary>
		public const int MaxDicePerPool = 1000;

		/// <summary>
		/// The maximum number of faces a die may have.
		/// </summary>
		public const int MaxSides = 100000;

		/// <summary>
		/// The maximum number of dice groups and constant terms a single expression may contain.
		/// </summary>
		public const int MaxTerms = 32;

		/// <summary>
		/// The maximum number of times the whole expression may be rolled in one call.
		/// </summary>
		public const int MaxRepeat = 100;

		/// <summary>
		/// The maximum number of dice listed per group in the result text; the rest is counted, not printed.
		/// </summary>
		public const int MaxListedDice = 30;
	}
}
