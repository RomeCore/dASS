using Avalonia.Media;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting;
using Material.Icons;

namespace LLMDesktopAssistant.LLM.MVVM.Settings;

/// <summary>
/// Represents a single <see cref="PromptPartDiagnosticCode"/> flag for display in the UI.
/// Contains icon, color, and localized tooltip.
/// </summary>
public class PromptPartDiagnosticFlagInfo
{
	/// <summary>
	/// The <see cref="PromptPartDiagnosticCode"/> flag.
	/// </summary>
	public PromptPartDiagnosticCode Flag { get; }

	/// <summary>
	/// Localized display name key for the tooltip.
	/// </summary>
	public LocaleKeyBase DisplayNameKey { get; }

	/// <summary>
	/// Localized description text key for the tooltip.
	/// </summary>
	public LocaleKeyBase DescriptionKey { get; }

	/// <summary>
	/// Icon to display for this flag.
	/// </summary>
	public VisualIconKind Icon { get; }

	/// <summary>
	/// Color associated with this flag's severity/category.
	/// </summary>
	public IBrush Color { get; }

	public PromptPartDiagnosticFlagInfo(PromptPartDiagnosticCode flag, LocaleKeyBase displayNameKey, LocaleKeyBase descriptionKey,
		VisualIconKind icon, IBrush color)
	{
		Flag = flag;
		DisplayNameKey = displayNameKey;
		DescriptionKey = descriptionKey;
		Icon = icon;
		Color = color;
	}

	/// <summary>
	/// Creates a <see cref="PromptPartDiagnosticFlagInfo"/> for a given <see cref="PromptPartDiagnosticCode"/> flag.
	/// </summary>
	public static PromptPartDiagnosticFlagInfo Create(PromptPartDiagnosticCode flag)
	{
		var key = $"prompt.diagnostic.{flag.ToString().ToLower()}";
		return new PromptPartDiagnosticFlagInfo(flag,
			Locale.GetKey(key),
			Locale.GetKey($"{key}.hint"),
			GetIcon(flag), GetColor(flag));
	}

	/// <summary>
	/// Creates a list of <see cref="PromptPartDiagnosticFlagInfo"/> for a set of diagnostic flags.
	/// </summary>
	public static ImmutableList<PromptPartDiagnosticFlagInfo> CreateForFlags(PromptPartDiagnosticCode codes)
	{
		var result = ImmutableList.CreateBuilder<PromptPartDiagnosticFlagInfo>();
		foreach (var flag in Enum.GetValues<PromptPartDiagnosticCode>())
		{
			if (flag is not PromptPartDiagnosticCode.None && codes.HasFlag(flag))
				result.Add(Create(flag));
		}
		return result.ToImmutableList();
	}

	/// <summary>
	/// Creates a list of <see cref="PromptPartDiagnosticFlagInfo"/> from a <see cref="PromptPartDiagnostic"/>.
	/// </summary>
	public static ImmutableList<PromptPartDiagnosticFlagInfo> CreateFromDiagnostic(PromptPartDiagnostic? diagnostic)
	{
		if (diagnostic == null || diagnostic.Code == PromptPartDiagnosticCode.None)
			return [];

		var flags = CreateForFlags(diagnostic.Code);

		// Add exception flag if present
		if (diagnostic.Exception != null)
		{
			var exceptionFlag = new PromptPartDiagnosticFlagInfo(
				PromptPartDiagnosticCode.None,
				Locale.GetConstKey(diagnostic.Exception.Message),
				Locale.GetConstKey(diagnostic.Exception.StackTrace ?? string.Empty),
				MaterialIconKind.AlertCircle,
				diagnostic.IsFatal ? Brushes.Red : Brushes.Orange);
			flags = flags.Add(exceptionFlag);
		}

		return flags;
	}

	private static VisualIconKind GetIcon(PromptPartDiagnosticCode flag) => flag switch
	{
		PromptPartDiagnosticCode.None => MaterialIconKind.CheckCircle,

		// Structure issues
		PromptPartDiagnosticCode.MissingTemplateIdentifier => MaterialIconKind.FileQuestion,
		PromptPartDiagnosticCode.MissingGuid => MaterialIconKind.CardText,
		PromptPartDiagnosticCode.InvalidGuid => MaterialIconKind.CardText,
		PromptPartDiagnosticCode.MissingStrId => MaterialIconKind.CardText,
		PromptPartDiagnosticCode.MissingName => MaterialIconKind.CardText,
		PromptPartDiagnosticCode.MissingDescription => MaterialIconKind.CardText,
		PromptPartDiagnosticCode.MissingCategory => MaterialIconKind.TagOutline,
		PromptPartDiagnosticCode.MissingLanguage => MaterialIconKind.Language,

		// Format issues
		PromptPartDiagnosticCode.InvalidParameterSchema => MaterialIconKind.CodeBraces,
		PromptPartDiagnosticCode.InvalidSlotKind => MaterialIconKind.Layers,

		_ => MaterialIconKind.HelpCircle
	};

	private static IBrush GetColor(PromptPartDiagnosticCode flag) => flag switch
	{
		// Fatal-level issues (red)
		PromptPartDiagnosticCode.MissingGuid => Brushes.Red,
		PromptPartDiagnosticCode.InvalidGuid => Brushes.Red,
		PromptPartDiagnosticCode.InvalidSlotKind => Brushes.Red,

		// Critical warnings (orange)
		PromptPartDiagnosticCode.MissingTemplateIdentifier => Brushes.Orange,
		PromptPartDiagnosticCode.InvalidParameterSchema => Brushes.Orange,

		// Medium warnings (gold)
		PromptPartDiagnosticCode.MissingStrId => Brushes.Gold,
		PromptPartDiagnosticCode.MissingName => Brushes.Gold,
		PromptPartDiagnosticCode.MissingDescription => Brushes.Gold,

		// Low warnings (dodger blue)
		PromptPartDiagnosticCode.MissingLanguage => Brushes.DodgerBlue,
		PromptPartDiagnosticCode.MissingCategory => Brushes.DodgerBlue,

		_ => Brushes.Gray
	};
}
