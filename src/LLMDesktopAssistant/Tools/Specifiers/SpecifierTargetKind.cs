namespace LLMDesktopAssistant.Tools.Specifiers
{
	/// <summary>
	/// The dialect a specifier literal is compiled in. It is a property of the argument the literal
	/// targets, not of the rule text: <c>src/**/*.cs</c> is a syntax error for a command line and a
	/// perfectly good path.
	/// </summary>
	public enum SpecifierTargetKind
	{
		/// <summary>
		/// A flat text: <c>*</c>, <c>?</c>, <c>[...]</c>, <c>{...}</c> and the terminal <c>:*</c>.
		/// Used for command lines, ids and any other prose argument.
		/// </summary>
		Text,

		/// <summary>
		/// A filesystem path: segments, <c>/</c>, <c>**</c> and a resolvable prefix, matched against
		/// the argument after it is resolved against the tool's working directory.
		/// </summary>
		Path,
	}
}
