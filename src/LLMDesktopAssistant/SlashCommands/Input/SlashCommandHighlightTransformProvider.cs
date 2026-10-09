using System;
using System.Collections.Generic;
using Avalonia.Media;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.InputCompletion;

namespace LLMDesktopAssistant.SlashCommands.Input
{
	/// <summary>
	/// The renderer of a command input: colours the token by its resolution state and the argument text, and inserts
	/// the completion's ghost preview at the caret. It also exposes that preview through
	/// <see cref="IInlineCompletionProvider"/>, so the input control can accept it character by character.
	/// </summary>
	/// <remarks>
	/// Pure projection — it does not compute the completion itself. The commands (for the analysis), the caret and the
	/// current <see cref="InputCompletionResult"/> (for the ghost) are supplied through delegates, so the view model
	/// decides what is current and the renderer only draws it. Colour choice is caret-independent; the ghost is
	/// inserted at the caret (mid-string is safe, the control resets on a manual caret move).
	/// </remarks>
	public sealed class SlashCommandHighlightTransformProvider : IHighlightTransformProvider, IInlineCompletionProvider
	{
		private readonly Func<IReadOnlyList<SlashCommandInfo>> _commands;
		private readonly Func<int> _caret;
		private readonly Func<InputCompletionResult?> _completion;
		private readonly SlashCommandHighlightPalette _palette;

		public SlashCommandHighlightTransformProvider(
			Func<IReadOnlyList<SlashCommandInfo>> commands,
			Func<int> caret,
			Func<InputCompletionResult?> completion,
			SlashCommandHighlightPalette? palette = null)
		{
			_commands = commands ?? throw new ArgumentNullException(nameof(commands));
			_caret = caret ?? throw new ArgumentNullException(nameof(caret));
			_completion = completion ?? throw new ArgumentNullException(nameof(completion));
			_palette = palette ?? SlashCommandHighlightPalette.Default;
		}

		/// <inheritdoc/>
		public event EventHandler? LayoutChanged;

		/// <inheritdoc/>
		public string? CompletionText { get; private set; }

		/// <inheritdoc/>
		public int TokenEnd { get; private set; }

		/// <summary>Notifies the control that the visual output changed without a text change.</summary>
		public void NotifyLayoutChanged() => LayoutChanged?.Invoke(this, EventArgs.Empty);

		/// <inheritdoc/>
		public HighlightTransformResult Transform(string text)
		{
			text ??= string.Empty;
			CompletionText = null;
			TokenEnd = 0;

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
				var caretIndex = Math.Clamp(_caret(), 0, text.Length);
				CompletionText = ghost;
				TokenEnd = _completion()!.Span.End;

				rendered = text[..caretIndex] + ghost + text[caretIndex..];
				spans.Add(new TextHighlightSpan(caretIndex, ghost.Length, _palette.Ghost));
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
