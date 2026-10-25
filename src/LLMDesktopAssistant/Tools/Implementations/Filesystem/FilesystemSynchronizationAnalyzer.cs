using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Tools.Execution.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.Tools.Implementations.Filesystem
{
	/// <summary>
	/// Derives the synchronization keys of a filesystem call from its paths.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Paths are absolutized here - the synchronizer never resolves paths. Access denial is irrelevant:
	/// a key is about the path, not about permission. The variants differ only in the kind of the key and
	/// in the access of each side, because the same shape recurs across the family: one path, or a source
	/// and a target that are claimed differently.
	/// </para>
	/// <para>
	/// A missing or unusable path falls back to the family-wide <see cref="ToolSyncKeyKind.AllFiles"/> key,
	/// so an invocation with unparsable arguments cannot race with another call of the same family. The
	/// access survives the fallback: an unparsable read claims a shared all-files key, an unparsable write
	/// an exclusive one.
	/// </para>
	/// <para>
	/// A read of a path that is a directory must claim a <see cref="ToolSyncKeyKind.Directory"/> key: a
	/// file key placed on a directory compares equal only to itself, so it would silently fail to conflict
	/// with the files inside it.
	/// </para>
	/// </remarks>
	public static class FilesystemSynchronizationAnalyzer
	{
		/// <summary>
		/// Analyzes a call that writes one file.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		/// <param name="args">The model's arguments.</param>
		/// <returns>The keys.</returns>
		public static IReadOnlyList<ToolSyncKey> AnalyzeFileWrite(IToolExecutionContext ctx,
			[OriginalArgs] JsonNode? args) =>
			Single(ctx, args, ToolSyncKeyKind.File, ToolSyncAccess.Write, "path");

		/// <summary>
		/// Analyzes a call that reads one file.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		/// <param name="args">The model's arguments.</param>
		/// <returns>The keys.</returns>
		public static IReadOnlyList<ToolSyncKey> AnalyzeFileRead(IToolExecutionContext ctx,
			[OriginalArgs] JsonNode? args) =>
			Single(ctx, args, ToolSyncKeyKind.File, ToolSyncAccess.Read, "path");

		/// <summary>
		/// Analyzes a call that writes one directory.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		/// <param name="args">The model's arguments.</param>
		/// <returns>The keys.</returns>
		public static IReadOnlyList<ToolSyncKey> AnalyzeDirectoryWrite(IToolExecutionContext ctx,
			[OriginalArgs] JsonNode? args) =>
			Single(ctx, args, ToolSyncKeyKind.Directory, ToolSyncAccess.Write, "path");

		/// <summary>
		/// Analyzes a download that writes the response body into the file named by <c>savePath</c>.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		/// <param name="args">The model's arguments.</param>
		/// <returns>The keys.</returns>
		public static IReadOnlyList<ToolSyncKey> AnalyzeSavePathWrite(IToolExecutionContext ctx,
			[OriginalArgs] JsonNode? args) =>
			Single(ctx, args, ToolSyncKeyKind.File, ToolSyncAccess.Write, "savePath");

		/// <summary>
		/// Analyzes a call that reads a path which may be a file or a directory.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		/// <param name="args">The model's arguments.</param>
		/// <returns>The keys.</returns>
		public static IReadOnlyList<ToolSyncKey> AnalyzePathRead(IToolExecutionContext ctx,
			[OriginalArgs] JsonNode? args) =>
			PathRead(ctx, args, "path");

		/// <summary>
		/// Analyzes a copy of a file: the source is only read, the target is written.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		/// <param name="args">The model's arguments.</param>
		/// <returns>The keys.</returns>
		public static IReadOnlyList<ToolSyncKey> AnalyzeCopyFile(IToolExecutionContext ctx,
			[OriginalArgs] JsonNode? args) =>
			Pair(ctx, args, ToolSyncKeyKind.File, ToolSyncAccess.Read);

		/// <summary>
		/// Analyzes a rename or a move of a file: both sides are written, since the source disappears.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		/// <param name="args">The model's arguments.</param>
		/// <returns>The keys.</returns>
		public static IReadOnlyList<ToolSyncKey> AnalyzeMoveFile(IToolExecutionContext ctx,
			[OriginalArgs] JsonNode? args) =>
			Pair(ctx, args, ToolSyncKeyKind.File, ToolSyncAccess.Write);

		/// <summary>
		/// Analyzes a copy of a directory: the source is only read, the target is written.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		/// <param name="args">The model's arguments.</param>
		/// <returns>The keys.</returns>
		public static IReadOnlyList<ToolSyncKey> AnalyzeCopyDirectory(IToolExecutionContext ctx,
			[OriginalArgs] JsonNode? args) =>
			Pair(ctx, args, ToolSyncKeyKind.Directory, ToolSyncAccess.Read);

		/// <summary>
		/// Analyzes a move of a directory: both sides are written, since the source disappears.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		/// <param name="args">The model's arguments.</param>
		/// <returns>The keys.</returns>
		public static IReadOnlyList<ToolSyncKey> AnalyzeMoveDirectory(IToolExecutionContext ctx,
			[OriginalArgs] JsonNode? args) =>
			Pair(ctx, args, ToolSyncKeyKind.Directory, ToolSyncAccess.Write);

		/// <summary>
		/// Builds the key of a single-path call.
		/// </summary>
		private static IReadOnlyList<ToolSyncKey> Single(IToolExecutionContext ctx, JsonNode? args,
			ToolSyncKeyKind kind, ToolSyncAccess access, string pathName)
		{
			var fullPath = Absolutize(ctx, args, pathName);

			return fullPath is null
				? [new(ToolSyncKeyKind.AllFiles, string.Empty, access)]
				: [new(kind, fullPath, access)];
		}

		/// <summary>
		/// Builds the key of a read whose path may be a file or a directory.
		/// </summary>
		private static IReadOnlyList<ToolSyncKey> PathRead(IToolExecutionContext ctx, JsonNode? args,
			string pathName)
		{
			var fullPath = Absolutize(ctx, args, pathName);
			if (fullPath is null)
				return [new(ToolSyncKeyKind.AllFiles, string.Empty, ToolSyncAccess.Read)];

			var kind = Directory.Exists(fullPath) ? ToolSyncKeyKind.Directory : ToolSyncKeyKind.File;
			return [new(kind, fullPath, ToolSyncAccess.Read)];
		}

		/// <summary>
		/// Builds the keys of a call that touches a source and a target path.
		/// </summary>
		private static IReadOnlyList<ToolSyncKey> Pair(IToolExecutionContext ctx, JsonNode? args,
			ToolSyncKeyKind kind, ToolSyncAccess sourceAccess)
		{
			var source = Absolutize(ctx, args, "oldPath");
			var target = Absolutize(ctx, args, "newPath");

			if (source is null || target is null)
			{
				// The strongest access wins: an unparsable write side must still exclude readers.
				var access = sourceAccess == ToolSyncAccess.Write || source is null
					? ToolSyncAccess.Write
					: ToolSyncAccess.Read;

				return [new(ToolSyncKeyKind.AllFiles, string.Empty, access)];
			}

			return [new(kind, source, sourceAccess), new(kind, target, ToolSyncAccess.Write)];
		}

		/// <summary>
		/// Resolves one argument to an absolute path, or returns <see langword="null"/> when the argument
		/// is missing or cannot be resolved.
		/// </summary>
		private static string? Absolutize(IToolExecutionContext ctx, JsonNode? args, string pathName)
		{
			try
			{
				var path = args?[pathName]?.GetValue<string>();
				if (string.IsNullOrWhiteSpace(path))
					return null;

				var access = ctx.Services.GetService<IWorkingDirectoryAccessService>();
				if (access is null)
					return null;

				var fullPath = access.CheckedAccessPath(path, DirectoryAccessMode.Read, out _);
				return string.IsNullOrEmpty(fullPath) ? null : fullPath;
			}
			catch
			{
				return null;
			}
		}
	}
}
