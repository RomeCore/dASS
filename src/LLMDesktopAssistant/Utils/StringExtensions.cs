using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LLMDesktopAssistant.Markdown;

namespace LLMDesktopAssistant.Utils
{
	public static class StringExtensions
	{
		public static string? ToNullIfEmpty(this string? value)
		{
			if (string.IsNullOrEmpty(value))
				return null;
			return value;
		}

		public static string? ToNullIfWhiteSpace(this string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return null;
			return value;
		}

		public static string Slugify(this string phrase)
		{
			if (string.IsNullOrWhiteSpace(phrase))
			{
				return string.Empty;
			}

			// 1. Lowercase the string
			string str = phrase.ToLowerInvariant();

			// 2. Remove diacritics (accents) like á, é, ö into a, e, o
			string normalizedString = str.Normalize(NormalizationForm.FormD);
			StringBuilder stringBuilder = new StringBuilder();

			foreach (char c in normalizedString)
			{
				UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
				if (unicodeCategory != UnicodeCategory.NonSpacingMark)
				{
					stringBuilder.Append(c);
				}
			}

			str = stringBuilder.ToString().Normalize(NormalizationForm.FormC);

			// 3. Invalid characters removal
			str = Regex.Replace(str, @"[^a-z0-9\s-]", "");

			// 4. Convert multiple spaces into a single space
			str = Regex.Replace(str, @"\s+", " ").Trim();

			// 5. Replace spaces with hyphens
			str = Regex.Replace(str, @"\s", "-");

			return str;
		}

		public static string MarkdownEscape(this string text)
		{
			return MarkdownEscaper.Escape(text) ?? string.Empty;
		}

		public static string? TruncateEnd(this string? text, int maxLength, string suffix = "...")
		{
			if (string.IsNullOrEmpty(text))
				return text;
			if (text.Length <= maxLength)
				return text;
			return string.Concat(text.AsSpan(0, maxLength - suffix.Length), suffix);
		}
	}
}