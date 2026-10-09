using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LLMDesktopAssistant.SlashCommands.Input
{
	/// <summary>
	/// The colours the slash-command renderer uses. The defaults are code brushes; <see cref="FromResources"/> reads
	/// the named theme brushes (see <c>HighlightTextBoxTheme.axaml</c>) and falls back to the defaults when a resource
	/// is missing (headless host, custom theme).
	/// </summary>
	public sealed class SlashCommandHighlightPalette
	{
		/// <summary>The code-default palette.</summary>
		public static readonly SlashCommandHighlightPalette Default = new();

		/// <summary>The colour of a token that resolves to exactly one command.</summary>
		public IBrush Known { get; init; } = Hex("#FF6BC1");

		/// <summary>The colour of a token that resolves to nothing.</summary>
		public IBrush Unknown { get; init; } = Hex("#F44747");

		/// <summary>The colour of a token that resolved only by winning over others.</summary>
		public IBrush Ambiguous { get; init; } = Hex("#FFA500");

		/// <summary>The colour of the argument text.</summary>
		public IBrush Argument { get; init; } = Hex("#8C8C8C");

		/// <summary>The colour of a keyed argument's name.</summary>
		public IBrush ArgumentKey { get; init; } = Hex("#2ECC71");

		/// <summary>The colour of the <c>=</c> between a keyed argument's name and its value.</summary>
		public IBrush ArgumentEquals { get; init; } = Hex("#1E8E5A");

		/// <summary>The colour of a value's grouping quotes.</summary>
		public IBrush Quote { get; init; } = Hex("#B9B9CC");

		/// <summary>The chip behind an argument, so one argument reads as one unit.</summary>
		public IBrush ArgumentBackground { get; init; } = Hex("#268C8C8C");

		/// <summary>The colour of the ghost preview.</summary>
		public IBrush Ghost { get; init; } = Hex("#8C8C8C");

		/// <summary>The decorations of an unknown token.</summary>
		public TextDecorationCollection? UnknownDecorations { get; init; } = TextDecorations.Underline;

		/// <summary>The decorations of a token that won over others.</summary>
		public TextDecorationCollection? AmbiguousDecorations { get; init; } = TextDecorations.Underline;

		/// <summary>
		/// Builds a palette from the named theme brushes, falling back to <see cref="Default"/> per missing brush.
		/// </summary>
		public static SlashCommandHighlightPalette FromResources()
		{
			if (Application.Current is not { } app)
				return Default;

			return new SlashCommandHighlightPalette
			{
				Known = ResolveBrush(app, "SlashCommandKnownBrush") ?? Default.Known,
				Unknown = ResolveBrush(app, "SlashCommandUnknownBrush") ?? Default.Unknown,
				Ambiguous = ResolveBrush(app, "SlashCommandAmbiguousBrush") ?? Default.Ambiguous,
				Argument = ResolveBrush(app, "SlashCommandArgumentBrush") ?? Default.Argument,
				ArgumentKey = ResolveBrush(app, "SlashCommandArgumentKeyBrush") ?? Default.ArgumentKey,
				ArgumentEquals = ResolveBrush(app, "SlashCommandArgumentEqualsBrush") ?? Default.ArgumentEquals,
				Quote = ResolveBrush(app, "SlashCommandQuoteBrush") ?? Default.Quote,
				ArgumentBackground = ResolveBrush(app, "SlashCommandArgumentBackgroundBrush")
					?? Default.ArgumentBackground,
				Ghost = ResolveBrush(app, "SlashCommandGhostBrush") ?? Default.Ghost
			};
		}

		private static IBrush? ResolveBrush(IResourceHost host, string key)
			=> host.TryFindResource(key, out var value) && value is IBrush brush ? brush : null;

		private static IBrush Hex(string hex) => new SolidColorBrush(Color.Parse(hex));
	}
}
