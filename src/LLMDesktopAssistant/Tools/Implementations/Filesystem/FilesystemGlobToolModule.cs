using System.ComponentModel;
using System.Text;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Utils;
using LLMDesktopAssistant.Utils.Files;
using LLMDesktopAssistant.Utils.Patterns;
using Material.Icons;

namespace LLMDesktopAssistant.Tools.Implementations.Filesystem
{
	/// <summary>
	/// Tool module that provides glob-based file and directory searching on top of the shared
	/// pattern layer, so a wildcard means the same thing here as it does in a name filter.
	/// </summary>
	[ToolModule]
	public class FilesystemGlobToolModule : ToolModule
	{
		private readonly IWorkingDirectoryAccessService _fileAccess;

		public FilesystemGlobToolModule(IWorkingDirectoryAccessService fileAccess)
		{
			_fileAccess = fileAccess;

			AddTool(new ToolInitializationInfo
			{
				Executor = Glob,
				StreamingAnalyzer = GlobStreaming,
				PreviewExecutor = GlobPreview,
				SyncAnalyzer = FilesystemSynchronizationAnalyzer.AnalyzePathRead,
				Name = "fs-glob",
				Description = """
					Finds files and directories by glob pattern.
					Syntax: *, ?, [...], {...}, **. No backslash escaping; '**' must be a whole segment.
					A leading directory in the pattern becomes the search root, so an absolute pattern works.
					Syntax the dialect does not cover fails with a reason instead of returning no matches.
					""",
				NameKey = Locale.GetKey("tool.name.fs-glob"),
				DescriptionKey = Locale.GetKey("tool.description.fs-glob"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.DirectoryRead | ToolBehaviour.AccessOutsideWorkdir
			});
		}

		private StreamingToolAnalysisResult GlobStreaming(
			IToolExecutionContext ctx,
			string? path, string? pattern)
		{
			path ??= "?";
			ctx.Call.SetStatus(MaterialIconKind.FileSearch,
				pattern != null ? $"**{path}** → **{pattern.MarkdownEscape()}**" : $"**{path}**");
			return StreamingToolAnalysisResult.Continue;
		}

		private PreviewToolExecutionResult GlobPreview(
			IToolExecutionContext ctx,
			string path, string pattern,
			[SharedContext] out GlobSearch? search)
		{
			search = null;

			try
			{
				var (resolved, outside) = ResolveSearch(path, pattern);

				search = resolved;
				ctx.Call.SetStatus(MaterialIconKind.FolderSearch,
					$"**{path.MarkdownEscape()}** → **{pattern.MarkdownEscape()}**");

				if (!Directory.Exists(resolved.Root))
				{
					ctx.Call.SetResult(
						$"Search root does not exist: {resolved.Root}");
					return new PreviewToolExecutionResult { InterruptingSuccess = false };
				}

				return new PreviewToolExecutionResult
				{
					ExpectedBehaviour = ToolBehaviour.DirectoryRead
						| (outside ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None)
				};
			}
			catch (GlobException exception)
			{
				ctx.Call.SetStatus(null, null);
				ctx.Call.SetResult(exception.Message);
				return new PreviewToolExecutionResult { InterruptingSuccess = false };
			}
		}

		private async Task<ToolExecutionResult> Glob(
			IToolExecutionContext ctx,
			[SharedContext] GlobSearch? search,
			[Description("The glob pattern to search for, e.g. '**/*.cs' or '*.txt'")]
			[MainArgument] string pattern,
			[Description("The starting directory path. If empty or '.', uses the current working directory.")]
			[PathArgument(File = false, Directory = true)] string path = ".",
			[Description("The maximum number of results to return. Use 0 for unlimited.")]
			int limit = 100,
			[Description("Include files in results.")]
			bool files = true,
			[Description("Include directories in results.")]
			bool directories = false,
			[Description("Names or single-segment patterns of entries to skip. A matching directory is not descended into. Pass an empty array to skip nothing.")]
			string[]? ignore = null,
			CancellationToken cancellationToken = default)
		{
			try
			{
				if (search is null)
				{
					var (resolved, _) = ResolveSearch(path, pattern);
					search = resolved;
				}

				var options = new GlobWalkOptions
				{
					Files = files,
					Directories = directories,
					Limit = limit,
					Ignore = ignore is null
						? GlobTextSet.DefaultIgnore
						: GlobTextSet.ParseNames(ignore, GlobCase.PlatformDefault)
				};

				var current = search;
				var workdir = _fileAccess.GetWorkingDirectory();

				return await Task.Run(() =>
				{
					try
					{
						ctx.Call.SetStatus(MaterialIconKind.FolderSearch,
							$"**{path.MarkdownEscape()}** → **{pattern.MarkdownEscape()}**");
						int found = 0;

						foreach (var entry in GlobWalker.Enumerate(current, options, cancellationToken))
						{
							ctx.Call.AppendResultLine(Describe(current, entry, workdir));
							found++;

							ctx.Call.SetStatusTitle(
								$"**{path.MarkdownEscape()}** → **{pattern.MarkdownEscape()}** "
								+ LocalizationManager.LocalizeStaticFormat("tool.status.fs-glob.found_count", found));
						}

						if (limit > 0 && found >= limit)
							ctx.Call.AppendResultLine($"(Limited to {limit} results)");

						if (found == 0)
							ctx.Call.AppendResultLine("No matches found.");

						ctx.Call.SetStatus(MaterialIconKind.FolderMultiple,
							$"**{path.MarkdownEscape()}** → **{pattern.MarkdownEscape()}** "
							+ LocalizationManager.LocalizeStaticFormat("tool.status.fs-glob.results", found));

						return ToolExecutionResult.Succeeded;
					}
					catch (OperationCanceledException)
					{
						ctx.Call.AppendResultLine("Glob search cancelled.");
						return ToolExecutionResult.Failed;
					}
					catch (GlobException exception)
					{
						ctx.Call.SetStatus(null, null);
						ctx.Call.AppendResultLine(exception.Message);
						return ToolExecutionResult.Failed;
					}
					catch (Exception exception)
					{
						ctx.Call.SetStatus(null, null);
						ctx.Call.AppendResultLine($"Error during glob search: {exception.Message}");
						return ToolExecutionResult.Failed;
					}
				}, cancellationToken);
			}
			catch (GlobException exception)
			{
				ctx.Call.AppendResultLine(exception.Message);
				return ToolExecutionResult.Failed;
			}
			catch (Exception exception)
			{
				ctx.Call.AppendResultLine($"Glob error: {exception.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		/// <summary>
		/// Resolves the starting path and the pattern prefix into one root, and reports whether
		/// either of the two paths the user named lies outside the working directory. Both are
		/// checked: the pattern prefix can move the root anywhere, including outside the path.
		/// </summary>
		private (GlobSearch Search, bool Outside) ResolveSearch(string path, string pattern)
		{
			var startingPath = _fileAccess.CheckedAccessPath(path, DirectoryAccessMode.Read,
				out var isAccessed);

			var resolved = GlobSearch.From(pattern, startingPath, GlobPathMatcher.DefaultOptions);

			var root = _fileAccess.CheckedAccessPath(resolved.Root, DirectoryAccessMode.Read,
				out var isRootAccessed);

			return (resolved.WithRoot(root), !(isAccessed && isRootAccessed));
		}

		/// <summary>
		/// Renders one result line. Metrics are read here, after the walk has already applied the
		/// limit, so an entry that never reaches the output is never opened.
		/// </summary>
		private static string Describe(GlobSearch search, GlobEntry entry, string workdir)
		{
			var display = Path.GetRelativePath(workdir, entry.FullPath);

			try
			{
				if (entry.IsDirectory)
				{
					var info = new DirectoryInfo(entry.FullPath);
					return $"[DIR] {display} ({info.GetFileSystemInfos().Length} items, {info.LastWriteTime:yyyy-MM-dd HH:mm})";
				}

				var metrics = FileUtils.GetFileMetrics(entry.FullPath);
				var lines = metrics.LineCount != null ? $"{metrics.LineCount} lines" : "binary";

				return $"[FILE] {display} ({FileUtils.BytesToDisplaySize(metrics.Size)}, {lines}, {metrics.Modified:yyyy-MM-dd HH:mm})";
			}
			catch
			{
				return entry.IsDirectory ? $"[DIR] {display}" : $"[FILE] {display}";
			}
		}
	}
}
