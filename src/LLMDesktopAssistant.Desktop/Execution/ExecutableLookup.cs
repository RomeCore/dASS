using System.IO;
using System.Runtime.InteropServices;

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
		/// <param name="fileName">The file name to look for, with the extension the platform needs.</param>
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

			var pathSeparator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
			var paths = pathEnv.Split(pathSeparator, StringSplitOptions.RemoveEmptyEntries);

			var extensions = new[] { string.Empty };
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				var pathext = Environment.GetEnvironmentVariable("PATHEXT");
				if (!string.IsNullOrEmpty(pathext))
					extensions = pathext.Split(';', StringSplitOptions.RemoveEmptyEntries);
			}

			foreach (var path in paths)
			{
				foreach (var ext in extensions)
				{
					var fullPath = Path.Combine(path.Trim(), fileName + ext);
					if (File.Exists(fullPath))
						return fullPath;
				}
			}

			return null;
		}
	}
}
