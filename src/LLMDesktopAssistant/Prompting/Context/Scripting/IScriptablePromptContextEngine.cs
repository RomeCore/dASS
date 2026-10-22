using LLMDesktopAssistant.Scripting;

namespace LLMDesktopAssistant.Prompting.Context.Scripting
{
	/// <summary>
	/// Defines a scripting engine that can run scriptable prompt context addons written in a specific language
	/// (Lua, Python or C# script). Each engine knows the frontmatter syntax and how to execute a context body.
	/// </summary>
	/// <remarks>
	/// A third family next to <c>IScriptableToolEngine</c> and <c>IScriptableSlashCommandEngine</c>: a prompt context
	/// neither returns a tool result nor mutates a message, it returns plain text that becomes part of the prompt.
	/// The contract is deliberately narrow: a body plus the previous rendered text in, stdout out.
	/// </remarks>
	public interface IScriptablePromptContextEngine
	{
		/// <summary>The scripting language this engine handles.</summary>
		ScriptLanguageType Language { get; }

		/// <summary>Additional information about serialization and extensions.</summary>
		IScriptablePromptContextEngineDescriptor Descriptor { get; }

		/// <summary>
		/// Runs the context body and returns its raw output.
		/// </summary>
		/// <param name="services">The chat-scoped service provider.</param>
		/// <param name="body">The script body (the file content without the frontmatter).</param>
		/// <param name="previous">
		/// The previously injected text of this context, or <see langword="null"/> when there is none.
		/// Exposed to the script as the <c>previous</c> global; the engine does not compare it — the caller does.
		/// </param>
		/// <param name="cancellationToken">The token that aborts the execution (the caller applies the timeout).</param>
		/// <returns>The raw output of the script. Whitespace-only output means "no context".</returns>
		Task<string?> RunAsync(IServiceProvider services, string body, string? previous,
			CancellationToken cancellationToken = default);
	}
}
