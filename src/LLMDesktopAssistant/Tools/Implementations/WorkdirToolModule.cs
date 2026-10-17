using System.Text;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Utils;
using Material.Icons;
using LLMDesktopAssistant.Localization;
using DomainPreviewResult = LLMDesktopAssistant.Tools.Execution.Domain.PreviewToolExecutionResult;

namespace LLMDesktopAssistant.Tools.Implementations
{
	[ToolModule(chatScoped: true)]
	public class WorkdirToolModule : ToolModule
	{
		private readonly IChatSettingsService _chatSettings;

		public WorkdirToolModule(IChatSettingsService chatSettings)
		{
			_chatSettings = chatSettings;

			AddTool(new ToolInitializationInfo
			{
				Executor = ListWorkingDirectories,
				Name = "wd-list",
				Description = "Lists all working directories configured for the current chat session.",
				NameKey = Locale.GetKey("tool.name.wd-list"),
				DescriptionKey = Locale.GetKey("tool.description.wd-list"),
				CategoryKey = Locale.GetKey("tool.category.workdir")
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = SwitchWorkingDirectory,
				PreviewExecutor = SwitchWorkingDirectoryPreview,
				Name = "wd-switch",
				Description = "Switches the working directory for the current chat session.",
				NameKey = Locale.GetKey("tool.name.wd-switch"),
				DescriptionKey = Locale.GetKey("tool.description.wd-switch"),
				CategoryKey = Locale.GetKey("tool.category.workdir"),
				DefaultExpectedBehaviour = ToolBehaviour.WorkdirChange,
				StaticSyncKeys = [new(ToolSyncKeyKind.Custom, "wd-switch")] // Prevent parallel execution of this tool
			});
		}

		private ToolExecutionResult ListWorkingDirectories(IToolExecutionContext ctx)
		{
			var sb = new StringBuilder();

			var workingDirectories = _chatSettings.Settings.Environment.GetEffectiveWorkingDirectories();

			if (workingDirectories.IsDefaultWorkingDirectoryEnabled)
				sb.AppendLine($"- *{Directories.DefaultWorkingDirectoryName}*: {Directories.DefaultWorkingDirectory}{(workingDirectories.IsDefaultWorkingDirectoryActive ? " **(ACTIVE)**" : "")}");

			foreach (var wd in workingDirectories.Items)
				if (wd.IsEnabled)
					sb.AppendLine($"- *{wd.Name ?? "null"}*: {wd.Path}{(wd.IsActive ? " **(ACTIVE)**" : "")}");

			if (sb.Length == 0)
				sb.AppendLine("No working directories configured.");

			sb.AppendLine().AppendLine("Note: you can directly access files without typing the entire working directory path if it is active.  ");
			sb.Append("Example: `fs-read_entry` with path='.' will read all contents from the active working directory.");

			ctx.Call.SetResult(sb.ToString());
			ctx.Call.SetStatusIcon(MaterialIconKind.FolderStar);
			ctx.Call.UseMarkdown = true;
			return ToolExecutionResult.Succeeded;
		}

		private DomainPreviewResult SwitchWorkingDirectoryPreview(
			string name,
			IToolExecutionContext ctx)
		{
			var workingDirectories = _chatSettings.Settings.Environment.GetEffectiveWorkingDirectories();
			if (name == Directories.DefaultWorkingDirectoryName && workingDirectories.IsDefaultWorkingDirectoryEnabled)
			{
				ctx.Call.SetStatus(MaterialIconKind.FolderArrowRight, $"*{name}*");
				return DomainPreviewResult.Continue;
			}

			if (!workingDirectories.Items.Any(wd => wd.Name == name && wd.IsEnabled))
			{
				ctx.Call.SetStatus(MaterialIconKind.FolderAlert, $"*{name}*");
				ctx.Call.SetResult($"Working directory *{name}* not found or it's disabled.");
				ctx.Call.UseMarkdown = true;
				return new DomainPreviewResult { InterruptingSuccess = false };
			}

			ctx.Call.SetStatus(MaterialIconKind.FolderArrowRight, $"*{name}*");
			return DomainPreviewResult.Continue;
		}

		private ToolExecutionResult SwitchWorkingDirectory(
			string name,
			IToolExecutionContext ctx)
		{
			var workingDirectories = _chatSettings.Settings.Environment.GetEffectiveWorkingDirectories();
			if (name == Directories.DefaultWorkingDirectoryName && workingDirectories.IsDefaultWorkingDirectoryEnabled)
			{
				workingDirectories.IsDefaultWorkingDirectoryActive = true;
				foreach (var wd in workingDirectories.Items)
					wd.IsActive = false;

				ctx.Call.SetResult($"Working directory *{name}* not found or it's disabled.");
				ctx.Call.SetStatus(MaterialIconKind.FolderAlert, $"*{name}*");
				ctx.Call.UseMarkdown = true;
				return ToolExecutionResult.Succeeded;
			}

			if (!workingDirectories.Items.Any(wd => wd.Name == name && wd.IsEnabled))
			{
				ctx.Call.SetResult($"Working directory *{name}* not found or it's disabled.");
				ctx.Call.SetStatus(MaterialIconKind.FolderAlert, $"*{name}*");
				ctx.Call.UseMarkdown = true;
				return ToolExecutionResult.Failed;
			}

			// Prevent to activate multiple working directories with the same name.
			bool onceFlag = true;
			string? path = null;
			foreach (var wd in workingDirectories.Items)
			{
				wd.IsActive = wd.Name == name && onceFlag;
				if (wd.IsActive)
				{
					onceFlag = false;
					path = wd.Path;
				}
			}

			ctx.Call.SetResult($"Working directory *{name}* ({path}) has been activated.");
			ctx.Call.SetStatus(MaterialIconKind.FolderArrowRight, $"*{name}*");
			ctx.Call.UseMarkdown = true;
			return ToolExecutionResult.Succeeded;
		}
	}
}
