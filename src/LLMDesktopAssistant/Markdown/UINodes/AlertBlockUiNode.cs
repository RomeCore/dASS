using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LiveMarkdown.Avalonia;
using Markdig.Extensions.Alerts;
using Material.Icons;

namespace LLMDesktopAssistant.Markdown.UINodes;

/// <summary>
/// Renders a GitHub-style alert block: a rounded border with a type-specific accent
/// color, an icon, a title and the alert body.
/// Supports the extended <see cref="RichAlertInlineParser"/> syntax where
/// <see cref="AlertBlock.Kind"/> may contain:
/// <code>
/// NOTE | SUCCESS                       — plain kind
/// SUCCESS:Done                         — kind + custom title
/// Important message:#FF98D8            — custom title + color
/// WARNING:Be careful:#FFA500           — kind + custom title + color
/// </code>
/// </summary>
public sealed class AlertBlockUiNode : ContainerBlockNode<AlertBlock>
{
	private const string AlertClass = "AlertBlock";
	private const string RootClass = "AlertBlockRoot";
	private const string HeaderClass = "AlertBlockHeader";
	private const string TitleClass = "AlertBlockTitle";
	private const string BodyClass = "AlertBlockBody";
	private const string NoteKind = "NOTE";

	private static readonly HashSet<string> KnownKinds = new(StringComparer.OrdinalIgnoreCase)
	{
		"NOTE", "TIP", "IMPORTANT", "WARNING", "CAUTION", "SUCCESS", "ERROR"
	};

	private readonly Border _border;
	private readonly VisualIcon _icon;
	private readonly TextBlock _titleText;
	private string? _currentTypeClass;

	/// <summary>
	/// Gets the border control rendered for the alert.
	/// </summary>
	public override Control Control => _border;

	/// <summary>
	/// Initializes a new alert block node.
	/// </summary>
	public AlertBlockUiNode()
	{
		_icon = new VisualIcon
		{
			Width = 18,
			Height = 18,
			VerticalAlignment = VerticalAlignment.Center
		};

		_titleText = new TextBlock
		{
			Classes = { TitleClass },
			VerticalAlignment = VerticalAlignment.Center
		};

		var header = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 6,
			Classes = { HeaderClass }
		};
		header.Children.Add(_icon);
		header.Children.Add(_titleText);

		container.Classes.Add(BodyClass);

		var layout = new StackPanel
		{
			Orientation = Orientation.Vertical,
			Classes = { RootClass }
		};
		layout.Children.Add(header);
		layout.Children.Add(container);

