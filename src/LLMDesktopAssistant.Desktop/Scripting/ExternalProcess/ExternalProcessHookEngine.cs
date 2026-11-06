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
		public async Task<INodeValue?> ExecuteAsync(IServiceProvider services, INodeValue? payload, INodeValue? previous,
			string hookType, string? hookName, CancellationToken cancellationToken = default)
		{
			var engine = registry.Resolve(hook.Path ?? throw new InvalidOperationException(
				$"The path of the hook '{hook.Name}' is not set.")).Engine;

			var variables = ImmutableDictionary<string, INodeValue?>.Empty
				.Add("payload", payload)
				.Add("previous", previous)
				.Add("hook_type", new ConstNodeStringValue { Value = hookType })
				.Add("hook_name", string.IsNullOrEmpty(hookName) ? ConstNodeNullValue.Instance :
					new ConstNodeStringValue { Value = hookName });

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
				Log.Debug("Error executing external process script hook {HookName} of type {HookType} on key {HookKey}: {Error}",
					hook.Name, hookType, hookName, result.Error);
				return null;
			}

			return result.Json.ToConstNodeValue();
		}
	}
}
