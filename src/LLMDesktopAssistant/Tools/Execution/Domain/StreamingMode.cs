namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// How the streaming stage treats the argument stream of an invocation.
	/// </summary>
	public enum StreamingMode
	{
		/// <summary>
		/// Analyse every argument update while the model is still emitting. Used by an ordinary chat
		/// run, where the call is shown while its arguments are being generated.
		/// </summary>
		Cyclic,

		/// <summary>
		/// Drain the stream first, then analyse the complete argument set once. Used by restarts,
		/// commands and scripts, which already know the arguments.
		/// </summary>
		Once
	}
}
