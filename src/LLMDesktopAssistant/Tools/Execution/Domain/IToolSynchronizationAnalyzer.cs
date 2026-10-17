using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// Derives the per-invocation synchronization keys from the arguments. This is the capability that
	/// absolutizes paths; the synchronizer only canonicalizes and compares.
	/// </summary>
	public interface IToolSynchronizationAnalyzer
	{
		/// <summary>
		/// Derives the keys this invocation must hold.
		/// </summary>
		/// <param name="args">The effective arguments.</param>
		/// <param name="ctx">The invocation context.</param>
		/// <returns>The keys, with paths already absolute. The synchronizer canonicalizes and collapses them.</returns>
		IReadOnlyList<ToolSyncKey> AnalyzeSyncKeys(JsonNode? args, IToolExecutionContext ctx);
	}
}
