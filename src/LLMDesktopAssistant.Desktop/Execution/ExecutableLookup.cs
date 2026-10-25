using System.IO;

namespace LLMDesktopAssistant.Desktop.Execution
{
	/// <summary>
	/// Shared discovery for the interpreters a chat can run scripts with.
	/// </summary>
	public static class ExecutableLookup
	{
		/// <summary>
		/// Finds an executable on <c>PATH</c>.
		/// </summary>
		/// <param name="fileName">The file name to look for, with the extension the platform needs.</param>
		/// <returns>The full path of the first match, or <see langword="null"/>.</returns>
		public static string? FindOnPath(string fileName)
		{
			var path = Environment.GetEnvironmentVariable("PATH");
			if (string.IsNullOrWhiteSpace(path))
				return null;

			foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
			{
				// A quoted entry is legal and appears in the wild; the quotes are not part of the directory.
				var directory = entry.Trim().Trim('"');
				if (directory.Length == 0)
					continue;

				var candidate = Path.Combine(directory, fileName);
				if (File.Exists(candidate))
					return candidate;
			}

			return null;
		}
	}
}
