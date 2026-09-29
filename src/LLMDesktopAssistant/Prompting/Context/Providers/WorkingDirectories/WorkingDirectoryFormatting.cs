using System.Text;

namespace LLMDesktopAssistant.Prompting.Context.Providers.WorkingDirectories
{
	/// <summary>
	/// The single place where a working directory is turned into a prompt line,
	/// shared by the state renderer and the delta renderer.
	/// </summary>
	internal static class WorkingDirectoryFormatting
	{
		/// <summary>
		/// Formats a directory the way the working directories list is rendered:
		/// an optional name, the path and the active/default marks.
		/// </summary>
		public static string FormatLine(WorkingDirectoryItem item)
		{
			var builder = new StringBuilder();

			if (!string.IsNullOrWhiteSpace(item.Name))
				builder.Append('*').Append(item.Name).Append("*: ");

			builder.Append('`').Append(item.Path).Append('`');

			if (item.IsActive)
				builder.Append(" **(ACTIVE)**");

			if (item.IsDefault)
				builder.Append(" (default)");

			return builder.ToString();
		}
	}
}
