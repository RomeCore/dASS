using System.Text;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
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
				Name = "fs-write_file",
				Description = "Writes text content to a file inside working directory.",
				NameKey = Locale.GetKey("tool.name.fs-write_file"),
				DescriptionKey = Locale.GetKey("tool.description.fs-write_file"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.FileEdit | ToolBehaviour.FileDirectoryCreate | ToolBehaviour.AccessOutsideWorkdir |
					ToolBehaviour.AddonPackEdit | ToolBehaviour.PromptEdit | ToolBehaviour.ScriptEdit,
				DefaultSelfHandledDecisions = ToolPolicyDecision.Approve | ToolPolicyDecision.Ask,
				SynchronizationGroup = FileSystemEditBaseToolModule.SyncGroup
			});
		}

		private StreamingToolArgumentsAnalysisResult? WriteFileStream(
			string? path,
			string? content,
			bool append = false)
		{
			path ??= "?";

			int lines = 0;
			if (content != null)
				foreach (var line in content.EnumerateLines())
					lines++;

			return new StreamingToolArgumentsAnalysisResult
			{
				StatusTitle = LocalizationManager.LocalizeStaticFormat("tool.status.fs-write_file.streaming_status",
					path != null ? $"**{path}**" : string.Empty,
					lines)
			};
		}

		private class WriteFileContext
		{
			public required string FullPath { get; init; }
			public required AddonKind AddonKind { get; init; }
		}

		private PreviewToolExecutionResult? WriteFilePreview(
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
				sharedCtx = new WriteFileContext { FullPath = fullPath, AddonKind = addonKind };

				if (fileExisted)
				{
					try
					{
						var oldContent = File.ReadAllText(fullPath);
						if (oldContent == content)
						{
							return new PreviewToolExecutionResult
							{
								StatusIcon = Material.Icons.MaterialIconKind.FileQuestion,
								StatusTitle = LocalizationManager.LocalizeStaticFormat("tool.status.fs-edit.changes_none", $"**{path}**"),
								ExpectedBehaviour = (!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
									AddonKindToolBehaviourConverter.Convert(addonKind),
								InterruptingSuccess = true,
								InterruptingContent = $"File **{path}** already contains the same content."
							};
						}

						return new PreviewToolExecutionResult
						{
							StatusIcon = Material.Icons.MaterialIconKind.FilePlus,
							StatusTitle = $"**{path}**",
							ExpectedBehaviour = ToolBehaviour.FileEdit |
								(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
								AddonKindToolBehaviourConverter.Convert(addonKind)
						};
					}
					catch
					{
						return null;
					}
				}

				return new PreviewToolExecutionResult
				{
					StatusIcon = Material.Icons.MaterialIconKind.FilePlus,
					StatusTitle = $"**{path}**",
					ExpectedBehaviour = ToolBehaviour.FileDirectoryCreate |
						(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
						AddonKindToolBehaviourConverter.Convert(addonKind),
					SelfHandledDecisions = ToolPolicyDecision.None
				};
			}
			catch (Exception ex)
			{
				sharedCtx = null;
				return new PreviewToolExecutionResult
				{
					InterruptingContent = $"Error writing file: {ex.Message}",
					InterruptingSuccess = false
				};
			}
		}

		private async Task WriteFile(
			[SharedContext] WriteFileContext? sharedCtx,
			ReactiveToolResult result,
			ToolExecutionContext ctx,
			CancellationToken cancellationToken,
			string path,
			string content,
			bool append = false)
		{
			try
			{
				var fullPath = sharedCtx?.FullPath ?? _fileAccess.AccessPath(path, DirectoryAccessMode.ReadWrite);
				var fileExisted = File.Exists(fullPath);
				var addonKind = sharedCtx?.AddonKind ?? _addonDetector.Detect(path, isFile: true,
					fileExisted ? FileOperation.Edit : FileOperation.Create,
					fileExisted ? null : FileUtils.GetExistingAncestorDirectory(fullPath));
				var dir = Path.GetDirectoryName(fullPath);

				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir!);

				string oldContent = string.Empty;

				if (fileExisted)
					oldContent = File.ReadAllText(fullPath);
				if (append && fileExisted)
					content = oldContent + content;

				if (fileExisted)
				{
					var postProcessResult = await PostProcessDiffAsync(fullPath, oldContent, content, ctx, result, cancellationToken);
					string userNotesPostfix = string.IsNullOrWhiteSpace(postProcessResult.UserNotes)
						? string.Empty
						: $"""
							User has provided notes:
							{postProcessResult.UserNotes}
							""";
					if (!postProcessResult.AppliedDiff.HasGroups)
					{
						result.StatusIcon = Material.Icons.MaterialIconKind.FileDiscard;
						result.StatusTitle = $"**{path}**";
						result.ResultContent = $"""
							User has rejected the changes, none has applied.
							{userNotesPostfix}
							""";
						result.CompleteWithSuccess();
						return;
					}

					File.WriteAllText(fullPath, postProcessResult.NewContent);
					_addonInvalidator.Invalidate(addonKind);

					var fileInfo = new FileInfo(fullPath);
					var size = FileUtils.BytesToDisplaySize(fileInfo.Length);

					var output = new StringBuilder();
					output.AppendLine($"File: {path}");
					output.AppendLine($"Operation: {(append ? "Append" : "Write")}");
					output.AppendLine($"New size: {fileInfo.Length} bytes ~ ({size})");

					// Compute and show diff for overwritten files
					string changesTitlePostfix = string.Empty;
					var diff = postProcessResult.AppliedDiff;
					if (diff.HasGroups)
					{
						var (removed, added) = diff.GetChangeCounts();
						changesTitlePostfix = $" *(-{removed} +{added})*";

						output.AppendLine($"File edited successfully. *(-{removed} +{added})*");
						output.AppendLine("[CHANGES]:");
						output.AppendLine(diff.ToString());
					}
					if (postProcessResult.RejectedDiff.HasGroups)
					{
						output.AppendLine("[REJECTED CHANGES BY THE USER, THESE ARE NOT APPLIED]:");
						output.AppendLine(postProcessResult.RejectedDiff.ToString());
					}
					if (!postProcessResult.Diff.HasGroups)
					{
						output.AppendLine("No changes has been applied.");
					}
					if (!string.IsNullOrWhiteSpace(postProcessResult.UserNotes))
					{
						output.AppendLine(userNotesPostfix);
					}

					result.StatusIcon = append ? Material.Icons.MaterialIconKind.FileEdit : Material.Icons.MaterialIconKind.FileCheck;
					result.StatusTitle = $"**{path}**{changesTitlePostfix}";
					result.ResultContent = output.ToString();
					result.CompleteWithSuccess();
				}
				else
				{
					File.WriteAllText(fullPath, content);
					_addonInvalidator.Invalidate(addonKind);

					var fileInfo = new FileInfo(fullPath);
					var size = FileUtils.BytesToDisplaySize(fileInfo.Length);

					var output = new StringBuilder();
					output.AppendLine($"File: {path}");
					output.AppendLine($"Operation: Write");
					output.AppendLine($"Size: {fileInfo.Length} bytes ~ ({size})");

					result.StatusIcon = Material.Icons.MaterialIconKind.FilePlus;
					result.StatusTitle = $"**{path}**";
					result.ResultContent = output.ToString();
					result.CompleteWithSuccess();
				}

			}
			catch (Exception ex)
			{
				result.StatusIcon = Material.Icons.MaterialIconKind.FileAlert;
				result.StatusTitle = $"**{path}**";
				result.ResultContent = $"Error writing file: {ex.Message}";
				result.CompleteWithError();
			}
		}
	}
}
