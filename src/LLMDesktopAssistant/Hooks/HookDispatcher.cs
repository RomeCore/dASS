using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.StructuredValues;
using Serilog;

namespace LLMDesktopAssistant.Hooks
{
	[ChatService(typeof(IHookDispatcher))]
	public class HookDispatcher(
		IAddonSetCollector<HookInfo> hookSetCollector,
		IServiceProvider services
	) : IHookDispatcher
	{
		public async Task<INodeValue?> ExecuteHooksAsync(INodeValue? payload,
			string hookType, string? hookName, HookExecutionFlow defaultFlow,
			HookExecutionFlow? forcedFlow = null, CancellationToken cancellationToken = default)
		{
			// Filter hooks based on hookType and hookName
			var hookSet = hookSetCollector.GetAddonsForChat();
			var hooks = hookSet.Where(h => h.HookTypePattern.Matches(hookType));
			if (!string.IsNullOrEmpty(hookName))
				hooks = hooks.Where(h => h.HookName is null || h.HookNamePattern!.Matches(hookName));

			var parallelHookTasks = new List<Task>();
			var sequentialHooks = new List<HookInfo>();

			foreach (var hook in hooks)
			{
				switch (forcedFlow ?? hook.ExecutionFlow ?? defaultFlow)
				{
					case HookExecutionFlow.FireAndForget:
					default:
						_ = Task.Run(() => hook.Executor.ExecuteAsync(services,
							payload, null, hookType, hookName, CancellationToken.None), CancellationToken.None);
						break;

					case HookExecutionFlow.Parallel:
						parallelHookTasks.Add(WrapTask(hook.Executor.ExecuteAsync(services, payload, null,
							hookType, hookName, cancellationToken), hook, hookType, hookName));
						break;

					case HookExecutionFlow.Sequential:
						sequentialHooks.Add(hook);
						break;
				}
			}

			// Only sequential hooks can provide data to the caller.
			INodeValue? previousHookValue = null;
			foreach (var hook in sequentialHooks.OrderBy(h => h.Order))
				previousHookValue = await WrapTask(hook.Executor.ExecuteAsync(services, payload, previousHookValue,
					hookType, hookName, cancellationToken), hook, hookType, hookName);
			await Task.WhenAll(parallelHookTasks);

			return previousHookValue;
		}

		private static async Task<INodeValue?> WrapTask(Task<INodeValue?> task, HookInfo hook,
			string hookType, string? hookName)
		{
			try
			{
				return await task;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Error executing hook {HookName} of type {HookType} on key {HookKey}: {Error}",
					hook.Name, hookType, hookName, ex.Message);
				return null;
			}
		}
	}
}
