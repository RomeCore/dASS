using System.Text.Json;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// The format provider of a structured command argument — a tool's <c>array</c> or <c>object</c> property. The
	/// value is written as a JSON literal (<c>paths=["a","b"]</c>, <c>options={"depth":2}</c>) and converted into a
	/// <see cref="JsonNode"/>.
	/// </summary>
	/// <remarks>
	/// The literal is only checked for being valid JSON: the expected node kind is a property of the slot this
	/// provider does not carry, and the tool's own binding is the authority on the shape anyway. <c>null</c> is
	/// rejected — a missing structured argument is expressed by omitting the key, not by writing the JSON null.
	/// </remarks>
	public sealed class JsonArgumentFormatProvider : ISlashCommandArgumentFormatProvider
	{
		/// <summary>The shared stateless instance.</summary>
		public static readonly JsonArgumentFormatProvider Instance = new();

		private JsonArgumentFormatProvider()
		{
		}

		/// <inheritdoc/>
		public bool CanComplete => false;

		/// <inheritdoc/>
		public bool TryValidate(string raw, out LocaleKeyBase? error)
		{
			if (TryParse(raw) is not null)
			{
				error = null;
				return true;
			}

			error = Locale.GetKey("command.error.invalid_argument");
			return false;
		}

		/// <inheritdoc/>
		public object? Convert(string raw) => TryParse(raw);

		/// <inheritdoc/>
		public IEnumerable<SlashCommandCompletionItem> Complete(string prefix, SlashCommandCompletionContext ctx)
			=> [];

		private static JsonNode? TryParse(string raw)
		{
			try
			{
				return JsonNode.Parse(raw);
			}
			catch (JsonException)
			{
				return null;
			}
		}
	}
}
