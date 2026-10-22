using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Prompting.Context.Scripting
{
	/// <summary>
	/// A live-tail context driven by a script: the body runs on every prompt build and its output is injected
	/// into the current request only.
	/// </summary>
	public sealed class LiveTailScriptContextProvider(
		IScriptablePromptContextEngine engine,
		PromptContextInfo addon
	) : IPromptLiveTailContextProvider
	{
		/// <inheritdoc/>
		public async Task<string?> ProvideAsync(EffectiveChatContext context,
			CancellationToken cancellationToken = default)
		{
			var content = await ScriptPromptContextHelper.RunAsync(engine, context.Services, addon.Body, null,
				addon.TimeoutMs, cancellationToken);

			return string.IsNullOrWhiteSpace(content)
				? null
				: ScriptPromptContextHelper.Wrap(addon.Name, content);
		}
	}
}
