using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Prompting.Context.Providers.WorkingDirectories
{
	/// <summary>
	/// Delta renderer of the working directories section: every reported concern becomes
	/// its own self-describing block of the 'working_directories_system_section_delta' template.
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaRenderer<WorkingDirectoriesSectionDelta>))]
	public class WorkingDirectoriesDeltaRenderer(
		ITemplateLibraryAccessor templates
	) : IPromptSectionDeltaRenderer<WorkingDirectoriesSectionDelta>
	{
		/// <inheritdoc/>
		public string Render(WorkingDirectoriesSectionDelta delta)
		{
			// When the section appears, the whole list is announced: the field-level reports
			// of the same delta would only duplicate it.
			bool appeared = delta.Appeared && delta.Items.Count > 0;

			return templates.GetTextTemplate("working_directories_system_section_delta").Render(new
			{
				lines = appeared
					? delta.Items.Select(WorkingDirectoryFormatting.FormatLine).ToArray()
					: null,
				added = !appeared && delta.AddedItems.Count > 0
					? delta.AddedItems.Select(WorkingDirectoryFormatting.FormatLine).ToArray()
					: null,
				removed = !appeared && delta.RemovedPaths.Count > 0
					? delta.RemovedPaths.ToArray()
					: null,
				active_changed = !appeared && delta.ActiveChanged,
				old_active = delta.OldActivePath ?? "unknown",
				new_active = delta.NewActivePath
			});
		}
	}
}
