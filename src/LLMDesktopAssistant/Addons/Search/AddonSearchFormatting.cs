using System.Text;
using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Addons.Search
{
	/// <summary>
	/// The formatting helpers for the <see cref="IAddonAgenticSearchProvider"/> implementations:
	/// they keep the rendered prompt fragments compact and consistent across the addon kinds.
	/// </summary>
	public static class AddonSearchFormatting
	{
		/// <summary>
		/// The maximum length of an inline text fragment (a description, a manual, etc.).
		/// </summary>
		public const int MaxInlineLength = 400;

		private static readonly char[] WhitespaceSeparators = [' ', '\t', '\r', '\n'];

		/// <summary>
		/// Collapses the whitespace of the specified text and truncates it, so it always fits into a single
		/// markdown line.
		/// </summary>
		public static string Inline(string? text, int maxLength = MaxInlineLength)
		{
			if (string.IsNullOrWhiteSpace(text))
				return string.Empty;

			var collapsed = string.Join(" ", text.Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries));
			if (collapsed.Length <= maxLength)
				return collapsed;

			return collapsed[..maxLength].TrimEnd() + "…";
		}

		/// <summary>
		/// Appends a markdown list item for the specified addon: '- `name` (suffix) - description'.
		/// </summary>
		public static void AppendItem(StringBuilder builder, string name, string? description, string? nameSuffix = null)
		{
			builder.Append("- `").Append(name).Append('`');

			if (!string.IsNullOrEmpty(nameSuffix))
				builder.Append(' ').Append(nameSuffix);

			var inlineDescription = Inline(description);
			if (inlineDescription.Length > 0)
				builder.Append(" - ").Append(inlineDescription);

			builder.AppendLine();
		}

		/// <summary>
		/// Appends the common addon metadata block (tags, source pack and home directory) used by the
		/// providers in the detailed mode.
		/// </summary>
		public static void AppendMetadata(StringBuilder builder,
			IEnumerable<string> tags, string? sourcePackName, string? path)
		{
			var tagList = tags.ToList();
			if (tagList.Count > 0)
				builder.Append("  - tags: ").AppendLine(string.Join(", ", tagList));

			if (!string.IsNullOrEmpty(sourcePackName))
				builder.Append("  - pack: ").AppendLine(sourcePackName);

			if (!string.IsNullOrEmpty(path))
				builder.Append("  - path: ").AppendLine(path);
		}
	}
}
