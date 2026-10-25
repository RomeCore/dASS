using System.Collections.Concurrent;
using System.IO;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Desktop.Scripting.Bash
{
	/// <summary>
	/// The outcome of looking for a bash interpreter.
	/// </summary>
	/// <param name="Path">The executable to run, or <see langword="null"/> when nothing usable was found.</param>
	/// <param name="Problem">Why nothing was found, or <see langword="null"/> on success.</param>
	public readonly record struct BashInterpreter(string? Path, string? Problem)
	{
		/// <summary>
		/// Whether an executable was found.
		/// </summary>
		public bool Found => Path is not null;
	}

	/// <summary>
	/// Finds the bash this machine can actually run a script with.
	/// </summary>
	public interface IBashInterpreterLocator
	{
		/// <summary>
		/// Resolves the interpreter, letting the configured path win over discovery.
		/// </summary>
		/// <param name="configuredPath">The chat's bash path, or <see langword="null"/>/empty to discover one.</param>
		/// <returns>The interpreter or the reason there is none.</returns>
		BashInterpreter Resolve(string? configuredPath);
	}

	/// <summary>
	/// The documented discovery order: the configured path, Git Bash derived from <c>git.exe</c>, then a
	/// <c>bash</c> from <c>PATH</c> unless it is the WSL stub.
	/// </summary>
	/// <remarks>
	/// The naive "bash from PATH" lookup is a trap on Windows: it finds <c>C:\Windows\System32\bash.exe</c>, which is
	/// WSL and cannot see <c>C:\...</c> paths. Git Bash is not looked up by hard-coded location either - git may live
	/// on any drive - so it is derived from wherever <c>git</c> actually is. Results are cached per configured path.
	/// </remarks>
	[Service(typeof(IBashInterpreterLocator))]
	public sealed class BashInterpreterLocator : IBashInterpreterLocator
	{
		private const string bashName = "bash";
		private const string bashExecutableName = "bash.exe";

		private readonly ConcurrentDictionary<string, BashInterpreter> _cache = [];

		/// <inheritdoc/>
		public BashInterpreter Resolve(string? configuredPath)
			=> _cache.GetOrAdd(configuredPath?.Trim() ?? string.Empty, Discover);

		private static BashInterpreter Discover(string configuredPath)
		{
			if (configuredPath.Length > 0)
			{
				return File.Exists(configuredPath)
					? new BashInterpreter(configuredPath, null)
					: new BashInterpreter(null,
						$"The configured bash executable '{configuredPath}' does not exist.");
			}

			foreach (var candidate in FromGit())
			{
				if (File.Exists(candidate))
					return new BashInterpreter(candidate, null);
			}

			if (FindOnPath(OperatingSystem.IsWindows() ? bashExecutableName : bashName) is { } onPath)
			{
				if (IsWslStub(onPath))
				{
					return new BashInterpreter(null,
						$"The 'bash' on PATH is the WSL stub ({onPath}). WSL is not supported: install Git for Windows " +
						"or set the bash executable in the chat environment.");
				}

				return new BashInterpreter(onPath, null);
			}

			return new BashInterpreter(null,
				"No bash interpreter was found on this machine. Install Git for Windows or set the bash executable " +
				"in the chat environment.");
		}

		/// <summary>
		/// Git Bash candidates, derived from the git that is actually on this machine.
		/// </summary>
		private static IEnumerable<string> FromGit()
		{
			if (!OperatingSystem.IsWindows())
				yield break;

			if (FindOnPath("git.exe") is not { } git)
				yield break;

			var root = Path.GetDirectoryName(Path.GetDirectoryName(git));
			if (root is null)
				yield break;

			yield return Path.Combine(root, "bin", bashExecutableName);
			yield return Path.Combine(root, "usr", "bin", bashExecutableName);
		}

		/// <summary>
		/// Whether the path is the Windows WSL launcher rather than a bash that understands Windows paths.
		/// </summary>
		private static bool IsWslStub(string path)
		{
			if (!OperatingSystem.IsWindows())
				return false;

			var directory = Path.GetDirectoryName(Path.GetFullPath(path));
			if (directory is null)
				return false;

			var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
			return string.Equals(directory.TrimEnd(Path.DirectorySeparatorChar),
					system.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
				|| directory.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase);
		}

		private static string? FindOnPath(string fileName)
		{
			var path = Environment.GetEnvironmentVariable("PATH");
			if (string.IsNullOrWhiteSpace(path))
				return null;

			foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
			{
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
