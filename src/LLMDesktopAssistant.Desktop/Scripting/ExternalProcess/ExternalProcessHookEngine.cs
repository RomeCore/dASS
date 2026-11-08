using System.Collections.Immutable;
using LLMDesktopAssistant.Hooks;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Const;
using LLMDesktopAssistant.StructuredValues.Converters;
using Serilog;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	[Service(typeof(IScriptableHookEngine))]
	public class ExternalProcessHookEngine(ExternalProcessEngineRegistry registry) : IScriptableHookEngine
	{
		/// <inheritdoc/>
		public IReadOnlyList<IScriptEngineDescriptor> Descriptors => registry.Descriptors;

		/// <inheritdoc/>
		public IHookExecutor CreateExecutor(HookInfo hook) => new ExternalProcessHookExecutor(hook, registry);
	}

	public class ExternalProcessHookExecutor(HookInfo hook,
		ExternalProcessEngineRegistry registry) : IHookExecutor
	{
		public async Task<HookExecutionResult> ExecuteAsync(IServiceProvider services, INodeValue? payload, INodeValue? previous,
			string hookType, string? hookTarget, CancellationToken cancellationToken = default)
		{
			var engine = registry.Resolve(hook.Path ?? throw new InvalidOperationException(
				$"The path of the hook '{hook.Name}' is not set.")).Engine;

			var variables = ImmutableDictionary<string, INodeValue?>.Empty
				.Add("payload", payload)
				.Add("previous", previous)
				.Add("hook_type", new ConstNodeStringValue { Value = hookType })
				.Add("hook_target", string.IsNullOrEmpty(hookTarget) ? ConstNodeNullValue.Instance :
					new ConstNodeStringValue { Value = hookTarget });

			var result = await engine.RunAsync(new ExternalProcessScriptParameters
			{
				Kind = ScriptableAddonKind.Hook,
				Body = hook.Body,
				Variables = variables,
				InputMode = hook.InputMode,
				OutputMode = hook.OutputMode,
				OutputPattern = hook.OutputPattern,
				ProcessName = hook.Name
			}, services, cancellationToken);

			if (result.Status is not ExternalProcessStatus.Succeeded)
			{
				Log.Debug("Error executing external process script hook {HookName} of type {HookType} on target {HookTarget}: {Error}",
					hook.Name, hookType, hookTarget, result.Error);

				// Exit code 2 blocks other hooks and produces a blocking message,
				// so hook callers can interrupt tool calls for example.
				if (result.ExitCode is 2)
					return new HookExecutionResult(null, result.Error);

				return new HookExecutionResult(null, null);
			}

			return new HookExecutionResult(result.Json.ToConstNodeValue(), null);
		}
	}
}
