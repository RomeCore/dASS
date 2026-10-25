using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;
using Serilog;

namespace LLMDesktopAssistant.Desktop.Services
{
	/// <summary>
	/// Desktop implementation of <see cref="IExplorerOpener"/> that reveals files and
	/// opens directories in the system file explorer via <see cref="Process"/>.
	/// </summary>
	[Service(typeof(IExplorerOpener))]
	public class DesktopExplorerOpener : IExplorerOpener
	{
		/// <inheritdoc/>
		public bool OpenPath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return false;

			if (File.Exists(path))
				return ShowFileInExplorer(path);

			if (Directory.Exists(path))
				return OpenDirectory(path);

			// The path doesn't exist - fall back to opening its parent directory.
			var parent = Path.GetDirectoryName(path);
			return !string.IsNullOrWhiteSpace(parent) && OpenDirectory(parent);
		}

		/// <inheritdoc/>
		public bool OpenDirectory(string directoryPath)
		{
			if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
				return false;

			return Launch(new ProcessStartInfo
			{
				FileName = directoryPath,
				UseShellExecute = true
			});
		}

		/// <inheritdoc/>
		public bool ShowFileInExplorer(string filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
				return false;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				return Launch(new ProcessStartInfo
				{
					FileName = "explorer.exe",
					Arguments = $"/select,\"{filePath}\"",
					UseShellExecute = true
				});
			}

			// On other platforms, revealing a file is not supported - open the parent directory instead.
			var parent = Path.GetDirectoryName(filePath);
			return !string.IsNullOrWhiteSpace(parent) && OpenDirectory(parent);
		}

		private static bool Launch(ProcessStartInfo startInfo)
		{
			try
			{
				Process.Start(startInfo);
				return true;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to open path in file explorer: {Path}", startInfo.FileName);
				return false;
			}
		}
	}
}
