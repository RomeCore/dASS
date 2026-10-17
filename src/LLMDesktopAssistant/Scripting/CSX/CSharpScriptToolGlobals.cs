using System.Text.Json.Nodes;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Scripting.CSX
{
	/// <summary>
	/// The globals object passed to C# scripts executed by <see cref="CSharpScriptToolEngine"/>.
	/// Provides access to tool arguments, the execution context and the tool result.
	/// </summary>
	public sealed class CSharpScriptToolGlobals
	{
		/// <summary>
		/// The arguments passed to the tool call, or <see langword="null"/> when the tool was called without arguments.
		/// </summary>
		public required JsonNode? ToolArgs { get; init; }

		/// <summary>
		/// The context of the current tool execution.
		/// </summary>
		public required IToolExecutionContext Context { get; init; }

		/// <summary>
		/// The result of the current tool execution, usable for streaming output, status updates and completion control.
		/// </summary>
		public required CSharpScriptToolResult Result { get; init; }

		/// <summary>
		/// The working directory of the current tool execution.
		/// </summary>
		public required string Workdir { get; init; }
	}
}
