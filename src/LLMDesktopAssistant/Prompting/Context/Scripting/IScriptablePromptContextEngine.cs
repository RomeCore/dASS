using LLMDesktopAssistant.Scripting;

namespace LLMDesktopAssistant.Prompting.Context.Scripting
{
	/// <summary>
	/// Defines a scripting engine that can run scriptable prompt context addons written in a specific language
	/// (Lua, Python, Bash, Batch, PowerShell, ...). Mirrors <c>IScriptableToolEngine</c> and
	/// <c>IScriptableSlashCommandEngine</c>: the engine builds the addon's provider, so the flags and the body
	/// stay on the addon instead of being threaded through a per-invocation signature.
	/// </summary>
	/// <remarks>
	/// A third family next to <c>IScriptableToolEngine</c> and <c>IScriptableSlashCommandEngine</c>: a prompt
	/// context neither returns a tool result nor mutates a message, it returns plain text that becomes part of
	/// the prompt. The provider owns the timeout and the failure isolation; the engine only runs the body.
	/// </remarks>
	public interface IScriptablePromptContextEngine
	{
		/// <summary>The scripting language this engine handles.</summary>
		ScriptLanguageType Language { get; }

		/// <summary>Additional information about serialization and extensions.</summary>
		IScriptablePromptContextEngineDescriptor Descriptor { get; }

		/// <summary>
		/// Builds the provider that runs this context, choosing the live-tail or the supersede behaviour from
		/// <see cref="PromptContextInfo.Kind"/>.
		/// </summary>
		/// <param name="context">The parsed context addon.</param>
		IPromptContextProvider CreateProvider(PromptContextInfo context);
	}
}
