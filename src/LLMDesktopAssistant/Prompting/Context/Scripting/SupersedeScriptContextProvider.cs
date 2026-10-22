using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Prompting.Context.Scripting
{
	/// <summary>
	/// A supersede context driven by a script: the body runs on every prompt build, receives the previously
	/// injected text as <c>previous</c> and produces a stamp only when the rendered text actually changed.
	/// </summary>
	public sealed class SupersedeScriptContextProvider(
		IScriptablePromptContextEngine engine,
		PromptContextInfo addon
	) : IPromptSupersedeContextProvider
	{
		/// <inheritdoc/>
		public string Discriminator => ScriptPromptContextHelper.GetDiscriminator(addon.Name);

		/// <inheritdoc/>
		public async Task<PromptSupersedeStampBase?> GetNewStampAsync(PromptSupersedeStampBase? previousStamp,
			EffectiveChatContext context, CancellationToken cancellationToken = default)
		{
			var content = await ScriptPromptContextHelper.RunAsync(engine, context.Services, addon.Body,
				previousStamp?.Snapshot, addon.TimeoutMs, cancellationToken);

			if (string.IsNullOrWhiteSpace(content))
				return null;

			// The comparison is byte-exact on the rendered text: the wrapper is deterministic,
			// so comparing the wrapped form is the same as comparing the raw output.
			var rendered = ScriptPromptContextHelper.Wrap(addon.Name, content);
			if (previousStamp?.Snapshot == rendered)
				return null;

			return new ScriptPromptContextStamp
			{
				Snapshot = rendered
			};
		}

		/// <inheritdoc/>
		/// <remarks>The snapshot is rendered in advance, so there is nothing left to render.</remarks>
		public string Render(PromptSupersedeStampBase stamp) => stamp.Snapshot;
	}
}
