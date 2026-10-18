using System.Text;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Tools.MVVM.Diff;
using LLMDesktopAssistant.Utils.Files;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Loading;
using LLMDesktopAssistant.Addons.Management;

namespace LLMDesktopAssistant.Tools.Implementations.Filesystem
{
	/// <summary>
	/// Tool module providing file write operations with integrated diff output.
	/// When overwriting an existing file, it computes and shows a line-based diff
	/// of the changes using a pure C# LCS-based algorithm (no git dependency).
	/// </summary>
	[ToolModule]
	public class FilesystemWriteToolModule : FileSystemEditBaseToolModule
	{
		private readonly IWorkingDirectoryAccessService _fileAccess;
		private readonly IAddonPathImpactDetector _addonDetector;
		private readonly IAddonManagerInvalidator _addonInvalidator;

		public FilesystemWriteToolModule(IWorkingDirectoryAccessService fileAccess,
			IAddonPathImpactDetector addonDetector, IAddonManagerInvalidator addonInvalidator)
		{
			_fileAccess = fileAccess;
			_addonDetector = addonDetector;
			_addonInvalidator = addonInvalidator;

			AddTool(new ToolInitializationInfo
			{
				Executor = WriteFile,
				StreamingAnalyzer = WriteFileStream,
				PreviewExecutor = WriteFilePreview,
				Approval = ApproveWriteFile,
				Name = "fs-write_file",
				Description = "Writes text content to a file inside working directory.",
				NameKey = Locale.GetKey("tool.name.fs-write_file"),
				DescriptionKey = Locale.GetKey("tool.description.fs-write_file"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.FileEdit | ToolBehaviour.FileDirectoryCreate | ToolBehaviour.AccessOutsideWorkdir |
					ToolBehaviour.AddonPackEdit | ToolBehaviour.PromptEdit | ToolBehaviour.ScriptEdit,
				HandledDecisions = ToolPolicyDecision.Approve | ToolPolicyDecision.Ask,
				SyncAnalyzer = FilesystemSynchronizationAnalyzer.AnalyzeSyncKeys
			});
		}

		private StreamingToolAnalysisResult WriteFileStream(
			IToolExecutionContext ctx,
			string? path,
			string? content,
			bool append = false)
		{
			path ??= "?";

			int lines = 0;
			if (content != null)
				foreach (var line in content.EnumerateLines())
					lines++;

			ctx.Call.SetStatusTitle(LocalizationManager.LocalizeStaticFormat("tool.status.fs-write_file.streaming_status",
				path != null ? $"**{path}**" : string.Empty,
				lines));

			return StreamingToolAnalysisResult.Continue;
		}

		private sealed class WriteFileContext : DiffSharedContext
		{
		}

		private PreviewToolExecutionResult? WriteFilePreview(
			IToolExecutionContext ctx,
			[SharedContext] out WriteFileContext? sharedCtx,
			string path,
			string content,
			bool append = false)
		{
			try
			{
				var fullPath = _fileAccess.CheckedAccessPath(path, DirectoryAccessMode.ReadWrite, out var isAccessed);
				var fileExisted = File.Exists(fullPath);
				var addonKind = _addonDetector.Detect(path, isFile: true,
					fileExisted ? FileOperation.Edit : FileOperation.Create,
					fileExisted ? null : FileUtils.GetExistingAncestorDirectory(fullPath));

				string oldContent;
				try
				{
					oldContent = fileExisted ? File.ReadAllText(fullPath) : string.Empty;
				}
				catch
				{
					sharedCtx = null;
					return null;
				}

				var newContent = append && fileExisted ? oldContent + content : content;

				if (fileExisted && oldContent == newContent)
				{
					sharedCtx = CreateShared(fullPath, addonKind, oldContent, oldContent, EmptyDiff(), null);
					ctx.Call.SetStatus(Material.Icons.MaterialIconKind.FileQuestion,
						LocalizationManager.LocalizeStaticFormat("tool.status.fs-edit.changes_none", $"**{path}**"));
					ctx.Call.SetResult($"File **{path}** already contains the same content.");

					return new PreviewToolExecutionResult
					{
						ExpectedBehaviour = (!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
							AddonKindToolBehaviourConverter.Convert(addonKind),
						InterruptingSuccess = true
					};
				}

				var (diffView, fullDiff) = PrepareDiff(ctx, path, oldContent, newContent);
				sharedCtx = CreateShared(fullPath, addonKind, oldContent, newContent, fullDiff, diffView);

				ctx.Call.SetStatus(Material.Icons.MaterialIconKind.FilePlus, $"**{path}**");

				return new PreviewToolExecutionResult
				{
					ExpectedBehaviour = (fileExisted ? ToolBehaviour.FileEdit : ToolBehaviour.FileDirectoryCreate) |
						(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
						AddonKindToolBehaviourConverter.Convert(addonKind),
					HandledDecisions = ctx.RunningInUi ? null : ToolPolicyDecision.None
				};
			}
			catch (Exception ex)
			{
				sharedCtx = null;
				ctx.Call.SetResult($"Error writing file: {ex.Message}");

				return new PreviewToolExecutionResult
				{
					InterruptingSuccess = false
				};
			}
		}

		private static WriteFileContext CreateShared(string path, AddonKind addonKind, string originalContent,
			string previewedContent, HunkGroups fullDiff, TextDiffAdditionalViewModel? diffView) => new()
		{
			Path = path,
			AddonKind = addonKind,
			OriginalContent = originalContent,
			PreviewedContent = previewedContent,
			FullDiff = fullDiff,
			DiffView = diffView
		};

		private static Task<ToolApprovalResult> ApproveWriteFile(
			IToolExecutionContext ctx,
			[SharedContext] WriteFileContext? sharedCtx,
			CancellationToken cancellationToken) =>
			AwaitDiffConsentAsync(ctx, sharedCtx, cancellationToken);

		private async Task<ToolExecutionResult> WriteFile(
			[SharedContext] WriteFileContext? sharedCtx,
			IToolExecutionContext ctx,
			CancellationToken cancellationToken,
			string path,
			string content,
			bool append = false)
		{
			try
			{
				var fullPath = sharedCtx?.Path ?? _fileAccess.AccessPath(path, DirectoryAccessMode.ReadWrite);
				var fileExisted = File.Exists(fullPath);
				var addonKind = sharedCtx?.AddonKind ?? _addonDetector.Detect(path, isFile: true,
					fileExisted ? FileOperation.Edit : FileOperation.Create,
					fileExisted ? null : FileUtils.GetExistingAncestorDirectory(fullPath));
				var dir = Path.GetDirectoryName(fullPath);

				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir!);

				var oldContent = sharedCtx?.OriginalContent ??
					(fileExisted ? File.ReadAllText(fullPath) : string.Empty);
				var newContent = sharedCtx?.PreviewedContent ??
					(append && fileExisted ? oldContent + content : content);

				if (fileExisted && oldContent == newContent)
				{
					ctx.Call.SetStatus(Material.Icons.MaterialIconKind.FileQuestion,
						LocalizationManager.LocalizeStaticFormat("tool.status.fs-edit.changes_none", $"**{path}**"));
					ctx.Call.SetResult($"File **{path}** already contains the same content.");
					return ToolExecutionResult.Succeeded;
				}

				var result = sharedCtx is null
					? BuildUnpreviewedResult(oldContent, newContent)
					: BuildDiffResult(sharedCtx);

				if (sharedCtx?.Approved == false ||
					(sharedCtx?.Approved == true && !result.AppliedDiff.HasGroups))
				{
					ctx.Call.SetStatus(Material.Icons.MaterialIconKind.FileDiscard, $"**{path}**");
					ctx.Call.SetResult("User has rejected the changes, none has applied.");
					return ToolExecutionResult.Succeeded;
				}

				await File.WriteAllTextAsync(fullPath, result.NewContent, cancellationToken);
				_addonInvalidator.Invalidate(addonKind);

				var fileInfo = new FileInfo(fullPath);
				var size = FileUtils.BytesToDisplaySize(fileInfo.Length);

				if (!fileExisted)
				{
					var created = new StringBuilder();
					created.AppendLine($"File: {path}");
					created.AppendLine($"Operation: Write");
					created.AppendLine($"Size: {fileInfo.Length} bytes ~ ({size})");

					ctx.Call.SetStatus(Material.Icons.MaterialIconKind.FilePlus, $"**{path}**");
					ctx.Call.SetResult(created.ToString());
					return ToolExecutionResult.Succeeded;
				}

				var output = new StringBuilder();
				output.AppendLine($"File: {path}");
				output.AppendLine($"Operation: {(append ? "Append" : "Write")}");
				output.AppendLine($"New size: {fileInfo.Length} bytes ~ ({size})");

				string changesTitlePostfix = string.Empty;
				var diff = result.AppliedDiff;
				if (diff.HasGroups)
				{
					var (removed, added) = diff.GetChangeCounts();
					changesTitlePostfix = $" *(-{removed} +{added})*";

					output.AppendLine($"File edited successfully. *(-{removed} +{added})*");
					output.AppendLine("[CHANGES]:");
					output.AppendLine(diff.ToString());
				}
				if (result.RejectedDiff.HasGroups)
				{
					output.AppendLine("[REJECTED CHANGES BY THE USER, THESE ARE NOT APPLIED]:");
					output.AppendLine(result.RejectedDiff.ToString());
				}
				if (!result.Diff.HasGroups)
				{
					output.AppendLine("No changes has been applied.");
				}

				ctx.Call.SetStatus(
					append ? Material.Icons.MaterialIconKind.FileEdit : Material.Icons.MaterialIconKind.FileCheck,
					$"**{path}**{changesTitlePostfix}");
				ctx.Call.SetResult(output.ToString());
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatus(Material.Icons.MaterialIconKind.FileAlert, $"**{path}**");
				ctx.Call.SetResult($"Error writing file: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}
	}
}
