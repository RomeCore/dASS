using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Utils.Files;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Loading;
using LLMDesktopAssistant.Addons.Management;
using Material.Icons;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Tools.Implementations.Filesystem
{
	/// <summary>
	/// Universal file editing tool that applies a list of replacement patches to a text file.
	/// Supports plain text (with flexible whitespace matching and file-relative indentation) and
	/// regex patterns, refuses ambiguous patches, and writes the file back in its own encoding.
	/// </summary>
	[ToolModule]
	public class FilesystemEditToolModule : FileSystemEditBaseToolModule
	{
		private readonly IWorkingDirectoryAccessService _fileAccess;
		private readonly IAddonPathImpactDetector _addonDetector;
		private readonly IAddonManagerInvalidator _addonInvalidator;

		public FilesystemEditToolModule(IWorkingDirectoryAccessService fileAccess,
			IAddonPathImpactDetector addonDetector, IAddonManagerInvalidator addonInvalidator)
		{
			_fileAccess = fileAccess;
			_addonDetector = addonDetector;
			_addonInvalidator = addonInvalidator;

			AddTool(new ToolInitializationInfo
			{
				Executor = Edit,
				StreamingAnalyzer = EditStreaming,
				PreviewExecutor = EditPreview,
				Approval = ApproveEdit,
				Name = "fs-edit",
				Description = """
					Edits a text file by applying a list of replacement patches.

					Every patch must be unambiguous:
					- `match` found more than once refuses the patch unless `isMultiEdit` is true;
					- `match` found nowhere is reported and skipped;
					- the other patches of the call are still applied.

					Matching is lenient: leading and trailing whitespace and common indentation of
					`match` are ignored, and the text before and after it on the matched lines is kept.

					Writing is relative, not verbatim. The first line of `replace` lands where the match
					starts, and every following line moves by one and the same shift, so the block keeps
					the shape you wrote. The shift is read from the second line, never the first:

						file:     \t\tName = item,
						replace:  Sync = A,
								  \t\tName = item,
						result:   \t\tSync = A,
								  \t\tName = item,

					With a multi-line `match` the second matched line of the file sets the shift, so write
					the lines after the first the way you would copy them out of the file. With a one-line
					`match` there is nothing to align against: the shallowest continuation line lands at the
					match's own indentation and the rest follow it. So either write them at the columns the
					file actually uses, or write the whole block flat at column zero.

					A one-line patch cannot express "one level deeper than the first line": those columns
					are indistinguishable from the file's own, so they collapse onto the match's level.
					When you open a new nesting level, spell the level out in columns and check the diff.

					An empty `replace` deletes: whole matched lines go away together with their line
					breaks, and no indentation is left where the block used to be.

					Regex mode (useRegex = true):
					- `match` is a .NET regular expression
					- `replace` is inserted verbatim, with $1, ${name}, $&, $$, etc.
					- use it when you need exact characters instead of a shape

					Examples:
					- Insert a line above an existing one:
						fs-edit(path: "file.cs", patches: [{ match: "Name = item,", replace: "Sync = A,\n\t\tName = item," }])
					- Replace every occurrence on purpose:
						fs-edit(path: "file.cs", patches: [{ match: "foo", replace: "bar", isMultiEdit: true }])
					- Delete a member:
						fs-edit(path: "file.cs", patches: [{ match: "public void Old() {\n\t\tBody();\n\t}" }])
					- Regex rename with a capture group:
						fs-edit(path: "file.cs", patches: [{ match: "class (\\w+)", replace: "class Renamed_$1", useRegex: true }])
					""",
				NameKey = Locale.GetKey("tool.name.fs-edit"),
				DescriptionKey = Locale.GetKey("tool.description.fs-edit"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.FileEdit | ToolBehaviour.AccessOutsideWorkdir |
					ToolBehaviour.AddonPackEdit | ToolBehaviour.PromptEdit | ToolBehaviour.ScriptEdit,
				HandledDecisions = ToolPolicyDecision.Approve | ToolPolicyDecision.Ask,
				SyncAnalyzer = FilesystemSynchronizationAnalyzer.AnalyzeFileWrite
			});
		}

		/// <summary>
		/// A single edit operation that replaces every occurrence of a match with a replacement text.
		/// </summary>
		public sealed class EditPatch
		{
			/// <summary>
			/// Gets or sets the text or regex pattern to search for.
			/// Can include multiple lines in plain text mode.
			/// </summary>
			[Required]
			[Description("The text or regex pattern to search for. Can include multiple lines in plain text mode.")]
			public required string match { get; init; }

			/// <summary>
			/// Gets or sets the replacement text. An empty string deletes the match.
			/// In regex mode standard replacement syntax is supported: $1, ${name}, $&, $$, etc.
			/// </summary>
			[Required]
			[Description("The replacement text. An empty string deletes the match. In regex mode supports $1, ${name}, $&, $$.")]
			public string replace { get; init; } = "";

			/// <summary>
			/// Gets or sets a value indicating whether <see cref="match"/> is a .NET regular expression.
			/// </summary>
			[DefaultValue(false)]
			[Description("If true, 'match' is treated as a .NET regular expression instead of plain text.")]
			public bool useRegex { get; init; } = false;

			/// <summary>
			/// Gets or sets a value indicating whether case is ignored when matching.
			/// </summary>
			[DefaultValue(false)]
			[Description("If true, case is ignored when matching.")]
			public bool ignoreCase { get; init; } = false;

			/// <summary>
			/// Gets or sets a value indicating whether case is ignored when matching.
			/// </summary>
			[DefaultValue(false)]
			[Description("If true, multiline mode is used when matching. This affects regex patterns only.")]
			public bool isRegexMultiline { get; init; } = false;

			/// <summary>
			/// Gets or sets a value indicating whether the patch may replace more than one occurrence.
			/// </summary>
			/// <remarks>
			/// A match found twice is ambiguous: the author either knows and wants both places changed,
			/// or made a match too short to be safe. Without this flag the patch is refused instead of
			/// silently changing a place nobody looked at.
			/// </remarks>
			[DefaultValue(false)]
			[Description("If true, every occurrence of 'match' is replaced. If false (default), a match found more than once refuses the patch.")]
			public bool isMultiEdit { get; init; } = false;
		}

		/// <summary>
		/// The outcome of one pass over the patch list.
		/// </summary>
		/// <param name="Content">The content after every patch that landed.</param>
		/// <param name="Errors">The patches that were not applied, each with the reason.</param>
		/// <param name="Applied">The patches that landed, each with its occurrence count.</param>
		private sealed record PatchRun(string Content, List<string> Errors, List<string> Applied);

		private string? CheckArgs(string path, string? fullPath, List<EditPatch> patches)
		{
			if (fullPath == null)
				return $"Access outside working directory is not allowed: {path}";

			if (!File.Exists(fullPath))
				return $"File not found: {path}";

			if (patches == null || patches.Count == 0)
				return "'patches' parameter cannot be empty.";

			foreach (var patch in patches)
			{
				if (string.IsNullOrWhiteSpace(patch.match))
					return "'patches[].match' cannot be empty.";
			}

			return null;
		}

		private sealed class FSWriteSharedContext : DiffSharedContext
		{
			public IReadOnlyList<string> PatchErrors { get; init; } = [];

			public IReadOnlyList<string> AppliedPatches { get; init; } = [];
		}

		private StreamingToolAnalysisResult EditStreaming(
			IToolExecutionContext ctx,
			string? path)
		{
			path ??= "?";
			ctx.Call.SetStatus(MaterialIconKind.FileDocumentEdit, $"**{path.MarkdownEscape()}**");
			return StreamingToolAnalysisResult.Continue;
		}

		private PreviewToolExecutionResult EditPreview(
			IToolExecutionContext ctx,
			[SharedContext] ref FSWriteSharedContext? sharedCtx,
			string path,
			List<EditPatch> patches,
			[Description("The number of columns a tab character occupies when measuring indentation (default: 4).")]
			int tabSize = 4,
			CancellationToken cancellationToken = default)
		{
			var fullPath = _fileAccess.CheckedAccessPath(path, DirectoryAccessMode.ReadWrite, out var isAccessed);
			var addonKind = _addonDetector.Detect(path, isFile: true, FileOperation.Edit);
			var error = CheckArgs(path, fullPath, patches);
			if (error != null)
			{
				ctx.Call.SetStatus(MaterialIconKind.FileAlert, $"**{path.MarkdownEscape()}**");
				ctx.Call.SetResult(error);

				return new PreviewToolExecutionResult
				{
					InterruptingSuccess = false,
					ExpectedBehaviour = ToolBehaviour.None |
						(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
						AddonKindToolBehaviourConverter.Convert(addonKind)
				};
			}

			var source = TextFileContent.Read(fullPath!);
			var originalContent = source.Text;
			var run = ApplyPatches(originalContent, patches, tabSize, cancellationToken);

			if (run.Content == originalContent)
			{
				sharedCtx = new FSWriteSharedContext
				{
					Path = fullPath!,
					AddonKind = addonKind,
					Source = source,
					OriginalContent = originalContent,
					PreviewedContent = originalContent,
					FullDiff = EmptyDiff(),
					PatchErrors = run.Errors,
					AppliedPatches = run.Applied
				};

				ctx.Call.SetStatus(MaterialIconKind.FileQuestion,
					LocalizationManager.LocalizeStaticFormat("tool.status.fs-edit.changes_none", $"**{path}**"));
				ctx.Call.SetResult(DescribeNoChange(run));

				return new PreviewToolExecutionResult
				{
					InterruptingSuccess = true,
					ExpectedBehaviour = ToolBehaviour.None |
						(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
						AddonKindToolBehaviourConverter.Convert(addonKind)
				};
			}

			var (diffView, fullDiff) = PrepareDiff(ctx, path, originalContent, run.Content);
			sharedCtx = new FSWriteSharedContext
			{
				Path = fullPath!,
				AddonKind = addonKind,
				Source = source,
				OriginalContent = originalContent,
				PreviewedContent = run.Content,
				FullDiff = fullDiff,
				DiffView = diffView,
				PatchErrors = run.Errors,
				AppliedPatches = run.Applied
			};

			ctx.Call.SetStatus(MaterialIconKind.FileDocumentEdit, $"**{path.MarkdownEscape()}**");

			return new PreviewToolExecutionResult
			{
				ExpectedBehaviour = ToolBehaviour.FileEdit |
					(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
					AddonKindToolBehaviourConverter.Convert(addonKind),
				HandledDecisions = ctx.RunningInUi ? null : ToolPolicyDecision.None
			};
		}

		private static Task<ToolApprovalResult> ApproveEdit(
			IToolExecutionContext ctx,
			[SharedContext] FSWriteSharedContext? sharedCtx,
			CancellationToken cancellationToken) =>
			AwaitDiffConsentAsync(ctx, sharedCtx, cancellationToken);

		private async Task<ToolExecutionResult> Edit(
			[SharedContext] FSWriteSharedContext? sharedCtx,
			IToolExecutionContext ctx,
			CancellationToken cancellationToken,
			[Description("The path to the file to edit.")]
			string path,
			[Description("The list of patches to apply. Each patch replaces its match, and only if that match is unambiguous.")]
			List<EditPatch> patches,
			[Description("The number of columns a tab character occupies when measuring indentation (default: 4).")]
			int tabSize = 4)
		{
			try
			{
				var fullPath = sharedCtx?.Path ?? _fileAccess.AccessPath(path, DirectoryAccessMode.ReadWrite);
				var addonKind = sharedCtx?.AddonKind ?? _addonDetector.Detect(path, isFile: true, FileOperation.Edit);
				var error = CheckArgs(path, fullPath, patches);

				if (error != null)
				{
					ctx.Call.SetStatus(MaterialIconKind.FileAlert, $"**{path.MarkdownEscape()}**");
					ctx.Call.SetResult(error);
					return ToolExecutionResult.Failed;
				}

				var source = sharedCtx?.Source ?? TextFileContent.Read(fullPath!);
				var originalContent = sharedCtx?.OriginalContent ?? source.Text;
				var newContent = sharedCtx?.PreviewedContent;
				IReadOnlyList<string> patchErrors = sharedCtx?.PatchErrors ?? [];
				IReadOnlyList<string> appliedPatches = sharedCtx?.AppliedPatches ?? [];
				if (newContent == null)
				{
					var run = ApplyPatches(originalContent, patches, tabSize, cancellationToken);
					newContent = run.Content;
					patchErrors = run.Errors;
					appliedPatches = run.Applied;
				}

				if (newContent == originalContent)
				{
					ctx.Call.SetStatus(MaterialIconKind.FileQuestion,
						LocalizationManager.LocalizeStaticFormat("tool.status.fs-edit.changes_none", $"**{path}**"));
					ctx.Call.SetResult(DescribeNoChange(patchErrors, appliedPatches));
					return ToolExecutionResult.Succeeded;
				}

				var result = sharedCtx is null
					? BuildUnpreviewedResult(originalContent, newContent)
					: BuildDiffResult(sharedCtx);

				if (sharedCtx?.Approved == false ||
					(sharedCtx?.Approved == true && !result.AppliedDiff.HasGroups))
				{
					ctx.Call.SetStatus(MaterialIconKind.FileDiscard, $"**{path.MarkdownEscape()}**");
					ctx.Call.SetResult(result.RejectedDiff.HasGroups ?
						$"""
						User has rejected the changes, none has applied.
						[REJECTED CHANGES BY THE USER, THESE ARE NOT APPLIED]:
						{result.RejectedDiff}
						""" :
						"User has rejected the changes, none has applied.");
					return ToolExecutionResult.Succeeded;
				}

				await source.WriteAsync(fullPath!, result.NewContent, cancellationToken);
				_addonInvalidator.Invalidate(addonKind);

				var diff = result.AppliedDiff;
				var (removed, added) = diff.GetChangeCounts();
				var warning = source.GetDecodingWarning();

				ctx.Call.SetStatus(warning != null ? MaterialIconKind.FileAlert : MaterialIconKind.FileDocumentEdit,
					$"**{path.MarkdownEscape()}** *(-{removed} +{added})*");
				ctx.Call.SetResult(BuildReport(removed, added, diff, result.RejectedDiff, patchErrors, appliedPatches, warning));
				return ToolExecutionResult.Succeeded;
			}
			catch (OperationCanceledException)
			{
				ctx.Call.SetStatus(MaterialIconKind.FileQuestion, $"**{path.MarkdownEscape()}**");
				ctx.Call.SetResult("Edit operation was cancelled.");
				return ToolExecutionResult.Failed;
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatus(MaterialIconKind.FileAlert, $"**{path.MarkdownEscape()}**");
				ctx.Call.SetResult($"Error editing file: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		/// <summary>
		/// Builds the model-facing report: what landed, what did not and why, the diff, and the
		/// encoding warning when the file could not be decoded cleanly.
		/// </summary>
		private static string BuildReport(
			int removed,
			int added,
			HunkGroups diff,
			HunkGroups rejectedDiff,
			IReadOnlyList<string> patchErrors,
			IReadOnlyList<string> appliedPatches,
			string? warning)
		{
			var report = new System.Text.StringBuilder();
			report.Append($"File edited successfully. *(-{removed} +{added})*");

			AppendSection(report, "[PATCHES APPLIED]:", appliedPatches);
			report.AppendLine();
			report.AppendLine("[APPLIED CHANGES]:");
			report.AppendLine(diff.ToString());

			if (rejectedDiff.HasGroups)
			{
				report.AppendLine("[REJECTED CHANGES BY THE USER, THESE ARE NOT APPLIED]:");
				report.AppendLine(rejectedDiff.ToString());
			}

			AppendSection(report, "[NOT APPLIED PATCHES]:", patchErrors);

			if (warning != null)
				report.AppendLine(warning);

			return report.ToString();
		}

		private static void AppendSection(System.Text.StringBuilder report, string title, IReadOnlyList<string> lines)
		{
			if (lines.Count == 0)
				return;

			report.AppendLine();
			report.AppendLine(title);
			foreach (var line in lines)
				report.AppendLine(line);
		}

		private static string DescribeNoChange(PatchRun run)
			=> DescribeNoChange(run.Errors, run.Applied);

		private static string DescribeNoChange(IReadOnlyList<string> errors, IReadOnlyList<string> applied)
		{
			if (errors.Count > 0)
				return $"No changes were made to the file.\n{string.Join("\n", errors)}";

			if (applied.Count > 0)
				return $"No changes were made to the file.\n{string.Join("\n", applied)}";

			return "No changes were made to the file. The specified matches were not found.";
		}

		private static PatchRun ApplyPatches(
			string content,
			List<EditPatch> patches,
			int tabSize,
			CancellationToken cancellationToken)
		{
			var errors = new List<string>();
			var applied = new List<string>();
			var current = content;

			for (var i = 0; i < patches.Count; i++)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var patch = patches[i];
				var label = $"Patch #{i + 1} (match: \"{patch.match}\")";

				if (patch.useRegex)
				{
					Regex regex;
					try
					{
						regex = new Regex(patch.match, RegexOptions.Compiled
							| (patch.ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None)
							| (patch.isRegexMultiline ? RegexOptions.Multiline : RegexOptions.None));
					}
					catch (ArgumentException ex)
					{
						errors.Add($"{label}: invalid pattern: {ex.Message}");
						continue;
					}

					var count = regex.Matches(current).Count;
					if (IsRefused(label, count, patch, errors))
						continue;

					current = regex.Replace(current, patch.replace);
					applied.Add($"{label}: {DescribeOccurrences(count)} replaced.");
					continue;
				}

				var application = FilePatchedReplacer.Replace(current, patch.match, patch.replace, tabSize, patch.ignoreCase);
				if (IsRefused(label, application.Occurrences, patch, errors))
					continue;

				current = application.Content!;
				applied.Add($"{label}: {DescribeOccurrences(application.Occurrences)} replaced.");
			}

			return new PatchRun(current, errors, applied);
		}

		/// <summary>
		/// Decides whether a patch must be refused and records why. A match found more than once is
		/// ambiguous, so it lands only when the model asked for every occurrence explicitly.
		/// </summary>
		private static bool IsRefused(string label, int occurrences, EditPatch patch, List<string> errors)
		{
			if (occurrences == 0)
			{
				errors.Add($"{label}: no occurrences found.");
				return true;
			}

			if (occurrences > 1 && !patch.isMultiEdit)
			{
				errors.Add($"{label}: found {occurrences} occurrences, isMultiEdit is not set.");
				return true;
			}

			return false;
		}

		private static string DescribeOccurrences(int count)
			=> count == 1 ? "1 occurrence" : $"{count} occurrences";
	}
}
