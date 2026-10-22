namespace LLMDesktopAssistant.Addons
{
	[Flags]
	public enum AddonDiagnosticCode : ulong
	{
		None = 0,

		// ==============================
		// === Common errors          ===
		// ==============================

		GeneralParsingError = 1UL << 0,

		MissingFrontmatter = 1UL << 1,

		FrontmatterParsingError = 1UL << 2,

		FrontmatterDecodingError = 1UL << 3,

		NameFSMismatch = 1UL << 4,

		NameFormatError = 1UL << 5,

		MissingName = 1UL << 6,

		MissingFrontmatterName = 1UL << 7,

		MissingDescription = 1UL << 8,

		MissingFrontmatterDescription = 1UL << 9,

		MissingFile = 1UL << 10,

		FileAccessError = 1UL << 11,

		// ==============================
		// === Addon-specific errors  ===
		// ==============================

		/// <summary>
		/// The addon's <c>argument-hint</c> starts with an argument but could not be parsed.
		/// </summary>
		InvalidArgumentHint = 1UL << 12,

		/// <summary>
		/// The addon's <c>kind</c> has invalid value.
		/// </summary>
		InvalidKind = 1UL << 13,
	}
}
