using System.Globalization;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// The format provider of a numeric command argument. The value is always converted to <see cref="double"/>
	/// (<see cref="StructuredValues"/> uses doubles throughout), validated with the invariant culture and never
	/// completed.
	/// </summary>
	public sealed class NumberArgumentFormatProvider : ISlashCommandArgumentFormatProvider
	{
		/// <summary>The shared stateless instance.</summary>
		public static readonly NumberArgumentFormatProvider Instance = new();

		private NumberArgumentFormatProvider()
		{
		}

		/// <inheritdoc/>
		public bool CanComplete => false;

		/// <inheritdoc/>
		public bool TryValidate(string raw, out LocaleKeyBase? error)
		{
			if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
			{
				error = null;
				return true;
			}

			error = Locale.GetKey("command.error.invalid_argument");
			return false;
		}

		/// <inheritdoc/>
		public object Convert(string raw)
			=> double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);

		/// <inheritdoc/>
		public IEnumerable<SlashCommandCompletionItem> Complete(string prefix, SlashCommandCompletionContext ctx)
			=> [];
	}
}
