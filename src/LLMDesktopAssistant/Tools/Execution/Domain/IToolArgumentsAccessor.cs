using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// The single place the effective arguments are read from. It hides the two channels — the
	/// persisted <see cref="IToolCall.Arguments"/> and the ephemeral
	/// <see cref="IToolExecutionContext.ArgumentsOverride"/> — behind one cached value.
	/// </summary>
	public interface IToolArgumentsAccessor
	{
		/// <summary>
		/// <c>ArgumentsOverride ?? parsed(call.Arguments)</c>. Recomputed when either input changes;
		/// a stale parse tree is therefore unrepresentable rather than merely discouraged.
		/// </summary>
		JsonNode? Effective { get; }
	}
}
