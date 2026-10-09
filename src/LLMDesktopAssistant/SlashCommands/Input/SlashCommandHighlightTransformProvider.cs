using System;
using System.Collections.Generic;
using Avalonia.Media;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.InputCompletion;

namespace LLMDesktopAssistant.SlashCommands.Input
{
	/// <summary>
	/// The renderer of a command input: colours the token by its resolution state and the argument text, and appends
	/// the completion's ghost preview.
	/// </summary>
	/// <remarks>
	/// Pure projection — it does not compute the completion itself. The commands (for the analysis) and the current
	/// <see cref="InputCompletionResult"/> (for the ghost) are supplied through delegates, so the view model decides
	/// what is current and the renderer only draws it. Colour choice is caret-independent; the ghost is a suffix
	/// append (its mid-token placement and acceptance belong to the control).
	/// </remarks>
	public sealed class SlashCommandHighlightTransformProvider : IHighlightTransformProvider
	{
		private readonly Func<IReadOnlyList<SlashCommandInfo>> _commands;
		private readonly Func<InputCompletionResult?> _completion;
		private readonly SlashCommandHighlightPalette _palette;

		public SlashCommandHighlightTransformProvider(
			Func<IReadOnlyList<SlashCommandInfo>> commands,
			Func<InputCompletionResult?> completion,
			SlashCommandHighlightPalette? palette = null)
		{
			_commands = commands ?? throw new ArgumentNullException(nameof(commands));
			_completion = completion ?? throw new ArgumentNullException(nameof(completion));
			_palette = palette ?? SlashCommandHighlightPalette.Default;
		}

		/// <inheritdoc/>
		public event EventHandler? LayoutChanged;

		/// <summary>Notifies the control that the visual output changed without a text change.</summary>
		public void NotifyLayoutChanged() => LayoutChanged?.Invoke(this, EventArgs.Empty);

		/// <inheritdoc/>
		public HighlightTransformResult Transform(string text)
		{
			text ??= string.Empty;

			// With no commands there is nothing to highlight (commands disabled, or an empty set): leave the text alone.
			var commands = _commands();
			if (commands.Count == 0)
				return new HighlightTransformResult(null, null);

			// The colours are caret-independent (the caret flags are for the popup), so any caret index does here.
			var analysis = SlashCommandInputAnalyzer.Analyze(text, text.Length, commands);
			var spans = new List<TextHighlightSpan>();

			if (analysis.IsCommand)
			{
				if (TokenStyle(analysis.ResolutionState) is { } style)
					spans.Add(new TextHighlightSpan(analysis.TokenSpan.Start, analysis.TokenSpan.Length,
						style.Brush, style.Decorations));

				if (analysis.ArgumentSpan.Length > 0)
					spans.Add(new TextHighlightSpan(analysis.ArgumentSpan.Start, analysis.ArgumentSpan.Length,
						_palette.Argument));
			}

			string? rendered = null;
			if (_completion()?.GhostText is { Length: > 0 } ghost)
			{
				rendered = text + ghost;
				spans.Add(new TextHighlightSpan(text.Length, ghost.Length, _palette.Ghost));
			}

			return new HighlightTransformResult(rendered, spans.Count > 0 ? spans : null);
		}

		private (IBrush Brush, TextDecorationCollection? Decorations)? TokenStyle(
			SlashCommandInputResolutionState state) => state switch
		{
			SlashCommandInputResolutionState.Known => (_palette.Known, null),
			SlashCommandInputResolutionState.Unknown => (_palette.Unknown, _palette.UnknownDecorations),
			SlashCommandInputResolutionState.WonOthers => (_palette.Ambiguous, _palette.AmbiguousDecorations),
			_ => null
		};
	}
}
