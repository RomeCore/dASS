namespace LLMDesktopAssistant.SlashCommands.Resolution
{
	/// <summary>
	/// Resolves a command token into exactly one command (or none).
	/// </summary>
	/// <remarks>
	/// The resolver works on the slash-free token — knowing about the leading <c>/</c> marker is the job of the
	/// message-insertion service and the input autocomplete service, both of which use
	/// <see cref="SlashCommandExtractor.TryExtractToken"/>.
	/// </remarks>
	public interface ISlashCommandResolver
	{
		/// <summary>
		/// Resolves <paramref name="token"/> against the chat's command set.
		/// </summary>
		SlashCommandResolution Resolve(string token);
	}
}
