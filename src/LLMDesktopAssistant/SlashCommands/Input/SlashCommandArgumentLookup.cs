using LLMDesktopAssistant.SlashCommands.Arguments;

namespace LLMDesktopAssistant.SlashCommands.Input
{
	/// <summary>
	/// The argument the caret is currently inside, found in a parsed argument list: its schema slot (null for a
	/// surplus positional), the span of its value in the raw argument text and the value prefix typed before the caret.
	/// </summary>
	public readonly record struct SlashCommandArgumentTarget(
		SlashCommandArgument? Slot, int ValueStart, int ValueLength, string Prefix);

	/// <summary>
	/// Locates the argument under the caret in a parsed argument list. Pure and static.
	/// </summary>
	public static class SlashCommandArgumentLookup
	{
		/// <summary>
		/// The argument whose value region contains <paramref name="caretOffset"/> (a 0-based offset into
		/// <paramref name="rawArguments"/>), or <see langword="null"/> when the caret is between arguments (or on a
		/// keyed argument's key, which v1 does not complete). The rest positional counts as an argument: its region is
		/// the whole surplus, so a caret anywhere in the free text resolves to it.
		/// </summary>
		public static SlashCommandArgumentTarget? Find(SlashCommandParsedArguments parsed, string rawArguments,
			int caretOffset)
		{
			ArgumentNullException.ThrowIfNull(parsed);
			ArgumentNullException.ThrowIfNull(rawArguments);

			foreach (var argument in parsed.Positionals)
			{
				if (TryTarget(argument, rawArguments, caretOffset, out var target))
					return target;
			}

			foreach (var argument in parsed.Keyed.Values)
			{
				if (TryTarget(argument, rawArguments, caretOffset, out var target))
					return target;
			}

			// The rest positional is asked last: it spans the whole surplus region, so a declared or keyed argument that
			// contains the caret has already won by the time it is reached.
			if (parsed.RestPositional is { } rest && TryTarget(rest, rawArguments, caretOffset, out var restTarget))
				return restTarget;

			return null;
		}

		private static bool TryTarget(SlashCommandRawArgument argument, string rawArguments, int caretOffset,
			out SlashCommandArgumentTarget target)
		{
			var valueStart = argument.ValuePosition;
			var valueEnd = argument.ValuePosition + argument.ValueLength;

			// A keyed argument's key is not a completable value: only the region after '=' counts.
			if (caretOffset < valueStart || caretOffset > valueEnd)
			{
				target = default;
				return false;
			}

			target = new SlashCommandArgumentTarget(argument.Definition, valueStart, argument.ValueLength,
				rawArguments[valueStart..caretOffset]);
			return true;
		}
	}
}
