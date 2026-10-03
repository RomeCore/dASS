using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Utils.Files;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Loading;
using LLMDesktopAssistant.Addons.Management;
using Material.Icons;

namespace LLMDesktopAssistant.Tools.Implementations.Filesystem
{
	/// <summary>
	/// Universal file editing tool that applies a list of replacement patches to a text file.
	/// Supports plain text (with flexible whitespace matching) and regex patterns.
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
				Name = "fs-edit",
				Description = """
					Edits a text file by applying a list of replacement patches.

					Each patch replaces ALL occurrences of its `match` with the `replace` text.
					Patches are applied sequentially: each patch sees the result of the previous ones.
					If a patch finds no occurrences, it is skipped and reported in the result вЂ”
					all other patches are still applied.

					Plain text mode (useRegex = false, default):
					- `match` is treated as a literal string (can be multi-line)
					- Leading/trailing whitespace and common indentation are ignored in matching:
					  `match: "public class Foo"` matches `"    public class Foo"` in the file
					- Text before and after the match is preserved

					Regex mode (useRegex = true):
					- `match` is interpreted as a .NET regular expression
					- `replace` supports standard replacement syntax: $1, ${name}, $&, $$, etc.
					- To delete a match, pass an empty `replace` string

					Examples:
					- Replace text (plain):
						fs-edit(path: "file.cs", patches: [{ match: "public class Foo", replace: "public class Bar" }])
					- Regex rename with capture group:
						fs-edit(path: "file.cs", patches: [{ match: "class (\\w+)", replace: "class Renamed_$1", useRegex: true }])
					- Delete all matches (empty replace):
						fs-edit(path: "file.cs", patches: [{ match: "Console\\.WriteLine", replace: "", useRegex: true }])
					- Multiple patches in one call:
						fs-edit(path: "file.cs", patches: [
							{ match: "foo", replace: "bar" },
							{ match: "class (\\w+)", replace: "class C_$1", useRegex: true }
						])
					""",
				NameKey = Locale.GetKey("tool.name.fs-edit"),
				DescriptionKey = Locale.GetKey("tool.description.fs-edit"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.FileEdit | ToolBehaviour.AccessOutsideWorkdir |
					ToolBehaviour.AddonPackEdit | ToolBehaviour.PromptEdit | ToolBehaviour.ScriptEdit,
				DefaultSelfHandledDecisions = ToolPolicyDecision.Approve | ToolPolicyDecision.Ask,
				SynchronizationGroup = FileSystemEditBaseToolModule.SyncGroup
			});
		}

		/// <summary>
		/// A single edit operation that replaces all occurrences of a match with a replacement text.
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
		}

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

		private class FSWriteSharedContext
		{
			public required string Path { get; init; }
			public required AddonKind AddonKind { get; init; }
			public required string NewContent { get; init; }
			public IReadOnlyList<string> PatchErrors { get; init; } = [];
		}

		private StreamingToolArgumentsAnalysisResult EditStreaming(
			string? path)
		{
			path ??= "?";
			return new StreamingToolArgumentsAnalysisResult
			{
				StatusIcon = MaterialIconKind.FileDocumentEdit,
				StatusTitle = $"**{path}**"
			};
		}

		private PreviewToolExecutionResult EditPreview(
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
				return new PreviewToolExecutionResult
				{
					InterruptingSuccess = false,
					InterruptingContent = error,
					StatusIcon = MaterialIconKind.FileAlert,
					StatusTitle = $"**{path}**",
					ExpectedBehaviour = ToolBehaviour.None |
						(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
						AddonKindToolBehaviourConverter.Convert(addonKind)
				};
			}

			var originalContent = File.ReadAllText(fullPath!);
			var normalizedContent = NormalizeLineEndings(originalContent);
			var (newContent, patchErrors) = ApplyPatches(normalizedContent, patches, tabSize, cancellationToken);
			newContent = PreserveLineEndings(originalContent, newContent);

			if (newContent == originalContent)
			{
				sharedCtx = new FSWriteSharedContext
				{
					Path = fullPath!,
					AddonKind = addonKind,
					NewContent = originalContent,
					PatchErrors = patchErrors
				};

				return new PreviewToolExecutionResult
				{
					InterruptingSuccess = true,
					InterruptingContent = patchErrors.Count > 0
						? $"No changes were made to the file.\n{string.Join("\n", patchErrors)}"
						: "No changes were made to the file. The specified matches were not found.",
					StatusIcon = MaterialIconKind.FileQuestion,
					StatusTitle = LocalizationManager.LocalizeStaticFormat("tool.status.fs-edit.changes_none", $"**{path}**"),
					ExpectedBehaviour = ToolBehaviour.None |
						(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
						AddonKindToolBehaviourConverter.Convert(addonKind)
				};
			}

			sharedCtx = new FSWriteSharedContext
			{
				Path = fullPath!,
				AddonKind = addonKind,
				NewContent = newContent,
				PatchErrors = patchErrors
			};

			return new PreviewToolExecutionResult
			{
				StatusIcon = MaterialIconKind.FileDocumentEdit,
				StatusTitle = $"**{path}**",
				ExpectedBehaviour = ToolBehaviour.FileEdit |
					(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
					AddonKindToolBehaviourConverter.Convert(addonKind)
			};
		}

		private async Task Edit(
			[SharedContext] FSWriteSharedContext? sharedCtx,
			ReactiveToolResult result,
			ToolExecutionContext ctx,
			CancellationToken cancellationToken,
			[Description("The path to the file to edit.")]
			string path,
			[Description("The list of patches to apply. Each patch replaces all occurrences of its match with the replacement text.")]
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
					result.StatusIcon = MaterialIconKind.FileAlert;
					result.StatusTitle = $"**{path}**";
					result.ResultContent = error;
					result.CompleteWithError();
					return;
				}

				var originalContent = File.ReadAllText(fullPath!);
				var newContent = sharedCtx?.NewContent;
				var patchErrors = sharedCtx?.PatchErrors ?? [];
				if (newContent == null)
				{
					var normalizedContent = NormalizeLineEndings(originalContent);
					(newContent, patchErrors) = ApplyPatches(normalizedContent, patches, tabSize, cancellationToken);
					newContent = PreserveLineEndings(originalContent, newContent);
				}

				if (newContent == originalContent)
				{
					result.StatusIcon = MaterialIconKind.FileQuestion;
					result.StatusTitle = LocalizationManager.LocalizeStaticFormat("tool.status.fs-edit.changes_none", $"**{path}**");
					result.ResultContent = patchErrors.Count > 0
						? $"No changes were made to the file.\n{string.Join("\n", patchErrors)}"
						: "No changes were made to the file. The specified matches were not found.";
					result.CompleteWithSuccess();
					return;
				}

				var postProcessResult = await PostProcessDiffAsync(fullPath, originalContent, newContent, ctx, result, cancellationToken);
				string userNotesPostfix = string.IsNullOrWhiteSpace(postProcessResult.UserNotes)
					? string.Empty
					:	$"""
							
							User has provided notes:
							{postProcessResult.UserNotes}
							""";
				if (!postProcessResult.AppliedDiff.HasGroups)
				{
					result.StatusIcon = MaterialIconKind.FileDiscard;
					result.StatusTitle = $"**{path}**";
					result.ResultContent = postProcessResult.RejectedDiff.HasGroups ?
						$"""
						User has rejected the changes, none has applied.
						[REJECTED CHANGES BY THE USER, THESE ARE NOT APPLIED]:
						{postProcessResult.RejectedDiff}{userNotesPostfix}
						""" :
						$"User has rejected the changes, none has applied.{userNotesPostfix}";
					result.CompleteWithSuccess();
					return;
				}

				File.WriteAllText(fullPath!, postProcessResult.NewContent);
				_addonInvalidator.Invalidate(addonKind);

				var diff = postProcessResult.AppliedDiff;
				var (removed, added) = diff.GetChangeCounts();

				string notAppliedText = patchErrors.Count > 0
					? $"""
						
						[NOT APPLIED PATCHES]:
						{string.Join("\n", patchErrors)}
						"""
					: string.Empty;

				result.StatusIcon = MaterialIconKind.FileDocumentEdit;
				result.StatusTitle = $"**{path}** *(-{removed} +{added})*";
				result.ResultContent = postProcessResult.RejectedDiff.HasGroups ?
					$"""
					File edited successfully. *(-{removed} +{added})*
					[APPLIED CHANGES]:
					{diff}
					[REJECTED CHANGES BY THE USER, THESE ARE NOT APPLIED]:
					{postProcessResult.RejectedDiff}{userNotesPostfix}{notAppliedText}
					""" :
					$"""
					File edited successfully. *(-{removed} +{added})*
					[APPLIED CHANGES]:
					{diff}{userNotesPostfix}{notAppliedText}
					""";
				result.CompleteWithSuccess();
			}
			catch (OperationCanceledException)
			{
				result.StatusIcon = MaterialIconKind.FileQuestion;
				result.StatusTitle = $"**{path}**";
				result.ResultContent = "Edit operation was cancelled.";
				result.CompleteWithError();
			}
			catch (Exception ex)
			{
				result.StatusIcon = MaterialIconKind.FileAlert;
				result.StatusTitle = $"**{path}**";
				result.ResultContent = $"Error editing file: {ex.Message}";
				result.CompleteWithError();
			}
		}

		private static (string NewContent, List<string> PatchErrors) ApplyPatches(
			string content,
			List<EditPatch> patches,
			int tabSize,
			CancellationToken cancellationToken)
		{
			var patchErrors = new List<string>();
			var current = content;

			for (int i = 0; i < patches.Count; i++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var patch = patches[i];

				string? newContent;
				try
				{
					newContent = patch.useRegex
						? ApplyRegexPatch(current, patch)
						: ApplyPlainPatch(current, patch, tabSize);
				}
				catch (ArgumentException ex)
				{
					patchErrors.Add($"Patch #{i + 1} (match: \"{patch.match}\"): invalid pattern: {ex.Message}");
					continue;
				}

				if (newContent == null)
					patchErrors.Add($"Patch #{i + 1} (match: \"{patch.match}\"): no occurrences found.");
				else
					current = newContent;
			}

			return (current, patchErrors);
		}

		private static string? ApplyPlainPatch(string content, EditPatch patch, int tabSize)
		{
			return FilePatchedReplacer.Replace(content, patch.match, patch.replace, tabSize, patch.ignoreCase);
		}

		private static string? ApplyRegexPatch(string content, EditPatch patch)
		{
			var options = RegexOptions.Compiled
				| (patch.ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None)
				| (patch.isRegexMultiline ? RegexOptions.Multiline : RegexOptions.None);
			var regex = new Regex(patch.match, options);

			return regex.IsMatch(content) ? regex.Replace(content, patch.replace) : null;
		}

		private static string NormalizeLineEndings(string text)
			=> text.Replace("\r\n", "\n").Replace("\r", "\n");

		private static string PreserveLineEndings(string original, string modified)
		{
			if (original.Contains("\r\n") && !modified.Contains("\r\n"))
				return modified.Replace("\n", "\r\n");
			return modified;
		}
	}
}
