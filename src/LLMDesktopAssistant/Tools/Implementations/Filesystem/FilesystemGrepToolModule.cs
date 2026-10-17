using System.ComponentModel;
using System.Text.RegularExpressions;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using Material.Icons;
using DomainPreviewResult = LLMDesktopAssistant.Tools.Execution.Domain.PreviewToolExecutionResult;

namespace LLMDesktopAssistant.Tools.Implementations.Filesystem
{
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
				Name = "fs-grep",
				Description = """
					Searches for pattern in files using regex.
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

		private DomainPreviewResult GrepPreview(IToolExecutionContext ctx,
			string path, string pattern, [SharedContext] out string fullPath)
		{
			fullPath = _fileAccess.CheckedAccessPath(path, DirectoryAccessMode.Read, out var isAccessed);
			bool fileExists = File.Exists(fullPath);
			bool dirExists = Directory.Exists(fullPath);

			if (!fileExists && !dirExists)
			{
				ctx.Call.SetStatus(MaterialIconKind.FileSearch, $"**{path}** → `{pattern}`");
				ctx.Call.SetResult($"File or directory not found: {path}");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = !isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None,
					InterruptingSuccess = false
				};
			}

			ctx.Call.SetStatus(MaterialIconKind.FileSearch, $"**{path}** → `{pattern}`");

			return new DomainPreviewResult
			{
				ExpectedBehaviour = ToolBehaviour.FileRead |
					(dirExists ? ToolBehaviour.DirectoryRead : ToolBehaviour.None) |
					(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None)
			};
		}

		private async Task<ToolExecutionResult> Grep(
			IToolExecutionContext ctx,
			[SharedContext] string? fullPath,
			[Description("The path where to search, can be file or directory path.")]
			string path,
			[Description("The .NET regex pattern to search for.")]
			string pattern,
			[Description("The file extensions to allow in search. Examples: [\".cs\", \".txt\"]")]
			string[] allowedExtensions,
			[Description("The maximum count of matched files to return.")]
			int limitFiles = 20,
			[Description("The maximum count of matched lines per file to return.")]
			int limitLinesPerFile = 20,
			[Description("Whether to ignore case when searching for the pattern.")]
			bool ignoreCase = false,
			[Description("Whether to return line numbers with the matched lines.")]
			bool lineNumbers = true,
			[Description("Whether to return non-matched lines instead of matched lines.")]
			bool invert = false,
			[Description("Whether to return only regex matches instead of entire lines.")]
			bool onlyMatching = false,
			[Description("The number of lines to show before each match.")]
			int beforeContext = 0,
			[Description("The number of lines to show after each match.")]
			int afterContext = 0,
			[Description("Whether to search recursively through subdirectories.")]
			bool recursive = true,
			[Description("The maximum length of each line to return.")]
			int maxLineLength = 2000,
			CancellationToken cancellationToken = default)
		{
			ctx.Call.SetStatus(MaterialIconKind.FileSearch, $"**{path}** → `{pattern}`");

			try
			{
				var workingDirectory = _fileAccess.GetWorkingDirectory();
				fullPath ??= _fileAccess.AccessPath(path, DirectoryAccessMode.Read);
				var regexIgnoreCaseOptions = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
				var regex = new Regex(pattern, regexIgnoreCaseOptions | RegexOptions.Compiled);

				var filesToSearch = new List<string>();
				var results = new List<string>();
				int totalFilesMatched = 0;

				bool fileExists = File.Exists(fullPath);
				bool dirExists = Directory.Exists(fullPath);

				if (!fileExists && !dirExists)
				{
					ctx.Call.SetResult($"File or directory not found: {path}");
					return ToolExecutionResult.Failed;
				}

				if (dirExists && (allowedExtensions == null || allowedExtensions.Length == 0))
				{
					ctx.Call.SetResult("Allowed extensions cannot be empty when searching inside directory.");
					return ToolExecutionResult.Failed;
				}

				return await Task.Run(() =>
				{
					try
					{
						ctx.Call.SetStatus(MaterialIconKind.FileMultiple, $"**{path}** → `{pattern}` " +
							LocalizationManager.LocalizeStatic("tool.status.fs-grep.collecting_files"));

						if (fileExists)
						{
							filesToSearch.Add(fullPath);
						}
						else if (dirExists)
						{
							var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
							var allFiles = Directory.EnumerateFiles(fullPath, "*", searchOption);

							foreach (var file in allFiles)
							{
								cancellationToken.ThrowIfCancellationRequested();

								var ext = Path.GetExtension(file);
								if (!allowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
									continue;

								filesToSearch.Add(file);
								ctx.Call.SetStatusTitle($"**{path}** → `{pattern}` " + string.Format(
									LocalizationManager.LocalizeStatic("tool.status.fs-grep.collecting_files_count"),
									filesToSearch.Count));
							}
						}

						ctx.Call.SetStatus(MaterialIconKind.FileSearch, $"**{path}** → `{pattern}` " +
							LocalizationManager.LocalizeStatic("tool.status.fs-grep.scanning_files"));
						ctx.Call.SetProgress(0, 0, filesToSearch.Count);

						int progress = 0;
						foreach (var file in filesToSearch)
						{
							ctx.Call.SetProgress(++progress, 0, filesToSearch.Count);
							cancellationToken.ThrowIfCancellationRequested();

							var relativePath = Path.GetRelativePath(workingDirectory, file);
							var fileMatches = SearchInFile(workingDirectory, file, regex, limitLinesPerFile, invert, onlyMatching,
															lineNumbers, beforeContext, afterContext, cancellationToken);
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
								if (totalFilesMatched >= limitFiles)
									break;
							}
						}

						if (ctx.Call.GetResultLines().Length == 0)
							ctx.Call.AppendResultLine("No matches found.");

						ctx.Call.SetStatus(MaterialIconKind.FileCheck, $"**{path}** → `{pattern}` " +
							string.Format(LocalizationManager.LocalizeStatic("tool.status.fs-grep.completed"), totalFilesMatched));
						return ToolExecutionResult.Succeeded;
					}
					catch (Exception ex)
					{
						ctx.Call.AppendResultLine("Error occured: " + ex.Message);
						ctx.Call.SetStatus(MaterialIconKind.FileDocumentError, $"**{path}** → `{pattern}`");
						return ToolExecutionResult.Failed;
					}
				}, cancellationToken);
			}
			catch (Exception ex)
			{
				ctx.Call.SetResult($"Grep error: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private List<string> SearchInFile(
			string workDir,
			string filePath,
			Regex regex,
			int limitLinesPerFile,
			bool invert,
			bool onlyMatching,
			bool showLineNumbers,
			int beforeContext,
			int afterContext,
			CancellationToken cancellationToken = default)
		{
			string[] lines = File.ReadAllLines(filePath);
			try
			{
				lines = File.ReadAllLines(filePath);
			}
			catch
			{
				var relativePath = Path.GetRelativePath(workDir, filePath);
				return [$"Cannot read file {relativePath}: it may be corrupted or taken by another process."];
			}

			var matches = new List<string>();
			int count = 0;

			for (int i = 0; i < lines.Length; i++)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var line = lines[i];
				var isMatch = regex.IsMatch(line);

				if (invert)
					isMatch = !isMatch;

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

			return matches;
		}
	}
}
