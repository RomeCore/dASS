using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Prompting.Context.Scripting;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Lua implementation of <see cref="IScriptablePromptContextEngine"/>.
	/// The body runs in the chat's Lua state; <c>print(...)</c> output is the context text and the previous
	/// rendered text is exposed as the <c>previous</c> global.
	/// </summary>
	[Service(typeof(IScriptablePromptContextEngine))]
	public class LuaPromptContextEngine : IScriptablePromptContextEngine
	{
		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.Lua;

		/// <inheritdoc/>
		public IScriptablePromptContextEngineDescriptor Descriptor { get; } = new LuaPromptContextEngineDescriptor();

		/// <inheritdoc/>
		public IPromptContextProvider CreateProvider(PromptContextInfo context)
			=> context.Kind is PromptContextProviderKind.LiveTail
				? new LuaLiveTailScriptContextProvider(context)
				: new LuaSupersedeScriptContextProvider(context);
	}

	/// <summary>The descriptor of the Lua scriptable prompt-context engine.</summary>
	public class LuaPromptContextEngineDescriptor : IScriptablePromptContextEngineDescriptor
	{
		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.Lua;

		/// <inheritdoc/>
		public string[] Extensions => [".lua", ".alua"];

		/// <inheritdoc/>
		public string FrontmatterStart => "--[[";

		/// <inheritdoc/>
		public string FrontmatterEnd => "]]";
	}
}
