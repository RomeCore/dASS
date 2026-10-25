using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Prompting.Skills;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// The service for accessing files inside current working directory.
	/// </summary>
	/// <param name="chatSettings">The settings service of the chat.</param>
	[ChatService(typeof(IWorkingDirectoryAccessService))]
	public class WorkingDirectoryAccessService(
		IChatSettingsService chatSettings,
		IAddonAccessor<SkillInfo> skillAddons
	) : IWorkingDirectoryAccessService
	{
		public string GetWorkingDirectory()
		{
			return chatSettings.Settings.Environment.GetEffectiveWorkingDirectories().GetWorkingDirectory();
		}

		public string? TryAccessPath(string path, DirectoryAccessMode mode)
		{
			var fullPath = CheckedAccessPath(path, mode, out bool isAccessed);
			if (!isAccessed)
				return null;
			return fullPath;
		}

		public string AccessPath(string path, DirectoryAccessMode mode)
		{
			var fullPath = CheckedAccessPath(path, mode, out bool isAccessed);
			if (!isAccessed)
				throw new UnauthorizedAccessException("The path cannot be accessed because of access restrictions.");
			return fullPath;
		}

		public string CheckedAccessPath(string path, DirectoryAccessMode mode, out bool isAccessed)
		{
			var baseDir = Path.GetFullPath(chatSettings.Settings.Environment.GetEffectiveWorkingDirectories().GetWorkingDirectory());
			var fullPath = string.IsNullOrEmpty(path) ? baseDir : Path.GetFullPath(Path.Combine(baseDir, path));

			isAccessed = false;

			// Any of working directories are allowed to access.
			foreach (var wd in chatSettings.Settings.Environment.GetEffectiveWorkingDirectories().Items)
			{
				if (wd.IsEnabled && !string.IsNullOrEmpty(wd.Path) && IsSubdirectoryOf(wd.Path, fullPath))
				{
					isAccessed = true;
					break;
				}
			}

			// Skill folders are allowed to access.
			if (!isAccessed)
				foreach (var skill in skillAddons.Addons)
				{
					var skillDir = skill.HomeDirectory ?? (skill.Path != null ? Path.GetDirectoryName(skill.Path) : null);
					if (!string.IsNullOrEmpty(skillDir) && IsSubdirectoryOf(skillDir, fullPath))
					{
						isAccessed = true;
						break;
					}
				}

			// Order rules by path length (more common rules goes first),
			// then calculate by access mode with overrides.
			foreach (var access in chatSettings.Settings.Environment.GetEffectiveDirectoryAccessRules().OrderBy(a => a.Path?.Length ?? 0))
			{
				if (!access.IsEnabled || string.IsNullOrEmpty(access.Path) || !IsSubdirectoryOf(access.Path, fullPath))
					continue;

				if (access.AccessMode == DirectoryAccessMode.None)
				{
					// Deny access but wait for next rules that might allow it.
					isAccessed = false;
					continue;
				}

				isAccessed = (access.AccessMode & mode) != 0;
			}

			return fullPath;
		}

		private static readonly StringComparison _pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

		private static bool IsSubdirectoryOf(string baseDir, string fullPath)
		{
			// Normalize both paths to absolute, canonical form.
			// This resolves relative segments (., ..) and standardizes separators.
			var normalizedBase = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			var normalizedFull = Path.GetFullPath(fullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

			// Exact match - accessing the root itself.
			if (string.Equals(normalizedBase, normalizedFull, _pathComparison))
				return true;

			// Check that fullPath starts with baseDir followed by a separator.
			// This prevents partial-name collisions like "C:\Projects" matching "C:\ProjectsSomething".
			return normalizedFull.StartsWith(
				normalizedBase + Path.DirectorySeparatorChar,
				_pathComparison);
		}
	}
}
