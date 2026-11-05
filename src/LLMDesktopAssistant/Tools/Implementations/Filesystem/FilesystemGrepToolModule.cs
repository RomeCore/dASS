using System.ComponentModel;
using System.Text.RegularExpressions;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Utils.Files;
using LLMDesktopAssistant.Utils.Patterns;
using Material.Icons;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Tools.Implementations.Filesystem
{
	/// <summary>
	/// Tool module that searches file contents with a regular expression over the files a glob path
	/// selects. The glob path does both jobs at once: it names the file to read, or the set of files
	/// to search, and its literal prefix becomes the directory the walk starts from.
	/// </summary>
	[ToolModule]
	public class FilesystemGrepToolModule : ToolModule
	{
		private readonly IWorkingDirectoryAccessService _fileAccess;

		public FilesystemGrepToolModule(IWorkingDirectoryAccessService fileAccess)
		{
			_fileAccess = fileAccess;

			AddTool(new ToolInitializationInfo
			{
				Executor = Grep,
				StreamingAnalyzer = GrepStreaming,
				PreviewExecutor = GrepPreview,
				SyncAnalyzer = FilesystemSynchronizationAnalyzer.AnalyzePathRead,
				Name = "fs-grep",
				Description = """
					Searches for a regex pattern in the files selected by a glob path.
					The path names either one file ('src/Program.cs') or a set ('src/**/*.{md,cs,razor}').
					Use '**' to recurse; without it the pattern stays in the directory it names.
					Use this tool with care, as it can be slow and resource-intensive.
					""",
				NameKey = Locale.GetKey("tool.name.fs-grep"),
				DescriptionKey = Locale.GetKey("tool.description.fs-grep"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.DirectoryRead | ToolBehaviour.FileRead | ToolBehaviour.AccessOutsideWorkdir
			});
		}

		private StreamingToolAnalysisResult GrepStreaming(
			IToolExecutionContext ctx,
			string? path, string? pattern)
		{
			path ??= "?";
			ctx.Call.SetStatus(MaterialIconKind.FileSearch,
				pattern != null ? $"**{path}** → `{pattern}`" : $"**{path}**");
			return StreamingToolAnalysisResult.Continue;
		}

		private PreviewToolExecutionResult GrepPreview(
			IToolExecutionContext ctx,
			string path, string pattern,
			[SharedContext] out GlobSearch? search)
		{
			search = null;

			try
			{
				var (resolved, outside) = ResolveSearch(path);

				search = resolved;
				ctx.Call.SetStatus(MaterialIconKind.FileSearch,
					$"**{path.MarkdownEscape()}** → `{pattern}`");

				if (resolved.Pattern.IsEmpty && Directory.Exists(resolved.Root))
				{
					ctx.Call.SetResult(DirectoryInsteadOfFilesMessage(path));
					return new PreviewToolExecutionResult { InterruptingSuccess = false };
				}

				return new PreviewToolExecutionResult
				{
					ExpectedBehaviour = ToolBehaviour.DirectoryRead | ToolBehaviour.FileRead
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

		private async Task<ToolExecutionResult> Grep(
			IToolExecutionContext ctx,
			[SharedContext] GlobSearch? search,
			[Description("A glob path to search in: one file such as 'src/Program.cs', or a set such as 'src/**/*.{md,cs,razor}'. " +
				"Use '**' to recurse. For shallow search in folder use 'docs/*', for recursive search use 'docs/**/*'.")]
			string path,
			[Description("The .NET regex pattern to search for.")]
			string pattern,
			[Description("The maximum count of matched files to return.")]
			int limitFiles = 20,
			[Description("The maximum count of matched lines per file to return.")]
			int limitLinesPerFile = 20,
			[Description("Whether to ignore case when searching for the pattern.")]
			bool ignoreCase = false,
			[Description("Whether to return line numbers with the matched lines.")]
			bool lineNumbers = true,
			[Description("Whether to return only regex matches instead of entire lines.")]
			bool onlyMatching = false,
			[Description("The number of lines to show before each match.")]
			int beforeContext = 0,
			[Description("The number of lines to show after each match.")]
			int afterContext = 0,
			[Description("The maximum length of each line to return.")]
			int maxLineLength = 2000,
			[Description("Names or single-segment patterns of entries to skip. A matching directory is not descended into. Pass an empty array to skip nothing.")]
			string[]? ignore = null,
			CancellationToken cancellationToken = default)
		{
			ctx.Call.SetStatus(MaterialIconKind.FileSearch, $"**{path.MarkdownEscape()}** → `{pattern}`");

			try
			{
				search ??= ResolveSearch(path).Search;

				// A path that resolves to a directory selects nothing, and the walk would answer
				// "no matches" for what is really a wrong argument.
				if (Directory.Exists(Path.Combine(_fileAccess.GetWorkingDirectory(), path)))
				{
					ctx.Call.SetResult(DirectoryInsteadOfFilesMessage(path));
					return ToolExecutionResult.Failed;
				}

				// An empty pattern means the path named the entry itself, which may be a file;
				// a non-empty one still has to be matched, so the walk needs a directory to start from.
				var namesAFile = search.Pattern.IsEmpty && File.Exists(search.Root);
				if (!namesAFile && !Directory.Exists(search.Root))
				{
					ctx.Call.SetResult($"Path not found: {path}");
					return ToolExecutionResult.Failed;
				}

				var workingDirectory = _fileAccess.GetWorkingDirectory();
				var regexIgnoreCaseOptions = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
				var regex = new Regex(pattern, regexIgnoreCaseOptions | RegexOptions.Compiled);

				var options = new GlobWalkOptions
				{
					Files = true,
					Directories = false,
					Limit = 0,
					Ignore = ignore is null
						? GlobTextSet.DefaultIgnore
						: GlobTextSet.ParseNames(ignore)
				};

				var filesToSearch = new List<string>();

				return await Task.Run(() =>
				{
					try
					{
						ctx.Call.SetStatus(MaterialIconKind.FileMultiple, $"**{path.MarkdownEscape()}** → `{pattern}` " +
							LocalizationManager.LocalizeStatic("tool.status.fs-grep.collecting_files"));

						foreach (var entry in GlobWalker.Enumerate(search, options, cancellationToken))
						{
							cancellationToken.ThrowIfCancellationRequested();

							filesToSearch.Add(entry.FullPath);
							ctx.Call.SetStatusTitle($"**{path.MarkdownEscape()}** → `{pattern}` " + string.Format(
								LocalizationManager.LocalizeStatic("tool.status.fs-grep.collecting_files_count"),
								filesToSearch.Count));
						}

						ctx.Call.SetStatus(MaterialIconKind.FileSearch, $"**{path.MarkdownEscape()}** → `{pattern}` " +
							LocalizationManager.LocalizeStatic("tool.status.fs-grep.scanning_files"));
						ctx.Call.SetProgress(0, 0, filesToSearch.Count);

						int totalFilesMatched = 0;
						int totalMatches = 0;
						int progress = 0;

						foreach (var file in filesToSearch)
						{
							ctx.Call.SetProgress(++progress, 0, filesToSearch.Count);
							cancellationToken.ThrowIfCancellationRequested();

							var relativePath = Path.GetRelativePath(workingDirectory, file);
							var (fileMatches, matchCount) = SearchInFile(workingDirectory, file, regex, limitLinesPerFile,
								onlyMatching, lineNumbers, beforeContext, afterContext, cancellationToken);
							totalMatches += matchCount;

							if (fileMatches.Count > 0)
							{
								fileMatches = fileMatches.Select(m =>
								{
									if (m.Length > maxLineLength)
										return string.Concat(m.AsSpan(0, maxLineLength), $"...truncated, {m.Length - maxLineLength} more characters");
									return m;
								}).ToList();

								ctx.Call.AppendResultLine($"\n--- {relativePath} ---");
								foreach (var fileMatch in fileMatches)
									ctx.Call.AppendResultLine(fileMatch);

								totalFilesMatched++;
							}

							// The counters live in the title, so the caller watches the match count grow while the
							// scan runs instead of learning it only from the final status.
							ctx.Call.SetStatusTitle($"**{path.MarkdownEscape()}** → `{pattern}` " + string.Format(
								LocalizationManager.LocalizeStatic("tool.status.fs-grep.scanning_progress"),
								progress, filesToSearch.Count, totalMatches, totalFilesMatched));

							if (fileMatches.Count > 0 && totalFilesMatched >= limitFiles)
								break;
						}

						if (ctx.Call.GetResultLines().Length == 0)
							ctx.Call.AppendResultLine("No matches found.");

						ctx.Call.SetStatus(MaterialIconKind.FileCheck, $"**{path.MarkdownEscape()}** → `{pattern}` " +
							string.Format(LocalizationManager.LocalizeStatic("tool.status.fs-grep.completed"),
								totalFilesMatched, totalMatches));
						return ToolExecutionResult.Succeeded;
					}
					catch (OperationCanceledException)
					{
						ctx.Call.AppendResultLine("Grep cancelled.");
						return ToolExecutionResult.Failed;
					}
					catch (Exception ex)
					{
						ctx.Call.AppendResultLine("Error occured: " + ex.Message);
						ctx.Call.SetStatus(MaterialIconKind.FileDocumentError, $"**{path.MarkdownEscape()}** → `{pattern}`");
						return ToolExecutionResult.Failed;
					}
				}, cancellationToken);
			}
			catch (GlobException exception)
			{
				ctx.Call.AppendResultLine(exception.Message);
				return ToolExecutionResult.Failed;
			}
			catch (Exception ex)
			{
				ctx.Call.AppendResultLine($"Grep error: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		/// <summary>
		/// Resolves the glob path against the working directory and checks the root it lands on.
		/// Only the resolved root matters: the pattern prefix can move it anywhere.
		/// </summary>
		private (GlobSearch Search, bool Outside) ResolveSearch(string path)
		{
			var resolved = GlobSearch.From(path, _fileAccess.GetWorkingDirectory());

			var root = _fileAccess.CheckedAccessPath(resolved.Root, DirectoryAccessMode.Read,
				out var isRootAccessed);

			return (resolved.WithRoot(root), !isRootAccessed);
		}

		/// <summary>
		/// The message for a path that names a directory, where the caller meant files: a directory
		/// alone selects nothing, which would otherwise read as "no matches".
		/// </summary>
		private static string DirectoryInsteadOfFilesMessage(string path)
		{
			var basePath = path.TrimEnd('/', '\\');
			return $"'{path}' names a directory, not files. Use a file pattern such as '{basePath}/**/*'.";
		}

		private (List<string> Lines, int MatchCount) SearchInFile(
			string workDir,
			string filePath,
			Regex regex,
			int limitLinesPerFile,
			bool onlyMatching,
			bool showLineNumbers,
			int beforeContext,
			int afterContext,
			CancellationToken cancellationToken = default)
		{
			string[] lines;
			try
			{
				lines = File.ReadAllLines(filePath);
			}
			catch
			{
				var relativePath = Path.GetRelativePath(workDir, filePath);
				return ([$"Cannot read file {relativePath}: it may be corrupted or taken by another process."], 0);
			}

			var matches = new List<string>();
			int count = 0;

			for (int i = 0; i < lines.Length; i++)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var line = lines[i];
				var isMatch = regex.IsMatch(line);

				if (!isMatch)
					continue;

				// Add context lines before
				if (beforeContext > 0)
				{
					int contextStart = Math.Max(0, i - beforeContext);
					for (int ctx = contextStart; ctx < i; ctx++)
					{
						if (!matches.Contains($"{ctx + 1}: {lines[ctx]}"))
						{
							if (showLineNumbers)
								matches.Add($"{ctx + 1}: {lines[ctx]}");
							else
								matches.Add(lines[ctx]);
						}
					}
				}

				// Add the matching line
				if (onlyMatching)
				{
					var matchesCollection = regex.Matches(line);
					foreach (Match m in matchesCollection)
					{
						if (showLineNumbers)
							matches.Add($"{i + 1}: {m.Value}");
						else
							matches.Add(m.Value);
					}
				}
				else
				{
					if (showLineNumbers)
						matches.Add($"{i + 1}: {line}");
					else
						matches.Add(line);
				}

				// Add context lines after
				if (afterContext > 0)
				{
					int contextEnd = Math.Min(lines.Length, i + afterContext + 1);
					for (int ctx = i + 1; ctx < contextEnd; ctx++)
					{
						if (!matches.Contains($"{ctx + 1}: {lines[ctx]}"))
						{
							if (showLineNumbers)
								matches.Add($"{ctx + 1}: {lines[ctx]}");
							else
								matches.Add(lines[ctx]);
						}
					}
				}

				count++;
				if (count >= limitLinesPerFile)
					break;
			}

			return (matches, count);
		}
	}
}
