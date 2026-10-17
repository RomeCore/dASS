using System.Diagnostics;
using System.IO;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Services.Instances;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using Material.Icons;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Desktop.ToolModules
{
	[ToolModule]
	public class DesktopFilesystemToolModule : ToolModule
	{
		private readonly IWorkingDirectoryAccessService _fileAccess;
		private readonly IExplorerOpener _explorerOpener;

		public DesktopFilesystemToolModule(IWorkingDirectoryAccessService fileAccess, IExplorerOpener explorerOpener)
		{
			_fileAccess = fileAccess;
			_explorerOpener = explorerOpener;

			AddTool(new ToolInitializationInfo
			{
				Executor = OpenFile,
				StreamingAnalyzer = OpenFileStreaming,
				PreviewExecutor = OpenFilePreview,
				Name = "fs-open_file",
				Description = "Opens a file from the working directory with its default application.",
				NameKey = Locale.GetKey("tool.name.fs-open_file"),
				DescriptionKey = Locale.GetKey("tool.description.fs-open_file"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess | ToolBehaviour.FileRead |
					ToolBehaviour.AccessOutsideWorkdir
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = OpenInExplorer,
				StreamingAnalyzer = OpenInExplorerStreaming,
				PreviewExecutor = OpenInExplorerPreview,
				Name = "fs-open_in_explorer",
				Description = "Opens a file or directory in the system file explorer. " +
					"Files are revealed and selected, directories are opened, and non-existent paths " +
					"fall back to opening their parent directory.",
				NameKey = Locale.GetKey("tool.name.fs-open_in_explorer"),
				DescriptionKey = Locale.GetKey("tool.description.fs-open_in_explorer"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess | ToolBehaviour.AccessOutsideWorkdir
			});
		}

		public StreamingToolAnalysisResult OpenFileStreaming(
			IToolExecutionContext ctx,
			string? path)
		{
			path ??= "?";
			ctx.Call.SetStatus(MaterialIconKind.OpenInNew, $"**{path}**");
			return StreamingToolAnalysisResult.Continue;
		}

		public PreviewToolExecutionResult OpenFilePreview(IToolExecutionContext ctx,
			string path, [SharedContext] out string fullPath)
		{
			fullPath = _fileAccess.CheckedAccessPath(path, DirectoryAccessMode.Execute, out var isAccessed);

			ctx.Call.SetStatus(MaterialIconKind.OpenInNew, $"**{path}**");

			if (!File.Exists(fullPath))
			{
				ctx.Call.SetResult($"File not found: {path}");
			}

			return new PreviewToolExecutionResult
			{
				ExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess | ToolBehaviour.FileRead |
					(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : 0)
			};
		}

		public ToolExecutionResult OpenFile(
			[SharedContext] string? fullPath,
			string path,
			IToolExecutionContext ctx)
		{
			try
			{
				var workingDirectory = _fileAccess.GetWorkingDirectory();
				fullPath ??= _fileAccess.AccessPath(path, DirectoryAccessMode.Execute);

				if (!File.Exists(fullPath))
				{
					ctx.Call.SetStatus(MaterialIconKind.OpenInNew, $"**{path}**");
					ctx.Call.SetResult($"File not found: {path}");
					return ToolExecutionResult.Failed;
				}

				using (Process process = new Process())
				{
					process.StartInfo = new ProcessStartInfo
					{
						FileName = fullPath,
						WorkingDirectory = workingDirectory,
						UseShellExecute = true
					};
					process.Start();
				}

				ctx.Call.SetStatus(MaterialIconKind.OpenInNew, $"**{path}**");
				ctx.Call.SetResult($"Successfully opened: {path}");
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatus(MaterialIconKind.OpenInNew, $"**{path}**");
				ctx.Call.SetResult($"Error opening file {path}: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		public StreamingToolAnalysisResult OpenInExplorerStreaming(
			IToolExecutionContext ctx,
			string? path)
		{
			path ??= "?";
			ctx.Call.SetStatus(MaterialIconKind.FolderOpen, $"**{path}**");
			return StreamingToolAnalysisResult.Continue;
		}

		public PreviewToolExecutionResult OpenInExplorerPreview(IToolExecutionContext ctx,
			string path, [SharedContext] out string fullPath)
		{
			fullPath = _fileAccess.CheckedAccessPath(path, DirectoryAccessMode.Execute, out var isAccessed);

			ctx.Call.SetStatus(MaterialIconKind.FolderOpen, $"**{path}**");

			return new PreviewToolExecutionResult
			{
				ExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess |
					(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : 0)
			};
		}

		public ToolExecutionResult OpenInExplorer(
			[SharedContext] string? fullPath,
			string path,
			IToolExecutionContext ctx)
		{
			try
			{
				fullPath ??= _fileAccess.AccessPath(path, DirectoryAccessMode.Execute);

				if (!_explorerOpener.OpenPath(fullPath))
				{
					ctx.Call.SetStatus(MaterialIconKind.FolderOpen, $"**{path}**");
					ctx.Call.SetResult($"Failed to open in explorer: {path}");
					return ToolExecutionResult.Failed;
				}

				ctx.Call.SetStatus(MaterialIconKind.FolderOpen, $"**{path}**");
				ctx.Call.SetResult($"Successfully opened in explorer: {path}");
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatus(MaterialIconKind.FolderOpen, $"**{path}**");
				ctx.Call.SetResult($"Error opening {path} in explorer: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

	}
}
