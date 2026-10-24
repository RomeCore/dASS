using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Prompting.Context.Scripting;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// A live-tail prompt context written in Lua: the body runs on every prompt build and its text is injected
	/// into the current request only.
	/// </summary>
	public sealed class LuaLiveTailScriptContextProvider(PromptContextInfo addon)
		: LuaScriptContextProviderBase(addon), IPromptLiveTailContextProvider
	{
		/// <inheritdoc/>
		public async Task<string?> ProvideAsync(PromptContextExecutionContext context,
			CancellationToken cancellationToken = default)
		{
			var content = await RunAsync(context, null, cancellationToken);

			return string.IsNullOrWhiteSpace(content)
				? null
				: ScriptPromptContextHelper.Wrap(Addon.Name, content);
		}
	}
}
