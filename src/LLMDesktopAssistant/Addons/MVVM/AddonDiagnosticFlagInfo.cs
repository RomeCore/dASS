using Avalonia.Media;

using LLMDesktopAssistant.Localization;
using Material.Icons;

namespace LLMDesktopAssistant.Addons.MVVM;

/// <summary>
/// Represents a single <see cref="AddonDiagnosticCode"/> flag for display in the UI.
/// Contains icon, color, and localized tooltip.
/// </summary>
public class AddonDiagnosticFlagInfo
{
	/// <summary>
	/// The <see cref="AddonDiagnosticCode"/> flag.
	/// </summary>
	public AddonDiagnosticCode Flag { get; }

	/// <summary>
	/// Localized display name for the tooltip.
	/// </summary>
	public string DisplayName { get; }

	/// <summary>
	/// Localized description text for the tooltip.
	/// </summary>
	public string Description { get; }

	/// <summary>
	/// Icon to display for this flag.
	/// </summary>
	public VisualIconKind Icon { get; }

	/// <summary>
	/// Color associated with this flag's severity/category.
	/// </summary>
	public IBrush Color { get; }

	public AddonDiagnosticFlagInfo(AddonDiagnosticCode flag, string displayName, string description,
		VisualIconKind icon, IBrush color)
	{
		Flag = flag;
		DisplayName = displayName;
		Description = description;
		Icon = icon;
		Color = color;
	}

	/// <summary>
	/// Creates a <see cref="AddonDiagnosticFlagInfo"/> for a given <see cref="AddonDiagnosticCode"/> flag.
	/// </summary>
	public static AddonDiagnosticFlagInfo Create(AddonDiagnosticCode flag)
	{
		var key = $"addon.diagnostic.{flag.ToString().ToLower()}";
		var displayName = LocalizationManager.LocalizeStatic(key);
		var description = LocalizationManager.LocalizeStatic($"{key}.hint");

		if (displayName == key || string.IsNullOrEmpty(displayName))
			displayName = flag.ToString();
		if (description == $"{key}.hint" || string.IsNullOrEmpty(description))
			description = string.Empty;

		return new AddonDiagnosticFlagInfo(flag, displayName, description, GetIcon(flag), GetColor(flag));
	}

	/// <summary>
	/// Creates a list of <see cref="AddonDiagnosticFlagInfo"/> for a set of diagnostic flags.
	/// </summary>
	public static ImmutableList<AddonDiagnosticFlagInfo> CreateForFlags(AddonDiagnosticCode codes)
	{
		var result = ImmutableList.CreateBuilder<AddonDiagnosticFlagInfo>();
		foreach (var flag in Enum.GetValues<AddonDiagnosticCode>())
		{
			if (flag is not AddonDiagnosticCode.None && codes.HasFlag(flag))
				result.Add(Create(flag));
		}
		return result.ToImmutableList();
	}

	/// <summary>
	/// Creates a list of <see cref="AddonDiagnosticFlagInfo"/> from an <see cref="AddonDiagnostic"/>.
	/// </summary>
	public static ImmutableList<AddonDiagnosticFlagInfo> CreateFromDiagnostic(AddonDiagnostic? diagnostic)
	{
		if (diagnostic == null || diagnostic.Codes == AddonDiagnosticCode.None)
			return [];

		var flags = CreateForFlags(diagnostic.Codes);

		// Add exception flags if present
		foreach (var exception in diagnostic.Exceptions)
		{
			var exceptionFlag = new AddonDiagnosticFlagInfo(
				AddonDiagnosticCode.GeneralParsingError,
				exception.Message,
				exception.StackTrace ?? string.Empty,
				MaterialIconKind.AlertCircle,
				diagnostic.IsFatal ? Brushes.Red : Brushes.Orange);
			flags = flags.Add(exceptionFlag);
		}

		return flags;
	}

	private static VisualIconKind GetIcon(AddonDiagnosticCode flag) => flag switch
	{
		AddonDiagnosticCode.None => MaterialIconKind.CheckCircle,

		// Structure issues
		AddonDiagnosticCode.MissingFrontmatter => MaterialIconKind.CodeJson,
		AddonDiagnosticCode.MissingName => MaterialIconKind.CardText,
		AddonDiagnosticCode.MissingDescription => MaterialIconKind.CardText,
		AddonDiagnosticCode.MissingFrontmatterName => MaterialIconKind.CardText,
		AddonDiagnosticCode.MissingFrontmatterDescription => MaterialIconKind.CardText,
		AddonDiagnosticCode.MissingFile => MaterialIconKind.FileQuestion,

		// Format issues
		AddonDiagnosticCode.FrontmatterParsingError => MaterialIconKind.CodeBraces,
		AddonDiagnosticCode.FrontmatterDecodingError => MaterialIconKind.CodeBraces,
		AddonDiagnosticCode.NameFormatError => MaterialIconKind.FormatLetterCase,
		AddonDiagnosticCode.NameFSMismatch => MaterialIconKind.FolderAlert,

		// Errors
		AddonDiagnosticCode.FileAccessError => MaterialIconKind.FileLock,
		AddonDiagnosticCode.GeneralParsingError => MaterialIconKind.AlertCircle,

		_ => MaterialIconKind.HelpCircle
	};

	private static IBrush GetColor(AddonDiagnosticCode flag) => flag switch
	{
		// Fatal-level issues (red)
		AddonDiagnosticCode.MissingFile => Brushes.Red,
		AddonDiagnosticCode.FileAccessError => Brushes.Red,
		AddonDiagnosticCode.GeneralParsingError => Brushes.Red,

		// Critical warnings (orange)
		AddonDiagnosticCode.MissingFrontmatter => Brushes.Orange,
		AddonDiagnosticCode.FrontmatterParsingError => Brushes.Orange,
		AddonDiagnosticCode.FrontmatterDecodingError => Brushes.Orange,
		AddonDiagnosticCode.MissingName => Brushes.Orange,

		// Medium warnings (gold)
		AddonDiagnosticCode.NameFormatError => Brushes.Gold,
		AddonDiagnosticCode.NameFSMismatch => Brushes.Gold,
		AddonDiagnosticCode.MissingFrontmatterName => Brushes.Gold,
		AddonDiagnosticCode.MissingDescription => Brushes.Gold,

		// Low warnings (dodger blue)
		AddonDiagnosticCode.MissingFrontmatterDescription => Brushes.DodgerBlue,

		_ => Brushes.Gray
	};
}