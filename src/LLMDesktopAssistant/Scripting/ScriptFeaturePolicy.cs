namespace LLMDesktopAssistant.Scripting
{
	/// <summary>
	/// Which result channels a scriptable addon kind can consume. A fact about the kind, not about the language:
	/// the language's own limits live in <see cref="ScriptEngineCapabilities"/>.
	/// </summary>
	/// <remarks>
	/// A kind without <see cref="PlainOutput"/> has no result other than the promised structure, so its
	/// <c>json-output</c> becomes mandatory rather than optional — that is how a future hook, which answers with a
	/// decision and never with text, will be declared without a fourth flag.
	/// </remarks>
	[Flags]
	public enum ScriptFeaturePolicy
	{
		/// <summary>
		/// The kind consumes nothing beyond running the body.
		/// </summary>
		None = 0,

		/// <summary>
		/// The raw output is a legitimate result: without <c>json-output</c> the kind is satisfied with it.
		/// </summary>
		PlainOutput = 1,

		/// <summary>
		/// The kind consumes a structure taken from the output through <c>json-output</c>.
		/// </summary>
		JsonOutput = 2,

		/// <summary>
		/// The kind can hand its script to an interactive terminal instead of a managed process.
		/// </summary>
		Terminal = 4,
	}
}
