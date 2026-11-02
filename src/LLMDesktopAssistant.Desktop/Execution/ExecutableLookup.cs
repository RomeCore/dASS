using System.IO;

namespace LLMDesktopAssistant.Desktop.Execution
{
	/// <summary>
	/// Shared discovery for the interpreters a chat can run scripts with.
	/// </summary>
	public static class ExecutableLookup
	{
		/// <summary>
		/// Resolves the full path of a executable by searching PATH (and PATHEXT on Windows).
		/// </summary>
		/// <param name="fileName">The file name to look for, with or without the extension the platform needs.</param>
		/// <returns>The full path of the first match, or <see langword="null"/>.</returns>
		public static string? FindOnPath(string fileName)
		{
			if (Path.IsPathFullyQualified(fileName))
				return File.Exists(fileName) ? fileName : null;

			if (fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar))
			{
				var fullPath = Path.GetFullPath(fileName);
				return File.Exists(fullPath) ? fullPath : null;
			}

			var pathEnv = Environment.GetEnvironmentVariable("PATH");
			if (string.IsNullOrEmpty(pathEnv))
				return null;

			var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
			var extensions = Extensions();

			foreach (var entry in paths)
			{
				// A quoted entry is legal and appears in the wild; the quotes are not part of the directory.
				var directory = entry.Trim().Trim('"');
				if (directory.Length == 0)
					continue;

				foreach (var extension in extensions)
				{
					var candidate = Path.Combine(directory, fileName + extension);
					if (File.Exists(candidate))
						return candidate;
				}
			}

			return null;
		}

		/// <summary>
		/// The suffixes to try on <c>PATH</c>: the bare name first, then every <c>PATHEXT</c> extension on Windows.
		/// </summary>
		/// <remarks>
		/// The bare name has to come first: callers already pass fully spelled names (<c>bash.exe</c>, <c>git.exe</c>,
		/// <c>pwsh.exe</c>, or whatever a Lua script asked for), and appending an extension to those asks for
		/// <c>bash.exe.EXE</c>, which never exists - the executable is then reported as missing while it sits on PATH.
		/// </remarks>
		private static string[] Extensions()
		{
			if (!OperatingSystem.IsWindows())
				return [string.Empty];

			var pathext = Environment.GetEnvironmentVariable("PATHEXT");
			if (string.IsNullOrEmpty(pathext))
				return [string.Empty];

			var extensions = pathext.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			return [string.Empty, ..extensions];
		}
	}
}
