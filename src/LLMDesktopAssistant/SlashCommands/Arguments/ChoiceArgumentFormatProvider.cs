using System.Collections.Immutable;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// The format provider of a "one of many" command argument: it offers a fixed set of values as completions and, when
	/// strict, validates that the user's value is one of them.
	/// </summary>
	/// <remarks>
	/// A non-strict provider only suggests (free text stays valid); a strict one blocks the send with
	/// <c>command.error.invalid_argument</c> for anything outside the set. The value is a plain string - the provider
	/// does not convert it.
	/// </remarks>
	public sealed class ChoiceArgumentFormatProvider : ISlashCommandArgumentFormatProvider
	{
		private readonly ImmutableList<string> _choices;
		private readonly bool _strict;

		/// <summary>
		/// Creates a provider over <paramref name="choices"/>.
		/// </summary>
		/// <param name="choices">The accepted values, in the order they are offered.</param>
		/// <param name="strict">Whether a value outside <paramref name="choices"/> is invalid.</param>
		public ChoiceArgumentFormatProvider(IEnumerable<string> choices, bool strict)
		{
			_choices = [.. choices];
			_strict = strict;
		}

		/// <summary>The accepted values.</summary>
		public IReadOnlyList<string> Choices => _choices;

		/// <inheritdoc/>
		public bool CanComplete => true;

		/// <inheritdoc/>
		public bool TryValidate(string raw, out LocaleKeyBase? error)
		{
			if (!_strict || _choices.Contains(raw, StringComparer.OrdinalIgnoreCase))
			{
				error = null;
				return true;
			}

			error = Locale.GetKey("command.error.invalid_argument");
			return false;
		}

		/// <inheritdoc/>
		public object Convert(string raw) => raw;

		/// <inheritdoc/>
		public IEnumerable<SlashCommandCompletionItem> Complete(string prefix, SlashCommandCompletionContext ctx)
		{
			foreach (var choice in _choices)
			{
				if (choice.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
					yield return new SlashCommandCompletionItem { Value = choice };
			}
		}
	}
}
