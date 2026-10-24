namespace LLMDesktopAssistant.SlashCommands
{
	/// <summary>
	/// How the message a slash command produced is presented to the model.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Declared per command (<see cref="SlashCommandInfo.ModelFacingMode"/>, the <c>model-facing</c> frontmatter field of
	/// a scriptable command) and overridable per invocation by the command's executor
	/// (<see cref="Execution.SlashCommandExecutionResult.ModelFacingMode"/>). The resolved value is recorded in the
	/// message's <see cref="Execution.SlashCommandFingerprint"/> - so it survives a reload - and applied at
	/// prompt-rendering time by <c>LLM.Services.Prompting.MessageContentProjector</c>, to the message's <c>Content</c>
	/// slot only.
	/// </para>
	/// <para>
	/// No mode removes what the command itself wrote into the message: the text of every injected
	/// <c>AdditionalMessageContentPart</c> (any <c>IContentChatAttachment</c>) is appended after the shaped content, so
	/// <see cref="Neutral"/> and <see cref="Hidden"/> hide the token and its raw arguments, never the injected skill body
	/// or a sub-agent's answer. A message that carries no fingerprint - one no command produced - is passed through
	/// untouched.
	/// </para>
	/// </remarks>
	public enum ModelFacingMode
	{
		/// <summary>
		/// The model sees the message as the user typed it, the <c>/token</c> and its raw arguments included. The
		/// <see cref="SlashCommandInfo.ModelFacingMode"/> default, and the mode the built-in derived executors (skills,
		/// sub-agents, tools) return.
		/// </summary>
		Raw = 0,

		/// <summary>
		/// The model sees the bare token - <c>/</c> followed by <see cref="Execution.SlashCommandFingerprint.RawToken"/>,
		/// the command's name without its namespaces - instead of the typed text. Used by <c>/addon-reload</c>, whose
		/// arguments are a UI concern rather than model context.
		/// </summary>
		Neutral = 1,

		/// <summary>
		/// The model sees no command content at all: only what the command injected into the message reaches it.
		/// </summary>
		Hidden = 2
	}
}
