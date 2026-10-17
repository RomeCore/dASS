using System.ComponentModel;
using System.Text;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Utils;
using LLMDesktopAssistant.Utils.Files;
using Material.Icons;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace LLMDesktopAssistant.Tools.Implementations.Filesystem
{
	/// <summary>
	/// Tool module that provides glob-based file and directory searching.
	/// Uses Microsoft.Extensions.FileSystemGlobbing under the hood.
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
				Name = "fs-glob",
				Description = """
					Finds files and directories matching a glob pattern.

					Supported pattern syntax:
					- `*`       Matches any characters except directory separator
					- `**`      Matches any characters including directory separators (recursive)

					Differences from bash's `glob`:
					- NO one-character matching like [Aa]
					- NO one-word matching like {abc,def}

					Examples:
					- `**/*.cs`               - all C# files recursively
					- `*.txt`                 - all text files in root only
					- `src/**/test*`          - files starting with 'test' in any subdirectory of 'src'
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

		private PreviewToolExecutionResult GlobPreview(IToolExecutionContext ctx,
			string path, string pattern, [SharedContext] out string fullPath)
		{
			fullPath = _fileAccess.CheckedAccessPath(path, DirectoryAccessMode.Read, out var isAccessed);

			if (!Directory.Exists(fullPath))
			{
				ctx.Call.SetStatus(MaterialIconKind.FolderSearch, $"**{path}** → **{pattern.MarkdownEscape()}**");
				ctx.Call.SetResult($"Directory not found: {path}");
			}

			ctx.Call.SetStatus(MaterialIconKind.FolderSearch, $"**{path}** → **{pattern.MarkdownEscape()}**");

			return new PreviewToolExecutionResult
			{
				ExpectedBehaviour = ToolBehaviour.DirectoryRead |
					(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None)
			};
		}

		private async Task<ToolExecutionResult> Glob(
			IToolExecutionContext ctx,
			[SharedContext] string? fullPath,
			[Description("The glob pattern to search for, e.g. '**/*.cs' or '*.txt'")]
			string pattern,
			[Description("The starting directory path. If empty or '.', uses the current working directory.")]
			string path = ".",
			[Description("The maximum number of results to return. Use 0 for unlimited.")]
			int limit = 100,
			[Description("Include files in results.")]
			bool files = true,
			[Description("Include directories in results.")]
			bool directories = false,
			[Description("Whether to show hidden files and directories (those starting with dot).")]
			bool showHidden = false,
			[Description("Whether to return paths relative to the working directory.")]
			bool relativePaths = true,
			CancellationToken cancellationToken = default)
		{
			try
			{
				fullPath ??= _fileAccess.AccessPath(path, DirectoryAccessMode.Read);
				var workingDirectory = _fileAccess.GetWorkingDirectory();

				return await Task.Run(() =>
				{
					try
					{
						ctx.Call.SetStatus(MaterialIconKind.FolderSearch, $"**{path}** → **{pattern.MarkdownEscape()}**");

						var matcher = new Matcher();
						matcher.AddInclude(pattern);

						var matchingFiles = new List<string>();
						var matchingDirectories = new List<string>();

						// Search files via Matcher
						if (files)
						{
							var globResult = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(fullPath)));

							foreach (var file in globResult.Files)
							{
								cancellationToken.ThrowIfCancellationRequested();

								var filePath = Path.GetFullPath(Path.Combine(fullPath, file.Path));

								if (!showHidden)
								{
									var fileName = Path.GetFileName(filePath);
									if (fileName.StartsWith("."))
										continue;
								}

								try
								{
									var metrics = FileUtils.GetFileMetrics(filePath);
									var lines = metrics.LineCount != null ? $"{metrics.LineCount} lines" : "binary";

									var displayPath = relativePaths
										? Path.GetRelativePath(workingDirectory, filePath)
										: filePath;

									matchingFiles.Add($"[FILE] {displayPath} ({FileUtils.BytesToDisplaySize(metrics.Size)}, {lines}, {metrics.Modified:yyyy-MM-dd HH:mm})");

									ctx.Call.SetStatusTitle($"**{path}** → **{pattern.MarkdownEscape()}** {LocalizationManager.LocalizeStaticFormat("tool.status.fs-glob.found_count", matchingFiles.Count + matchingDirectories.Count)}");

									if (limit > 0 && matchingFiles.Count + matchingDirectories.Count >= limit)
										break;
								}
								catch
								{
									// skip inaccessible files
								}
							}
						}

						// Search directories via Directory.GetDirectories + Matcher.Match
						if (directories && (limit == 0 || matchingFiles.Count + matchingDirectories.Count < limit))
						{
							var allDirs = Directory.GetDirectories(fullPath, "*", SearchOption.AllDirectories);

							foreach (var dir in allDirs)
							{
								cancellationToken.ThrowIfCancellationRequested();

								if (!showHidden)
								{
									var dirName = Path.GetFileName(dir);
									if (dirName.StartsWith("."))
										continue;
								}

								var relDir = Path.GetRelativePath(fullPath, dir);

								// Check if directory matches the glob pattern using Matcher
								var dirMatcher = new Matcher();
								dirMatcher.AddInclude(pattern);
								var dirMatchResult = dirMatcher.Match(fullPath, relDir);
								if (!dirMatchResult.HasMatches)
									continue;

								try
								{
									var dirInfo = new DirectoryInfo(dir);
									int items;
									try
									{
										items = dirInfo.GetFileSystemInfos().Length;
									}
									catch
									{
										items = 0;
									}

									var displayPath = relativePaths
										? Path.GetRelativePath(workingDirectory, dir)
										: dir;

									matchingDirectories.Add($"[DIR] {displayPath} ({items} items, {dirInfo.LastWriteTime:yyyy-MM-dd HH:mm})");

									ctx.Call.SetStatusTitle($"**{path}** → **{pattern.MarkdownEscape()}** {LocalizationManager.LocalizeStaticFormat("tool.status.fs-glob.found_count", matchingFiles.Count + matchingDirectories.Count)}");

									if (limit > 0 && matchingFiles.Count + matchingDirectories.Count >= limit)
										break;
								}
								catch
								{
									// skip inaccessible directories
								}
							}
						}

						// Build output
						var totalFiles = matchingFiles.Count;
						var totalDirs = matchingDirectories.Count;
						var total = totalFiles + totalDirs;

						var sb = new StringBuilder();
						sb.AppendLine("[GLOB RESULTS]");
						sb.AppendLine($"Pattern: {pattern}");
						sb.AppendLine($"Path: {path}");
						sb.AppendLine($"Found: {total} item(s)");
						if (limit > 0 && total >= limit)
							sb.AppendLine($"(Limited to {limit} results)");
						sb.AppendLine();

						// Directories first, then files
						foreach (var dir in matchingDirectories)
							sb.AppendLine(dir);
						foreach (var file in matchingFiles)
							sb.AppendLine(file);

						if (total == 0)
						{
							sb.AppendLine("No matches found.");
							ctx.Call.SetStatus(MaterialIconKind.FolderOpen, $"**{path}** → **{pattern.MarkdownEscape()}** {LocalizationManager.LocalizeStaticFormat("tool.status.fs-glob.results", total)}");
						}
						else
						{
							ctx.Call.SetStatus(MaterialIconKind.FolderMultiple, $"**{path}** → **{pattern.MarkdownEscape()}** {LocalizationManager.LocalizeStaticFormat("tool.status.fs-glob.results", total)}");
						}

						ctx.Call.SetResult(sb.ToString());
						return ToolExecutionResult.Succeeded;
					}
					catch (OperationCanceledException)
					{
						ctx.Call.SetResult("Glob search cancelled.");
						return ToolExecutionResult.Failed;
					}
					catch (Exception ex)
					{
						ctx.Call.SetStatus(null, null);
						ctx.Call.SetResult($"Error during glob search: {ex.Message}");
						return ToolExecutionResult.Failed;
					}
				}, cancellationToken);
			}
			catch (Exception ex)
			{
				ctx.Call.SetResult($"Glob error: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}
	}
}
