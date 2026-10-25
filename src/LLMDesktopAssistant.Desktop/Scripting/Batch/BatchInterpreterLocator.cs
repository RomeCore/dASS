using System.IO;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Desktop.Scripting.Batch
{
	/// <summary>
	/// Finds the command processor that runs a batch script.
	/// </summary>
	public interface IBatchInterpreterLocator
	{
		/// <summary>
		/// Resolves <c>cmd.exe</c>.
		/// </summary>
		/// <returns>The command processor or the reason there is none.</returns>
		ExecutableResolution Resolve();
	}

	/// <summary>
	/// The command processor of a batch script: <c>COMSPEC</c>, then the system copy of <c>cmd.exe</c>.
	/// </summary>
	/// <remarks>
	/// There is no chat setting here on purpose: a machine has exactly one command processor and nothing about it is
	/// worth choosing, unlike bash (Git Bash against the WSL stub) or PowerShell (7 against 5.1). Batch is the only
	/// language of the family that exists on one platform, so off Windows the answer is a problem rather than a path.
	/// </remarks>
	[Service(typeof(IBatchInterpreterLocator))]
	public sealed class BatchInterpreterLocator : IBatchInterpreterLocator
	{
		private readonly Lazy<ExecutableResolution> _resolved = new(Discover);

		/// <inheritdoc/>
		public ExecutableResolution Resolve() => _resolved.Value;

		private static ExecutableResolution Discover()
		{
			if (!OperatingSystem.IsWindows())
			{
				return new ExecutableResolution(null,
					"Batch scripts require Windows: cmd.exe does not exist on this platform.");
			}

			var comspec = Environment.GetEnvironmentVariable("COMSPEC");
			if (!string.IsNullOrWhiteSpace(comspec) && File.Exists(comspec))
				return new ExecutableResolution(comspec, null);

			var system = Path.Combine(Environment.SystemDirectory, "cmd.exe");
			return File.Exists(system)
				? new ExecutableResolution(system, null)
				: new ExecutableResolution(null,
					"cmd.exe was neither named by COMSPEC nor found in the system directory.");
		}
	}
}
