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
		private const int defaultTimeoutMs = 2000;

		private const int maxTimeoutMs = 30000;

		/// <inheritdoc/>
		public async Task<HookExecutionResult> ExecuteHooksAsync(INodeValue? payload,
			string hookType, string? hookTarget, HookExecutionFlow defaultFlow,
			bool isFlowForced, CancellationToken cancellationToken = default)
		{
			// Filter hooks based on hookType and hookTarget
			var hookSet = hookSetCollector.GetAddonsForChat();
			var hooks = hookSet.Where(h => h.HookTypePattern.Matches(hookType));
			if (!string.IsNullOrEmpty(hookTarget))
				hooks = hooks.Where(h => h.HookTarget is null || h.HookTargetPattern!.Matches(hookTarget));

			var parallelHookTasks = new List<Task>();
			var sequentialHooks = new List<HookInfo>();

			foreach (var hook in hooks)
			{
				var flow = isFlowForced ? defaultFlow : hook.ExecutionFlow ?? defaultFlow;
				switch (flow)
				{
					case HookExecutionFlow.FireAndForget:
					default:
						RunDetached(hook, payload, hookType, hookTarget);
						break;

					case HookExecutionFlow.Parallel:
						parallelHookTasks.Add(RunBoundedAsync(hook, payload, null, hookType, hookTarget, cancellationToken));
						break;

					case HookExecutionFlow.Sequential:
						sequentialHooks.Add(hook);
						break;
				}
			}

			// Only sequential hooks can provide data to the caller.
			HookExecutionResult previousHookValue = default;
			foreach (var hook in sequentialHooks.OrderBy(h => h.Order))
			{
				var result = await RunBoundedAsync(hook, payload, previousHookValue.Value,
					hookType, hookTarget, cancellationToken);
				if (!string.IsNullOrEmpty(result.BlockingMessage))
				{
					previousHookValue = result;
					break;
				}
				if (result.Value is not null && result.Value is not INodeNullValue)
					previousHookValue = result;
			}
			await Task.WhenAll(parallelHookTasks);

			return previousHookValue;
		}

		private void RunDetached(HookInfo hook, INodeValue? payload, string hookType, string? hookTarget)
		{
			_ = Task.Run(async () =>
			{
				// The declared value is not clamped from above: a background hook may legitimately run long.
				using var timeoutSource = hook.TimeoutMs is { } declared
					? new CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Max(declared, 1)))
					: null;
				var token = timeoutSource?.Token ?? CancellationToken.None;

				try
				{
					await hook.Executor.ExecuteAsync(services, payload, null, hookType, hookTarget, token);
				}
				catch (OperationCanceledException) when (timeoutSource?.IsCancellationRequested == true)
				{
					Log.Warning("Fire-and-forget hook {HookName} of type {HookType} on target {HookTarget} did not finish " +
						"in {Timeout} ms and was cancelled.", hook.Name, hookType, hookTarget, hook.TimeoutMs);
				}
				catch (Exception ex)
				{
					Log.Warning(ex, "Error executing fire-and-forget hook {HookName} of type {HookType} on target " +
						"{HookTarget}: {Error}", hook.Name, hookType, hookTarget, ex.Message);
				}
			}, CancellationToken.None);
		}

		private async Task<HookExecutionResult> RunBoundedAsync(HookInfo hook, INodeValue? payload, INodeValue? previous,
			string hookType, string? hookTarget, CancellationToken cancellationToken)
		{
			var timeout = GetBoundedTimeout(hook);
			using var timeoutSource = new CancellationTokenSource(timeout);
			using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
				cancellationToken, timeoutSource.Token);

			try
			{
				return await hook.Executor.ExecuteAsync(services, payload, previous,
					hookType, hookTarget, linkedSource.Token);
			}
			catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested &&
				!cancellationToken.IsCancellationRequested)
			{
				Log.Warning("Hook {HookName} of type {HookType} on target {HookTarget} did not finish in {Timeout} ms " +
					"and was cancelled.", hook.Name, hookType, hookTarget, (long)timeout.TotalMilliseconds);
				return new HookExecutionResult(null, null);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Error executing hook {HookName} of type {HookType} on target {HookTarget}: {Error}",
					hook.Name, hookType, hookTarget, ex.Message);
				return new HookExecutionResult(null, null);
			}
		}

		private static TimeSpan GetBoundedTimeout(HookInfo hook) => hook.TimeoutMs is { } declared
			? TimeSpan.FromMilliseconds(Math.Clamp(declared, 1, maxTimeoutMs))
			: TimeSpan.FromMilliseconds(defaultTimeoutMs);
	}
}
