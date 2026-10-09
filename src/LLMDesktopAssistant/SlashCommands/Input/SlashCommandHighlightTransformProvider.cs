using System;
using System.Collections.Generic;
using Avalonia.Media;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.InputCompletion;
using LLMDesktopAssistant.SlashCommands.Arguments;

namespace LLMDesktopAssistant.SlashCommands.Input
{
	/// <summary>
	/// The renderer of a command input: colours the token by its resolution state, paints every argument as one chipped
	/// unit (a keyed name green, its <c>=</c> darker green, the value grey, the grouping quotes lighter) and substitutes
	/// the completion's ghost preview for the token's tail. It also exposes that preview through
	/// <see cref="IInlineCompletionProvider"/>, so the input control can accept it character by character.
	/// </summary>
	/// <remarks>
	/// Pure projection — it does not compute the completion itself. The commands (for the analysis), the caret and the
	/// current <see cref="InputCompletionResult"/> (for the ghost) are supplied through delegates, so the view model
	/// decides what is current and the renderer only draws it. Colour choice is caret-independent; the ghost replaces
	/// the region's tail from the caret (mid-string is safe — the control clamps the caret and the selection to the real
	/// text and resets the completion whenever the pointer moves them).
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

				AddArgumentSpans(spans, text, analysis);
			}

			string? rendered = null;
			if (_completion() is { GhostText: { Length: > 0 } ghost } completion)
			{
				var caretIndex = Math.Clamp(_caret(), 0, text.Length);

				// The ghost *replaces* the tail of the region the completion would replace rather than being inserted
				// before it: previewing the missing part of a token ("wait=tru" → "e") would otherwise draw the real tail
				// a second time ("truee").
				var tailEnd = Math.Clamp(completion.Span.End, caretIndex, text.Length);

				CompletionText = ghost;
				TokenEnd = completion.Span.End;

				rendered = text[..caretIndex] + ghost + text[tailEnd..];
				spans.Add(new TextHighlightSpan(caretIndex, ghost.Length, _palette.Ghost));
			}

			return new HighlightTransformResult(rendered, spans.Count > 0 ? spans : null);
		}

		/// <summary>
		/// Paints the arguments: every argument reads as one chipped unit, and inside it a keyed name, its <c>=</c>, the
		/// value and the grouping quotes get their own colours. Without a parse — an unresolved token, or an invalid
		/// argument list — the whole argument region falls back to the plain argument colour.
		/// </summary>
		private void AddArgumentSpans(List<TextHighlightSpan> spans, string text, SlashCommandInputAnalysis analysis)
		{
			if (analysis.ArgumentSpan.Length == 0)
				return;

			if (analysis.Arguments is not { } parsed)
			{
				spans.Add(new TextHighlightSpan(analysis.ArgumentSpan.Start, analysis.ArgumentSpan.Length,
					_palette.Argument));
				return;
			}

			// An argument's parts are contiguous and the parts of an argument precede the next argument's, so in raw-text
			// order the spans never overlap — which matters, because a text run carries a single property set and the
			// first span covering a position wins.
			var offset = analysis.ArgumentSpan.Start;
			foreach (var argument in ArgumentsOf(parsed).OrderBy(argument => argument.Position))
			{
				if (argument.KeyLength > 0)
				{
					AddPart(spans, text, offset + argument.Position, argument.KeyLength, _palette.ArgumentKey);
					AddPart(spans, text, offset + argument.Position + argument.KeyLength, 1, _palette.ArgumentEquals);
				}

				AddValueParts(spans, text, offset + argument.ValuePosition, argument.ValueLength);
			}
		}

		private static IEnumerable<SlashCommandRawArgument> ArgumentsOf(SlashCommandParsedArguments parsed)
		{
			foreach (var positional in parsed.Positionals)
				yield return positional;

			if (parsed.RestPositional is { } rest)
				yield return rest;

			foreach (var keyed in parsed.Keyed.Values)
				yield return keyed;
		}

		/// <summary>
		/// Paints a value: the grouping quotes lighter, the text between them in the argument colour.
		/// </summary>
		private void AddValueParts(List<TextHighlightSpan> spans, string text, int start, int length)
		{
			var end = Math.Min(start + length, text.Length);
			var segmentStart = start;

			for (var i = Math.Max(0, start); i < end; i++)
			{
				if (text[i] is not ('\'' or '\"'))
					continue;

				if (i > segmentStart)
					AddPart(spans, text, segmentStart, i - segmentStart, _palette.Argument);

				AddPart(spans, text, i, 1, _palette.Quote);
				segmentStart = i + 1;
			}

			if (end > segmentStart)
				AddPart(spans, text, segmentStart, end - segmentStart, _palette.Argument);
		}

		/// <summary>
		/// Adds one part of an argument: its own colour over the argument's chip. A part carries both, because a text run
		/// has a single property set — the chip cannot be a separate span drawn underneath.
		/// </summary>
		private void AddPart(List<TextHighlightSpan> spans, string text, int start, int length, IBrush brush)
		{
			start = Math.Max(0, start);
			length = Math.Min(length, text.Length - start);
			if (length <= 0)
				return;

			spans.Add(new TextHighlightSpan(start, length, brush, null, _palette.ArgumentBackground));
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
