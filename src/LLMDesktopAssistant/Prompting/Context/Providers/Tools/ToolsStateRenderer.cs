using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Tools
{
	/// <summary>
	/// Renders the tools section state into a snapshot: the tool definitions go to the tool list,
	/// while the hidden tools are announced by name in the system prompt text
	/// (with a nudge to inspect them via `addon-info`).
	/// </summary>
	[ChatService(typeof(IPromptSectionStateRenderer<ToolsSectionState>))]
	public class ToolsStateRenderer(
		ITemplateLibraryAccessor templates
	) : IPromptSectionStateRenderer<ToolsSectionState>
	{
		/// <inheritdoc/>
		public SystemPromptSnapshot Render(ToolsSectionState state) => new()
		{
			Text = templates.GetTextTemplate("tools_system_section").Render(new
			{
				hidden_tools = state.HiddenNames.Count > 0 ? state.HiddenNames.ToArray() : null
			}),
			Tools = [.. state.Items.Select(ToSerializableToolDefinition)]
		};

		private static SerializableToolDefinition ToSerializableToolDefinition(ToolItem item) => new()
		{
			Name = item.Name,
			Description = item.Description ?? string.Empty,
			ArgumentSchema = item.ArgumentSchema
		};
	}
}
