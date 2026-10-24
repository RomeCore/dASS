using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Prompting.Context.Scripting;

namespace LLMDesktopAssistant.Desktop.Scripting.Python
{
	/// <summary>
	/// A supersede prompt context written in Python: the body runs on every prompt build, receives the previously
	/// injected text as <c>previous</c> and produces a stamp only when the rendered text actually changed.
	/// </summary>
	public sealed class PythonSupersedeScriptContextProvider(
		PromptContextInfo promptContext,
		IProcessLauncher processLauncher,
		PythonHelperService pythonHelperService)
		: PythonScriptContextProviderBase(promptContext, processLauncher, pythonHelperService), IPromptSupersedeContextProvider
	{
		/// <inheritdoc/>
		public string Discriminator => ScriptPromptContextHelper.GetDiscriminator(PromptContext.Name);

		/// <inheritdoc/>
		public async Task<PromptSupersedeStampBase?> GetNewStampAsync(PromptSupersedeStampBase? previousStamp,
			PromptContextExecutionContext context, CancellationToken cancellationToken = default)
		{
			var content = await RunAsync(context, previousStamp?.Snapshot, cancellationToken);

			if (string.IsNullOrWhiteSpace(content))
				return null;

			// The comparison is byte-exact on the rendered text: the wrapper is deterministic,
			// so comparing the wrapped form is the same as comparing the raw output.
			var rendered = ScriptPromptContextHelper.Wrap(PromptContext.Name, content);
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
