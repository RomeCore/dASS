using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.InputCompletion;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.SlashCommands.Arguments;

namespace LLMDesktopAssistant.SlashCommands.Input
{
	/// <summary>
	/// The slash-command <see cref="IInputCompletionSource"/>: completes the leading command token from every command
	/// the chat enabled, and, once the token resolves, the argument under the caret.
	/// </summary>
	/// <remarks>
	/// Two states. <b>Token</b> — the caret is inside the token: prefix-match it against the commands, mark the
	/// shadowed ones (<see cref="InputCompletionItem.IsDefeated"/>) and offer each in its fully-qualified form
	/// (<c>/skill:grilling</c>). <b>Argument</b> — the token resolved and the caret is in the argument region: find the
	/// argument under the caret and delegate, when the slot declares a
	/// <see cref="ISlashCommandArgumentFormatProvider"/> that <see cref="ISlashCommandArgumentFormatProvider.CanComplete"/>,
	/// to <see cref="ISlashCommandArgumentFormatProvider.Complete"/>. There is no argument-kind-specific code here.
	/// </remarks>
	[ChatService(typeof(IInputCompletionSource))]
	public class SlashCommandCompletionSource(IAddonSetCollector<SlashCommandInfo> commands) : IInputCompletionSource
	{
		/// <summary>
		/// The source's priority. Commands claim only the leading token and its arguments, so they sit above any
		/// generic source a caret could otherwise fall into.
		/// </summary>
		public const int CommandSourcePriority = 100;

		private static readonly SlashCommandArgumentSchema EmptySchema = new();

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
				result = ComputeToken(analysis, commandSet);
				return true;
			}

			if (analysis.IsCaretInArguments)
				return TryComputeArgument(request, analysis, out result);

			return false;
		}

		/// <summary>
		/// The token state: the prefix matches, ordered by <c>Order</c> then name, each in its fully-qualified form,
		/// shadowed ones marked. An empty match set still yields a result — the popup shows "no matches".
		/// </summary>
		private static InputCompletionResult ComputeToken(SlashCommandInputAnalysis analysis,
			IReadOnlyList<SlashCommandInfo> commandSet)
		{
			var matches = SlashCommandPrefixMatcher.Match(commandSet, analysis.Token);
			var defeated = FindDefeated(matches);

			var items = matches
				.OrderBy(command => command.Order)
				.ThenBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
				.ThenBy(command => command.Key, StringComparer.Ordinal)
				.Select(command => new InputCompletionItem
				{
					InsertText = "/" + command.CanonicalToken,
					Description = command.DescriptionKey,
					Kind = InputCompletionKind.Command,
					IsDefeated = defeated.Contains(command)
				})
				.ToList();

			return new InputCompletionResult
			{
				Span = analysis.TokenSpan,
				State = new InputCompletionState { Kind = InputCompletionKind.Command },
				Items = items
			};
		}

		/// <summary>
		/// The argument state. Requires a resolved command; returns <see langword="false"/> when none (nothing to
		/// complete against).
		/// </summary>
		private static bool TryComputeArgument(InputCompletionRequest request, SlashCommandInputAnalysis analysis,
			out InputCompletionResult? result)
		{
			result = null;
			if (analysis.Command is not { } command)
				return false;

			var rawArguments = request.Text[analysis.ArgumentSpan.Start..];
			var caretOffset = request.CaretIndex - analysis.ArgumentSpan.Start;
			var schema = command.Executor.ArgumentSchema ?? EmptySchema;
			var parsed = SlashCommandArgumentParser.Parse(schema, rawArguments);

			if (SlashCommandArgumentLookup.Find(parsed, rawArguments, caretOffset) is not { } target)
				return false;

			var items = new List<InputCompletionItem>();
			if (target.Slot?.Format is { CanComplete: true } format)
			{
				var context = new SlashCommandCompletionContext
				{
					Command = command,
					RawArguments = rawArguments,
					CurrentPrefix = target.Prefix
				};

				items.AddRange(format.Complete(target.Prefix, context).Select(completion => new InputCompletionItem
				{
					InsertText = completion.Value,
					Display = completion.Display,
					Description = completion.Description,
					Kind = InputCompletionKind.Argument
				}));
			}

			result = new InputCompletionResult
			{
				Span = new InputCompletionSpan(analysis.ArgumentSpan.Start + target.ValueStart, target.ValueLength),
				State = new InputCompletionState { Kind = InputCompletionKind.Argument },
				Items = items
			};
			return true;
		}

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
	}
}
