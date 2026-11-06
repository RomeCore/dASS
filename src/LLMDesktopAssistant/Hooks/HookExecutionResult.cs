using LLMDesktopAssistant.StructuredValues;

namespace LLMDesktopAssistant.Hooks
{
	/// <summary>
	/// Represents the result of a hook execution.
	/// </summary>
	/// <param name="Value">The resulting structured value from the hook execution.</param>
	/// <param name="BlockingMessage">An optional message that blocks further hook execution.</param>
	public readonly record struct HookExecutionResult(INodeValue? Value, string? BlockingMessage);

	/// <summary>
	/// Represents the generic result of a hook execution.
	/// </summary>
	/// <param name="Value">The resulting value from the hook execution.</param>
	/// <param name="BlockingMessage">An optional message that blocks further hook execution.</param>
	public readonly record struct HookExecutionResult<T>(T Value, string? BlockingMessage);
}
