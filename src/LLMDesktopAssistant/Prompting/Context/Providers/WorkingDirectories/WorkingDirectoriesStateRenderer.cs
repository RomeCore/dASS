using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Prompting.Context.Providers.WorkingDirectories
{
	/// <summary>
	/// Renders the working directories section state into a snapshot (text only).
	/// </summary>
	[ChatService(typeof(IPromptSectionStateRenderer<WorkingDirectoriesSectionState>))]
	public class WorkingDirectoriesStateRenderer(
		ITemplateLibraryAccessor templates
	) : IPromptSectionStateRenderer<WorkingDirectoriesSectionState>
	{
		/// <inheritdoc/>
		public SystemPromptSnapshot Render(WorkingDirectoriesSectionState state)
		{
			var active = state.Items.FirstOrDefault(item => item.IsActive);

			return templates.GetTextTemplate("working_directories_system_section").Render(new
			{
				lines = state.Items.Count > 0
					? state.Items.Select(WorkingDirectoryFormatting.FormatLine).ToArray()
					: null,
				active_path = active?.Path,
				active_is_default = active?.IsDefault ?? false
			});
		}
	}
}
