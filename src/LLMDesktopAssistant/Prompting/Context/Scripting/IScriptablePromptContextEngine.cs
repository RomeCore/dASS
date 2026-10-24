using LLMDesktopAssistant.Scripting;

namespace LLMDesktopAssistant.Prompting.Context.Scripting
{
	/// <summary>
	/// Defines a scripting engine that can run scriptable prompt context addons written in a specific language
	/// (Lua, Python, Bash, ...). Mirrors <c>IScriptableToolEngine</c> and <c>IScriptableSlashCommandEngine</c>: the
	/// engine builds the addon's provider, so the flags and the body stay on the addon instead of being threaded
	/// through a per-invocation signature.
	/// </summary>
	/// <remarks>
	/// A third family next to <c>IScriptableToolEngine</c> and <c>IScriptableSlashCommandEngine</c>: a prompt
	/// context neither returns a tool result nor mutates a message, it returns plain text that becomes part of
	/// the prompt. The provider owns the run; the timeout budget and the failure isolation belong to the caller
	/// (<c>PromptContextBudget</c>), not to the engine.
	/// </remarks>
	public interface IScriptablePromptContextEngine
	{
		/// <summary>
		/// The languages this engine serves: extensions, frontmatter syntax and capabilities, one descriptor per
		/// language. A multi-language adapter answers with the union; a single-language engine answers with one item.
		/// </summary>
		IReadOnlyList<IScriptEngineDescriptor> Descriptors { get; }

		/// <summary>
		/// Builds the provider that runs this context, choosing the live-tail or the supersede behaviour from
		/// <see cref="PromptContextInfo.Kind"/>.
		/// </summary>
		/// <param name="context">The parsed context addon.</param>
		IPromptContextProvider CreateProvider(PromptContextInfo context);
	}
}
