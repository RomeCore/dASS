using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Tools.Execution.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.Tools.Implementations.Filesystem
{
	/// <summary>
	/// Derives the synchronization keys of a filesystem edit: the file the call writes.
	/// </summary>
	/// <remarks>
	/// The path is absolutized here - the synchronizer never resolves paths. Access denial is irrelevant:
	/// the key is about the path, not about permission. A missing or unusable path falls back to the
	/// family-wide <c>fs-edit</c> key, so an invocation with unparsable arguments cannot race with another
	/// edit of the same family.
	/// </remarks>
	public static class FilesystemSynchronizationAnalyzer
	{
		/// <summary>
		/// Analyzes the call arguments and returns the keys the invocation claims.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		/// <param name="args">The model's arguments.</param>
		/// <returns>The keys.</returns>
		public static IReadOnlyList<ToolSyncKey> AnalyzeSyncKeys(IToolExecutionContext ctx,
			[OriginalArgs] JsonNode? args)
		{
			try
			{
				var path = args?["path"]?.GetValue<string>();
				if (string.IsNullOrWhiteSpace(path))
					return [new(ToolSyncKeyKind.AllFiles, string.Empty)];

				var access = ctx.Services.GetService<IWorkingDirectoryAccessService>();
				if (access is null)
					return [new(ToolSyncKeyKind.AllFiles, string.Empty)];

				var fullPath = access.CheckedAccessPath(path, DirectoryAccessMode.Read, out _);
				return string.IsNullOrEmpty(fullPath)
					? [new(ToolSyncKeyKind.AllFiles, string.Empty)]
					: [new(ToolSyncKeyKind.File, fullPath)];
			}
			catch
			{
				return [new(ToolSyncKeyKind.AllFiles, string.Empty)];
			}
		}
	}
}
