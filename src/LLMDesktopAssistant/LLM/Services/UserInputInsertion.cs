using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// A message that has already been appended to the history, together with what the command path needs to run
	/// later. Opaque to callers: they only hand it back to
	/// <see cref="IChatMessageInsertionService.RunInsertedCommandAsync"/>.
	/// </summary>
	/// <param name="Message">The message as it now sits in the history.</param>
	/// <param name="Command">The command the message carries, or <see langword="null"/> when it carries none.</param>
	public sealed record UserInputInsertion(ChatMessage Message, PendingCommand? Command);

	/// <summary>
	/// A resolved command waiting to be executed on an already-inserted message.
	/// </summary>
	/// <param name="RawToken">The token exactly as the user typed it, for the fingerprint.</param>
	/// <param name="RawArguments">The arguments exactly as the user typed them, for the fingerprint.</param>
	/// <param name="Resolution">The resolved command and its bound arguments, or the reason it failed.</param>
	public sealed record PendingCommand(string RawToken, string RawArguments, CommandResolution Resolution);

	/// <summary>
	/// The outcome of an insertion as far as generation is concerned: the intent the command left behind.
	/// </summary>
	/// <param name="Generate">Whether an answer should be generated for this message.</param>
	public readonly record struct UserInputInsertionResult(bool Generate);
}
