using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Prompting.Context.Providers.WorkingDirectories
{
	/// <summary>
	/// Captures the working directories section state from the chat environment settings:
	/// the enabled working directories plus the active one.
	/// </summary>
	[ChatService(typeof(IPromptSectionStateProvider<WorkingDirectoriesSectionState>))]
	public class WorkingDirectoriesStateProvider(
		IChatSettingsService chatSettings
	) : IPromptSectionStateProvider<WorkingDirectoriesSectionState>
	{
		/// <inheritdoc/>
		public WorkingDirectoriesSectionState CaptureState(ChatAgentDescriptor agent)
		{
			var settings = chatSettings.Settings.Environment.GetEffectiveWorkingDirectories();
			var activePath = settings.GetWorkingDirectory();
			var items = new List<WorkingDirectoryItem>();

			// The default directory is listed when it is enabled, and also when it is the active one
			// (an active directory must always be known to the agent).
			if (settings.IsDefaultWorkingDirectoryEnabled || settings.IsDefaultWorkingDirectoryActive)
			{
				items.Add(new WorkingDirectoryItem
				{
					Name = Directories.DefaultWorkingDirectoryName,
					Path = Directories.DefaultWorkingDirectory,
					IsDefault = true,
					IsActive = settings.IsDefaultWorkingDirectoryActive
				});
			}

			foreach (var directory in settings.Items)
			{
				if (!directory.IsEnabled || string.IsNullOrWhiteSpace(directory.Path))
					continue;

				items.Add(new WorkingDirectoryItem
				{
					Name = directory.Name,
					Path = directory.Path,
					IsActive = !settings.IsDefaultWorkingDirectoryActive && directory.IsActive
				});
			}

			// The active directory is never hidden, even when it is not a part of the list above.
			if (!items.Any(item => item.IsActive) && !string.IsNullOrWhiteSpace(activePath))
			{
				var known = items.FirstOrDefault(item => IsSamePath(item.Path, activePath));
				if (known is not null)
					known.IsActive = true;
				else
					items.Add(new WorkingDirectoryItem
					{
						Path = activePath,
						IsActive = true,
						IsDefault = IsSamePath(activePath, Directories.DefaultWorkingDirectory)
					});
			}

			return new WorkingDirectoriesSectionState
			{
				Items = items
			};
		}

		/// <summary>
		/// Compares two paths the same way the working directory access service does.
		/// </summary>
		internal static bool IsSamePath(string? left, string? right)
			=> string.Equals(left, right, OperatingSystem.IsWindows()
				? StringComparison.OrdinalIgnoreCase
				: StringComparison.Ordinal);
	}
}
