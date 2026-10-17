using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Tools
{
	/// <summary>
	/// A custom attribute to mark a parameter as the original JSON arguments of a tool.
	/// When used in methods that passed to <see cref="Execution.Executors.ToolMethodBinder"/> (and analogs), the raw <see cref="JsonNode"/> function tool call arguments will be passed to this parameter.
	/// </summary>
	[AttributeUsage(AttributeTargets.Parameter)]
	public sealed class OriginalArgsAttribute : Attribute
	{
	}
}