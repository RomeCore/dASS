using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Prompting.Context.Scripting;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Scripting.Lua.Addons
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
		public IReadOnlyList<IScriptEngineDescriptor> Descriptors { get; } = [LuaScriptEngineDescriptor.Instance];

		/// <inheritdoc/>
		public IPromptContextProvider CreateProvider(PromptContextInfo context) => context.Kind switch
		{
			PromptContextProviderKind.LiveTail => new LuaLiveTailScriptContextProvider(context),
			PromptContextProviderKind.Supersede => new LuaSupersedeScriptContextProvider(context),
			_ => throw new NotSupportedException(
				$"Scriptable context '{context.Name}' declares kind '{context.Kind}', but only 'live-tail' and " +
				"'supersede' are scriptable. A scriptable anchored section does not exist yet.")
		};
	}
}
