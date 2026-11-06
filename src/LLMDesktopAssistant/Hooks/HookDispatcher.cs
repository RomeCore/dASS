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
		/// <summary>
		/// The time an awaited hook gets when it declares no timeout of its own, in milliseconds.
		/// </summary>
		private const int defaultTimeoutMs = 2000;

		/// <summary>
		/// The ceiling of an awaited hook's timeout, in milliseconds: a hook may ask for less, never for
		/// more, so an addon cannot stall the pipeline it is hooked into.
		/// </summary>
		private const int maxTimeoutMs = 10000;

		/// <inheritdoc/>
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
						parallelHookTasks.Add(RunBoundedAsync(hook, payload, null, hookType, hookName, cancellationToken));
						break;

					case HookExecutionFlow.Sequential:
						sequentialHooks.Add(hook);
						break;
				}
			}

			// Only sequential hooks can provide data to the caller.
			INodeValue? previousHookValue = null;
			foreach (var hook in sequentialHooks.OrderBy(h => h.Order))
				previousHookValue = await RunBoundedAsync(hook, payload, previousHookValue,
					hookType, hookName, cancellationToken);
			await Task.WhenAll(parallelHookTasks);

			return previousHookValue;
		}

		/// <summary>
		/// Runs one hook the caller waits on, bounded by the hook's declared timeout or the dispatcher
		/// default, whichever applies.
		/// </summary>
		/// <remarks>
		/// A hook that exceeds its bound is cancelled and treated as having no opinion: a hung script must
		/// not hold the pipeline, and failing open is the declared policy. Cancellation of the run itself
		/// still propagates.
		/// </remarks>
		/// <param name="hook">The hook to run.</param>
		/// <param name="payload">The hook input.</param>
		/// <param name="previous">The result of the previous hook in the chain, or <see langword="null"/>.</param>
		/// <param name="hookType">The event being fired.</param>
		/// <param name="hookName">The identity inside the event, or <see langword="null"/>.</param>
		/// <param name="cancellationToken">Cancellation of the run.</param>
		/// <returns>The hook's value, or <see langword="null"/> when it failed or timed out.</returns>
		private async Task<INodeValue?> RunBoundedAsync(HookInfo hook, INodeValue? payload, INodeValue? previous,
			string hookType, string? hookName, CancellationToken cancellationToken)
		{
			var timeout = GetTimeout(hook);
			using var timeoutSource = new CancellationTokenSource(timeout);
			using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
				cancellationToken, timeoutSource.Token);

			try
			{
				return await hook.Executor.ExecuteAsync(services, payload, previous,
					hookType, hookName, linkedSource.Token);
			}
			catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested &&
				!cancellationToken.IsCancellationRequested)
			{
				Log.Warning("Hook {HookName} of type {HookType} on key {HookKey} did not finish in {Timeout} ms " +
					"and was cancelled.", hook.Name, hookType, hookName, (long)timeout.TotalMilliseconds);
				return null;
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

		/// <summary>
		/// Gets the bound of one awaited hook: its declared timeout capped by the dispatcher ceiling, or the
		/// dispatcher default when the hook declares none.
		/// </summary>
		/// <param name="hook">The hook being dispatched.</param>
		/// <returns>The time the hook is allowed to run.</returns>
		private static TimeSpan GetTimeout(HookInfo hook) => hook.TimeoutMs is { } declared
			? TimeSpan.FromMilliseconds(Math.Clamp(declared, 1, maxTimeoutMs))
			: TimeSpan.FromMilliseconds(defaultTimeoutMs);
	}
}