		_border = new Border
		{
			Classes = { AlertClass },
			Child = layout
		};
	}

	/// <inheritdoc/>
	protected override bool UpdateCore(
		DocumentNode documentNode,
		AlertBlock alertBlock,
		in ObservableStringBuilderChangedEventArgs change,
		CancellationToken cancellationToken)
	{
		if (!base.UpdateCore(documentNode, alertBlock, change, cancellationToken))
			return false;

		ApplyAlertContent(alertBlock.Kind.ToString());
		return true;
	}

	private void ApplyAlertContent(string rawKind)
	{
		ParseKind(rawKind, out var type, out var title, out var color);

		// Reset everything first so styles (localization, brushes) can apply again.
		if (_currentTypeClass is not null)
		{
			_border.Classes.Remove(_currentTypeClass);
			_currentTypeClass = null;
		}
		_border.ClearValue(Border.BackgroundProperty);
		_border.ClearValue(Border.BorderBrushProperty);
		_icon.ClearValue(VisualIcon.ForegroundProperty);
		_titleText.ClearValue(TextBlock.ForegroundProperty);
		_titleText.ClearValue(TextBlock.TextProperty);

		// Use the default "note" look for unknown/custom content.
		var kind = type ?? NoteKind;
		var typeClass = GetTypeClass(kind);
		_currentTypeClass = typeClass;
		_border.Classes.Add(typeClass);

		_icon.Kind = GetIcon(kind);

		if (!string.IsNullOrEmpty(title))
			_titleText.Text = title;

		if (color is { } c)
		{
			_border.Background = new SolidColorBrush(Color.FromArgb(0x1A, c.R, c.G, c.B));
			_border.BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, c.R, c.G, c.B));
			var brush = new SolidColorBrush(c);
			_icon.Foreground = brush;
			_titleText.Foreground = brush;
		}
	}

	/// <summary>
	/// Parses the raw kind content into (type, customTitle, color).
	/// Color syntax: trailing ":##RRGGBB" / ":##RGB" / ":##RRGGBBAA".
	/// Title syntax: optional "TYPE:" prefix, otherwise the whole content is the title.
	/// </summary>
	private static void ParseKind(string raw, out string? type, out string? title, out Color? color)
	{
		type = null;
		title = null;
		color = null;

		var s = raw?.Trim() ?? string.Empty;
		if (s.Length == 0)
			return;

		// Extract optional trailing color ": #RRGGBB" (spaces allowed after ':')
		var hexStart = -1;
		for (int i = s.Length - 1; i >= 0; i--)
		{
			if (s[i] != ':' || i + 1 >= s.Length || s[i + 1] != '#')
				continue;

			// Validate hex after "#"
			int h = i + 2;
			int len = s.Length - h;
			if (len is 3 or 6 or 8 && s.AsSpan(h).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0)
			{
				hexStart = i;
				color = ParseHexColor(s.AsSpan(h));
				break;
			}
		}

		if (hexStart >= 0)
			s = s[..hexStart].TrimEnd();

		if (s.Length == 0)
			return;

		// "TYPE:Title" — split only when the leading part is a known kind,
		// otherwise the whole content is a custom title (may contain ':').
		int colon = s.IndexOf(':');
		if (colon > 0)
		{
			var candidate = s[..colon];
			if (KnownKinds.Contains(candidate))
			{
				type = candidate;
				title = s[(colon + 1)..].Trim();
				return;
			}
		}

		if (KnownKinds.Contains(s))
			type = s;
		else
			title = s;
	}

	private static Color? ParseHexColor(ReadOnlySpan<char> hex)
	{
		static int HexVal(char c) => c switch
		{
			>= '0' and <= '9' => c - '0',
			>= 'a' and <= 'f' => c - 'a' + 10,
			>= 'A' and <= 'F' => c - 'A' + 10,
			_ => -1
		};

		static byte Pair(ReadOnlySpan<char> hex, int i)
			=> (byte)((HexVal(hex[i]) << 4) | HexVal(hex[i + 1]));

		try
		{
			return hex.Length switch
			{
				3 => Color.FromRgb(
					(byte)(HexVal(hex[0]) * 17),
					(byte)(HexVal(hex[1]) * 17),
					(byte)(HexVal(hex[2]) * 17)),
				6 => Color.FromRgb(Pair(hex, 0), Pair(hex, 2), Pair(hex, 4)),
				8 => Color.FromArgb(Pair(hex, 6), Pair(hex, 0), Pair(hex, 2), Pair(hex, 4)),
				_ => null
			};
		}
		catch
		{
			return null;
		}
	}

	private static string GetTypeClass(string kind)
	{
		if (string.IsNullOrWhiteSpace(kind))
			return AlertClass + "Note";

		var pascal = char.ToUpperInvariant(kind[0]) + kind[1..].ToLowerInvariant();
		return AlertClass + pascal;
	}

	private static VisualIconKind GetIcon(string kind) => kind.ToUpperInvariant() switch
	{
		"TIP" => MaterialIconKind.LightbulbOnOutline,
		"IMPORTANT" => MaterialIconKind.AlertOutline,
		"WARNING" => MaterialIconKind.Warning,
		"CAUTION" => MaterialIconKind.AlertOctagonOutline,
		"SUCCESS" => MaterialIconKind.CheckCircleOutline,
		"ERROR" => MaterialIconKind.CloseCircleOutline,
		_ => MaterialIconKind.InformationOutline
	};
}
