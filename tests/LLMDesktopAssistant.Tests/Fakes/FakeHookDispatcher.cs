using LLMDesktopAssistant.Hooks;
using LLMDesktopAssistant.StructuredValues;

namespace LLMDesktopAssistant.Tests.Fakes
{
	/// <summary>
	/// A hook dispatcher that records what it was asked and answers every call with <see cref="Result"/>.
	/// </summary>
	public sealed class FakeHookDispatcher : IHookDispatcher
	{
		/// <summary>
		/// The events the pipeline fired, in order.
		/// </summary>
		public List<Call> Calls { get; } = [];

		/// <summary>
		/// The answer every call gets; the default is "no opinion".
		/// </summary>
		public HookExecutionResult Result { get; set; }

		/// <inheritdoc/>
		public Task<HookExecutionResult> ExecuteHooksAsync(INodeValue? payload, string hookType, string? hookTarget,
			HookExecutionFlow defaultFlow, bool isFlowForced = false, CancellationToken cancellationToken = default)
		{
			Calls.Add(new Call(hookType, hookTarget, payload));
			return Task.FromResult(Result);
		}

		/// <summary>
		/// One recorded dispatch.
		/// </summary>
		/// <param name="HookType">The event name the pipeline asked for.</param>
		/// <param name="HookTarget">The target the pipeline passed.</param>
		/// <param name="Payload">The document the hooks would receive.</param>
		public sealed record Call(string HookType, string? HookTarget, INodeValue? Payload);
	}
}
