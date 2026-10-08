using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// The format provider of a boolean command argument: validates and converts <c>true</c> / <c>false</c> and offers
	/// them as completions. Used by the <c>/agent</c> command's <c>wait</c> argument.
	/// </summary>
	public sealed class SlashCommandBooleanFormatProvider : ISlashCommandArgumentFormatProvider
	{
		/// <summary>The shared stateless instance.</summary>
		public static readonly SlashCommandBooleanFormatProvider Instance = new();

		private SlashCommandBooleanFormatProvider()
		{
		}

		/// <inheritdoc/>
		public bool CanComplete => true;

		/// <inheritdoc/>
		public bool TryValidate(string raw, out LocaleKeyBase? error)
		{
			if (bool.TryParse(raw, out _))
			{
				error = null;
				return true;
			}

			error = Locale.GetKey("command.error.invalid_argument");
			return false;
		}

		/// <inheritdoc/>
		public object Convert(string raw) => bool.Parse(raw);

		/// <inheritdoc/>
		public IEnumerable<SlashCommandCompletionItem> Complete(string prefix, SlashCommandCompletionContext ctx)
		{
			if ("true".StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				yield return new SlashCommandCompletionItem { Value = "true" };

			if ("false".StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				yield return new SlashCommandCompletionItem { Value = "false" };
		}
	}
}
