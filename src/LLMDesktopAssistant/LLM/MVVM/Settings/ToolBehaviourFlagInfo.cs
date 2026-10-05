using Avalonia.Media;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools;
using Material.Icons;

namespace LLMDesktopAssistant.LLM.MVVM.Settings;

/// <summary>
/// Represents a single ToolBehaviour flag for display in the UI.
/// Contains icon, color, and localized tooltip.
/// </summary>
public class ToolBehaviourFlagInfo(ToolBehaviour flag,
	LocaleKeyBase displayName, LocaleKeyBase description,
	VisualIconKind icon, IBrush color)
{
	/// <summary>
	/// The ToolBehaviour flag.
	/// </summary>
	public ToolBehaviour Flag { get; } = flag;

	/// <summary>
	/// Localized display name for the tooltip.
	/// </summary>
	public LocaleKeyBase DisplayName { get; } = displayName;

	/// <summary>
	/// Localized description text for the tooltip.
	/// </summary>
	public LocaleKeyBase Description { get; } = description;

	/// <summary>
	/// Icon to display for this flag.
	/// </summary>
	public VisualIconKind Icon { get; } = icon;

	/// <summary>
	/// Color associated with this flag's severity/category.
	/// </summary>
	public IBrush Color { get; } = color;

	/// <summary>
	/// Creates a <see cref="ToolBehaviourFlagInfo"/> for a given ToolBehaviour flag.
	/// </summary>
	public static ToolBehaviourFlagInfo Create(ToolBehaviour flag)
	{
		var key = $"tool.behaviour.{flag.ToString().ToLower()}";
		var displayName = Locale.GetKey(key);
		var description = Locale.GetKey($"{key}.hint");

		return new ToolBehaviourFlagInfo(flag, displayName, description, GetIcon(flag), GetColor(flag));
	}

	public static ImmutableList<ToolBehaviourFlagInfo> CreateForFlags(ToolBehaviour flags)
	{
		var result = ImmutableList.CreateBuilder<ToolBehaviourFlagInfo>();
		foreach (var flag in ToolBehaviours.AllValues)
		{
			if (flags.HasFlag(flag))
			{
				result.Add(Create(flag));
			}
		}
		if (result.Count == 0 && flags is ToolBehaviour.None)
		{
			result.Add(Create(ToolBehaviour.None));
		}
		return result.ToImmutableList();
	}

	/// <summary>
	/// Determines the icon for a behaviour flag.
	/// </summary>
	private static VisualIconKind GetIcon(ToolBehaviour flag) => flag switch
	{
		ToolBehaviour.None => MaterialIconKind.ShieldCheck,
		ToolBehaviour.FileDirectoryCreate => MaterialIconKind.FolderPlus,
		ToolBehaviour.FileRead => MaterialIconKind.FileDocument,
		ToolBehaviour.FileEdit => MaterialIconKind.FileEdit,
		ToolBehaviour.FileDelete => MaterialIconKind.FileRemove,
		ToolBehaviour.DirectoryRead => MaterialIconKind.FolderOpen,
		ToolBehaviour.DirectoryEdit => MaterialIconKind.FolderEdit,
		ToolBehaviour.DirectoryDelete => MaterialIconKind.FolderRemove,
		ToolBehaviour.SemanticMemoryRead => MaterialIconKind.DatabaseEye,
		ToolBehaviour.SemanticMemoryWrite => MaterialIconKind.DatabaseAdd,
		ToolBehaviour.SemanticMemoryDelete => MaterialIconKind.DatabaseRemove,
		ToolBehaviour.SemanticMemoryClear => MaterialIconKind.DatabaseRemove,
		ToolBehaviour.DatabaseRead => MaterialIconKind.DatabaseEye,
		ToolBehaviour.DatabaseChange => MaterialIconKind.DatabaseEdit,
		ToolBehaviour.DatabaseCustomConnect => MaterialIconKind.DatabaseCog,
		ToolBehaviour.ReadSecrets => MaterialIconKind.Key,
		ToolBehaviour.AccessOutsideWorkdir => MaterialIconKind.ExitRun,
		ToolBehaviour.WorkdirChange => MaterialIconKind.FolderArrowRight,
		ToolBehaviour.ClipboardWrite => MaterialIconKind.ClipboardPlus,
		ToolBehaviour.ClipboardRead => MaterialIconKind.ClipboardText,
		ToolBehaviour.InternetAccess => MaterialIconKind.Web,
		ToolBehaviour.LongRunningTask => MaterialIconKind.TimerSand,
		ToolBehaviour.ExecuteExternalProcess => MaterialIconKind.Console,
		ToolBehaviour.PossiblyUnexpected => MaterialIconKind.AlertCircle,
		ToolBehaviour.RunTerminal => MaterialIconKind.Terminal,
		ToolBehaviour.UserInteraction => MaterialIconKind.Account,
		ToolBehaviour.AgentExecution => MaterialIconKind.Robot,
		ToolBehaviour.AddonPackEdit => MaterialIconKind.Package,
		ToolBehaviour.PromptEdit => MaterialIconKind.Cards,
		ToolBehaviour.ScriptEdit => MaterialIconKind.Script,
		ToolBehaviour.MCP => MaterialIconKind.Server,
		ToolBehaviour.Meta => MaterialIconKind.AutoFix,
		ToolBehaviour.AdHoc => MaterialIconKind.LightbulbOn,

		_ => MaterialIconKind.HelpCircle
	};

	/// <summary>
	/// Determines the color for a behaviour flag based on its severity.
	/// </summary>
	private static IBrush GetColor(ToolBehaviour flag) => flag switch
	{
		// Highly dangerous (dark red)
		ToolBehaviour.DirectoryDelete => Brushes.DarkRed,
		ToolBehaviour.ReadSecrets => Brushes.DarkRed,
		ToolBehaviour.ExecuteExternalProcess => Brushes.DarkRed,
		ToolBehaviour.SemanticMemoryClear => Brushes.DarkRed,
		ToolBehaviour.AddonPackEdit => Brushes.DarkRed,
		ToolBehaviour.ScriptEdit => Brushes.DarkRed,

		// Dangerous (red)
		ToolBehaviour.FileDelete => Brushes.Red,
		ToolBehaviour.PossiblyUnexpected => Brushes.Red,
		ToolBehaviour.DatabaseCustomConnect => Brushes.Red,
		ToolBehaviour.SemanticMemoryDelete => Brushes.Red,
		ToolBehaviour.AgentExecution => Brushes.Red,
		ToolBehaviour.PromptEdit => Brushes.DarkRed,

		// Warning (yellow/orange)
		ToolBehaviour.FileEdit => Brushes.Orange,
		ToolBehaviour.DirectoryEdit => Brushes.Orange,
		ToolBehaviour.DatabaseChange => Brushes.Orange,
		ToolBehaviour.InternetAccess => Brushes.Orange,
		ToolBehaviour.RunTerminal => Brushes.Orange,
		ToolBehaviour.WorkdirChange => Brushes.Orange,
		ToolBehaviour.AccessOutsideWorkdir => Brushes.Orange,

		// Info (blue/cyan)
		ToolBehaviour.FileRead => Brushes.DodgerBlue,
		ToolBehaviour.DirectoryRead => Brushes.DodgerBlue,
		ToolBehaviour.FileDirectoryCreate => Brushes.DodgerBlue,
		ToolBehaviour.DatabaseRead => Brushes.DodgerBlue,
		ToolBehaviour.SemanticMemoryRead => Brushes.DodgerBlue,
		ToolBehaviour.SemanticMemoryWrite => Brushes.DodgerBlue,
		ToolBehaviour.ClipboardRead => Brushes.DodgerBlue,
		ToolBehaviour.ClipboardWrite => Brushes.DodgerBlue,
		ToolBehaviour.LongRunningTask => Brushes.DodgerBlue,
		ToolBehaviour.UserInteraction => Brushes.DodgerBlue,

		// External (purple)
		ToolBehaviour.MCP => Brushes.MediumPurple,
		ToolBehaviour.Meta => Brushes.MediumPurple,
		ToolBehaviour.AdHoc => Brushes.MediumPurple,

		// Safe (green)
		ToolBehaviour.None => Brushes.LimeGreen,

		_ => Brushes.Gray
	};
}
