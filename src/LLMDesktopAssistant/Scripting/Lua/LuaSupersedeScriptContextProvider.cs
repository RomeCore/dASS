using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Prompting.Context.Scripting;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// A supersede prompt context written in Lua: the body runs on every prompt build, receives the previously
	/// injected text as <c>previous</c> and produces a stamp only when the rendered text actually changed.
	/// </summary>
	public sealed class LuaSupersedeScriptContextProvider(PromptContextInfo addon)
		: LuaScriptContextProviderBase(addon), IPromptSupersedeContextProvider
	{
		/// <inheritdoc/>
		public string Discriminator => ScriptPromptContextHelper.GetDiscriminator(Addon.Name);

		/// <inheritdoc/>
		public async Task<PromptSupersedeStampBase?> GetNewStampAsync(PromptSupersedeStampBase? previousStamp,
			PromptContextExecutionContext context, CancellationToken cancellationToken = default)
		{
			var content = await RunAsync(context, previousStamp?.Snapshot, cancellationToken);

			if (string.IsNullOrWhiteSpace(content))
				return null;

			// The comparison is byte-exact on the rendered text: the wrapper is deterministic,
			// so comparing the wrapped form is the same as comparing the raw output.
			var rendered = ScriptPromptContextHelper.Wrap(Addon.Name, content);
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
