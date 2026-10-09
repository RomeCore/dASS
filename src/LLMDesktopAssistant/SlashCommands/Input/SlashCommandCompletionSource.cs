using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia.Media;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.InputCompletion;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands.Arguments;

namespace LLMDesktopAssistant.SlashCommands.Input
{
	/// <summary>
	/// The slash-command input-completion source: it completes the leading command token from every command the chat
	/// enabled and, once the token resolves, the argument under the caret — alongside the command's context (its name,
	/// its description and the arguments it declares).
	/// </summary>
	/// <remarks>
	/// <para>
	/// It is also the renderer of its own regions (<see cref="IInputCompletionRenderer"/>): what the caret sits in, how
	/// it resolves and how it looks are the same knowledge, so the analysis, the parse and the palette live here rather
	/// than in a renderer registered next to the source. The inline ghost of a completion belongs to the input instead —
	/// see <c>InputCompletionTransformProvider</c>.
	/// </para>
	/// <para>
	/// Two completion states. <b>Token</b> — the caret is inside the token: prefix-match it against the commands, mark
	/// the shadowed ones (<see cref="InputCompletionItem.IsDefeated"/>) and offer each in its fully-qualified form
	/// (<c>/skill:grilling</c>). <b>Argument</b> — the token resolved and the caret is in the argument region: build the
	/// command's context, name the argument under the caret and delegate, when the slot declares a
	/// <see cref="ISlashCommandArgumentFormatProvider"/> that <see cref="ISlashCommandArgumentFormatProvider.CanComplete"/>,
	/// to <see cref="ISlashCommandArgumentFormatProvider.Complete"/>. There is no argument-kind-specific code here.
	/// </para>
	/// <para>
	/// The argument state is produced for the whole argument region — the free text of a rest positional included, where
	/// there is nothing to complete but the context is still worth showing. The only argument region that yields nothing
	/// is the one whose token does not resolve.
	/// </para>
	/// </remarks>
	[ChatService(typeof(IInputCompletionSource))]
	[ChatService(typeof(IInputCompletionRenderer))]
	public class SlashCommandCompletionSource(IAddonSetCollector<SlashCommandInfo> commands)
		: IInputCompletionSource, IInputCompletionRenderer
	{
		/// <summary>
		/// The source's priority. Commands claim only the leading token and its arguments, so they sit above any
		/// generic source a caret could otherwise fall into.
		/// </summary>
		public const int CommandSourcePriority = 100;

		/// <summary>
		/// Whether the declared arguments stay listed while the caret sits in one of them: the context block then carries
		/// the current argument's heading *and* the list. Flip to <see langword="false"/> to show the current argument
		/// alone, leaving the list to the gaps between arguments.
		/// </summary>
		public const bool ShowArgumentListWithCurrentArgument = true;

		private static readonly SlashCommandArgumentSchema EmptySchema = new();

		private readonly SlashCommandHighlightPalette _palette = SlashCommandHighlightPalette.FromResources();

		/// <inheritdoc/>
		public int Priority => CommandSourcePriority;

		/// <inheritdoc/>
		public bool TryCompute(InputCompletionRequest request, [NotNullWhen(true)] out InputCompletionResult? result)
		{
			result = null;

			var commandSet = commands.GetAddonsForChat().ToList();
			if (commandSet.Count == 0)
				return false;

			var analysis = SlashCommandInputAnalyzer.Analyze(request.Text, request.CaretIndex, commandSet);
			if (!analysis.IsCommand)
				return false;

			if (analysis.IsCaretInToken)
			{
				result = ComputeToken(request, analysis, commandSet);
				return true;
			}

			if (analysis.IsCaretInArguments)
				return TryComputeArgument(request, analysis, commandSet, out result);

			return false;
		}

		/// <inheritdoc/>
		public HighlightTransformResult? Render(string text, int caretIndex)
		{
			text ??= string.Empty;

			var commandSet = commands.GetAddonsForChat().ToList();
			if (commandSet.Count == 0)
				return null;

			var analysis = SlashCommandInputAnalyzer.Analyze(text, caretIndex, commandSet);
			if (!analysis.IsCommand)
				return null;

			var spans = new List<TextHighlightSpan>();
			if (TokenStyle(analysis.ResolutionState) is { } style)
			{
				spans.Add(new TextHighlightSpan(analysis.TokenSpan.Start, analysis.TokenSpan.Length,
					style.Brush, style.Decorations));
			}

			AddArgumentSpans(spans, text, analysis);

			return spans.Count > 0 ? new HighlightTransformResult(spans) : null;
		}

		/// <summary>
		/// The token state: the prefix matches, ordered by <c>Order</c> then name, each in its fully-qualified form,
		/// shadowed ones marked. An empty match set still yields a result — the popup shows "no matches".
		/// </summary>
		private static InputCompletionResult ComputeToken(InputCompletionRequest request,
			SlashCommandInputAnalysis analysis, IReadOnlyList<SlashCommandInfo> commandSet)
		{
			var matches = SlashCommandPrefixMatcher.Match(commandSet, analysis.Token);
			var defeated = FindDefeated(matches);

			var items = matches
				.OrderBy(command => command.Order)
				.ThenBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
				.ThenBy(command => command.Key, StringComparer.Ordinal)
				.Select(command => TokenItem(command, commandSet, defeated.Contains(command)))
				.ToList();

			// The ghost previews the top match only while it *continues* the typed token: a command reached through an
			// alias or a namespace prefix has a fully-qualified form that does not extend what is typed, so there is
			// nothing to preview and the inline accept stays inert.
			var typed = TypedToken(request, analysis.TokenSpan);
			var ghost = items.Count > 0
				? GhostOf(items[0].InsertText, typed, previewWithoutPrefix: false)
				: null;

			return new InputCompletionResult
			{
				Span = analysis.TokenSpan,
				State = new InputCompletionState
				{
					Kind = InputCompletionKind.Command,
					Title = Locale.GetKey("command.completion.title.commands")
				},
				Items = items,
				GhostText = ghost
			};
		}

		/// <summary>
		/// One token-state continuation: the command under the shortest form it can safely be written in, with its
		/// fully-qualified token riding along as the hint when the two differ.
		/// </summary>
		private static InputCompletionItem TokenItem(SlashCommandInfo command,
			IReadOnlyList<SlashCommandInfo> commandSet, bool isDefeated)
		{
			var insertText = "/" + SlashCommandShortToken.For(command, commandSet);
			var full = "/" + command.CanonicalToken;

			return new InputCompletionItem
			{
				InsertText = insertText,
				Hint = insertText == full ? null : full,
				Description = command.DescriptionKey,
				Kind = InputCompletionKind.Command,
				IsDefeated = isDefeated
			};
		}

		/// <summary>
		/// The raw text of the token from its start up to the caret — the leading <c>/</c> included, so it compares
		/// against an <see cref="InputCompletionItem.InsertText"/> directly.
		/// </summary>
		private static string TypedToken(InputCompletionRequest request, InputCompletionSpan tokenSpan)
		{
			var text = request.Text ?? string.Empty;
			var start = Math.Clamp(tokenSpan.Start, 0, text.Length);
			var end = Math.Clamp(request.CaretIndex, start, text.Length);
			return text[start..end];
		}

		/// <summary>
		/// The suffix of <paramref name="insertText"/> the typed prefix does not carry yet — the ghost preview — or
		/// <see langword="null"/> when there is nothing left to preview or the completion does not continue the prefix.
		/// </summary>
		/// <remarks>
		/// An empty prefix previews the completion in full only with <paramref name="previewWithoutPrefix"/>: a keyed
		/// value has one obvious completion to offer, while a bare <c>/</c> has none.
		/// </remarks>
		private static string? GhostOf(string insertText, string typedPrefix, bool previewWithoutPrefix)
		{
			if (typedPrefix.Length == 0 && !previewWithoutPrefix)
				return null;

			if (!insertText.StartsWith(typedPrefix, StringComparison.OrdinalIgnoreCase))
				return null;

			return insertText.Length > typedPrefix.Length ? insertText[typedPrefix.Length..] : null;
		}

		/// <summary>
		/// The argument state: the command as the header, the argument under the caret (or the declared-arguments
		/// heading) as the context and, when the slot declares a completable format provider, its continuations.
		/// Requires a resolved command; returns <see langword="false"/> when there is none, because an unresolved token
		/// has no context to show.
		/// </summary>
		private static bool TryComputeArgument(InputCompletionRequest request, SlashCommandInputAnalysis analysis,
			IReadOnlyList<SlashCommandInfo> commandSet, out InputCompletionResult? result)
		{
			result = null;
			if (analysis.Command is not { } command)
				return false;

			var rawArguments = request.Text[analysis.ArgumentSpan.Start..];
			var caretOffset = request.CaretIndex - analysis.ArgumentSpan.Start;
			var schema = command.Executor.ArgumentSchema ?? EmptySchema;

			// The analyzer already parsed the region; an invalid argument list (an unterminated quote, say) leaves the
			// caret in no argument, so the popup keeps showing the command's context without a picker.
			var parsed = analysis.Arguments;
			var target = parsed is null ? null : SlashCommandArgumentLookup.Find(parsed, rawArguments, caretOffset);

			var items = new List<InputCompletionItem>();
			string? completablePrefix = null;
			if (target is { Slot.Format: { CanComplete: true } format } argument)
			{
				completablePrefix = argument.Prefix;
				var context = new SlashCommandCompletionContext
				{
					Command = command,
					RawArguments = rawArguments,
					CurrentPrefix = argument.Prefix
				};

				items.AddRange(format.Complete(argument.Prefix, context).Select(completion => new InputCompletionItem
				{
					InsertText = completion.Value,
					Display = completion.Display,
					Description = completion.Description,
					Kind = InputCompletionKind.Argument
				}));
			}

			// The ghost previews the top completion, so the input can accept it character by character (e.g. "wait=tr" →
			// "ue"); a keyed value previews in full even before its first character is typed.
			var ghost = items.Count > 0
				? GhostOf(items[0].InsertText, completablePrefix!, previewWithoutPrefix: true)
				: null;

			result = new InputCompletionResult
			{
				Span = target is { } value
					? new InputCompletionSpan(analysis.ArgumentSpan.Start + value.ValueStart, value.ValueLength)
					: new InputCompletionSpan(analysis.ArgumentSpan.Start, 0),
				State = BuildArgumentState(command, commandSet, schema, target),
				Items = items,
				GhostText = ghost
			};
			return true;
		}

		/// <summary>
		/// The argument state's presentation: the command's identity as the header, the argument the caret sits in (or
		/// the declared-arguments heading) as the context block, and the declared arguments as its rows.
		/// </summary>
		private static InputCompletionState BuildArgumentState(SlashCommandInfo command,
			IReadOnlyList<SlashCommandInfo> commandSet, SlashCommandArgumentSchema schema,
			SlashCommandArgumentTarget? target)
		{
			var slot = target?.Slot;
			var withList = ShowArgumentListWithCurrentArgument || slot is null;

			return new InputCompletionState
			{
				Kind = InputCompletionKind.Argument,
				Title = Locale.GetConstKey("/" + SlashCommandShortToken.For(command, commandSet)),
				Description = command.DescriptionKey,
				ContextTitle = slot?.Name ?? Locale.GetKey("command.completion.arguments"),
				ContextDescription = slot?.Description,
				ContextItems = withList ? BuildContextItems(schema, slot) : []
			};
		}

		/// <summary>
		/// The declared arguments as context rows, in schema order: the positionals, the rest positional, then the keyed
		/// ones. The argument the caret sits in is marked, and so is one the command requires.
		/// </summary>
		private static IReadOnlyList<InputCompletionContextItem> BuildContextItems(
			SlashCommandArgumentSchema schema, SlashCommandArgument? current)
		{
			var items = new List<InputCompletionContextItem>();

			foreach (var positional in schema.Positionals)
				items.Add(ContextItem(positional, current));

			if (schema.RestPositional is { } rest)
				items.Add(ContextItem(rest, current));

			foreach (var keyed in schema.Keyed.Values)
				items.Add(ContextItem(keyed, current));

			return items;
		}

		private static InputCompletionContextItem ContextItem(SlashCommandArgument argument, SlashCommandArgument? current)
			=> new()
			{
				Name = argument.Name,
				Description = argument.Description,
				IsRequired = argument.Required,
				IsCurrent = ReferenceEquals(argument, current)
			};

		/// <summary>
		/// The commands shadowed by a same-named command of higher precedence (higher <c>OverrideOrder</c>, then lower
		/// <c>Order</c>, then key). They stay reachable through a qualifier.
		/// </summary>
		private static HashSet<SlashCommandInfo> FindDefeated(IReadOnlyList<SlashCommandInfo> matches)
		{
			var defeated = new HashSet<SlashCommandInfo>();

			foreach (var group in matches.GroupBy(command => command.Name, StringComparer.OrdinalIgnoreCase))
			{
				var ordered = group
					.OrderByDescending(command => command.OverrideOrder)
					.ThenBy(command => command.Order)
					.ThenBy(command => command.Key, StringComparer.Ordinal)
					.ToList();

				for (var i = 1; i < ordered.Count; i++)
					defeated.Add(ordered[i]);
			}

			return defeated;
		}

		/// <summary>
		/// The brush and the decorations of a token, by how it resolves; <see langword="null"/> for a token that has not
		/// resolved yet (nothing to say about it yet).
		/// </summary>
		private (IBrush Brush, TextDecorationCollection? Decorations)? TokenStyle(
			SlashCommandInputResolutionState state) => state switch
		{
			SlashCommandInputResolutionState.Known => (_palette.Known, null),
			SlashCommandInputResolutionState.Unknown => (_palette.Unknown, _palette.UnknownDecorations),
			SlashCommandInputResolutionState.WonOthers => (_palette.Ambiguous, _palette.AmbiguousDecorations),
			_ => null
		};

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
				if (text[i] is not ('\'' or '"'))
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
	}
}
