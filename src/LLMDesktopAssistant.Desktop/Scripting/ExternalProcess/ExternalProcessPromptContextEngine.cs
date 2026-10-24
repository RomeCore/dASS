using System.Collections.Immutable;
using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Prompting.Context.Scripting;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Const;
using Serilog;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// The out-of-process prompt-context kind: it is a creator only, and every provider it builds runs the body
	/// through the language engine that owns the context's extension.
	/// </summary>
	/// <remarks>
	/// The timeout budget and the failure isolation belong to the caller (<c>PromptContextBudget</c>), never to a
	/// language, so a provider arms nothing and catches nothing.
	/// </remarks>
	[Service(typeof(IScriptablePromptContextEngine))]
	public class ExternalProcessPromptContextEngine(ExternalProcessEngineRegistry registry) : IScriptablePromptContextEngine
	{
		/// <inheritdoc/>
		public IReadOnlyList<IScriptEngineDescriptor> Descriptors => registry.Descriptors;

		/// <inheritdoc/>
		public IPromptContextProvider CreateProvider(PromptContextInfo context) => context.Kind switch
		{
			PromptContextProviderKind.LiveTail =>
				new ExternalProcessLiveTailScriptContextProvider(context, registry),
			PromptContextProviderKind.Supersede =>
				new ExternalProcessSupersedeScriptContextProvider(context, registry),
			_ => throw new NotSupportedException(
				$"Scriptable context '{context.Name}' declares kind '{context.Kind}', but only 'live-tail' and " +
				"'supersede' are scriptable. A scriptable anchored section does not exist yet.")
		};
	}

	/// <summary>
	/// The shared runner of an out-of-process prompt context: one assignment of <c>previous</c>, then the body.
	/// </summary>
	/// <remarks>
	/// The provider <em>is</em> the run: the concrete provider decides what the text means — a current-request live
	/// tail or a persisted supersede stamp. This base only starts the engine; the timeout is the caller's
	/// cancellation token, and a failed script is one warning and "no context", never a broken prompt build.
	/// </remarks>
	public abstract class ExternalProcessScriptContextProviderBase(PromptContextInfo addon,
		ExternalProcessEngineRegistry registry)
	{
		/// <summary>The context addon this provider runs.</summary>
		protected PromptContextInfo Addon { get; } = addon;

		/// <summary>Runs the body as a headless process and returns what it printed.</summary>
		protected async Task<string?> RunAsync(PromptContextExecutionContext context, string? previous,
			CancellationToken cancellationToken)
		{
			var engine = registry.Resolve(Addon.Path ?? throw new InvalidOperationException(
				$"The path of the context '{Addon.Name}' is not set.")).Engine;

			var result = await engine.RunAsync(new ExternalProcessScriptParameters
			{
				Kind = ScriptableAddonKind.PromptContext,
				Body = Addon.Body,
				Variables = ImmutableDictionary<string, INodeValue?>.Empty.Add("previous", previous is null
					? null
					: new ConstNodeStringValue { Value = previous }),
				InputMode = Addon.InputMode,
				// A context has no JSON channel, and its budget is the caller's cancellation token, not a deadline here.
				OutputMode = ScriptOutputMode.None,
				ProcessName = Addon.Name,
				TimeOut = null
			}, context.Services, cancellationToken);

			if (result.Status is ExternalProcessStatus.Succeeded)
				return result.Output;

			// A timeout is already reported by the budget that armed it; anything else is this script's own failure.
			if (result.Status is not ExternalProcessStatus.Cancelled)
			{
				Log.Warning("Prompt context script '{Context}' failed ({Status}): {Error}",
					Addon.Name, result.Status, result.Error);
			}

			return null;
		}
	}

	/// <summary>
	/// A live-tail context in any out-of-process language: the body runs on every prompt build and its text is
	/// injected into the current request only.
	/// </summary>
	public sealed class ExternalProcessLiveTailScriptContextProvider(PromptContextInfo addon,
		ExternalProcessEngineRegistry registry)
		: ExternalProcessScriptContextProviderBase(addon, registry), IPromptLiveTailContextProvider
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

	/// <summary>
	/// A supersede context in any out-of-process language: the body receives the previously injected text as
	/// <c>previous</c> and produces a stamp only when the rendered text actually changed.
	/// </summary>
	public sealed class ExternalProcessSupersedeScriptContextProvider(PromptContextInfo addon,
		ExternalProcessEngineRegistry registry)
		: ExternalProcessScriptContextProviderBase(addon, registry), IPromptSupersedeContextProvider
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
