using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Tools
{
	/// <summary>
	/// Delta provider of the tools section: the common addon delta engine plus the tool
	/// field-level comparison (description and argument schema).
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaProvider<ToolsSectionState, ToolsSectionDelta>))]
	public class ToolsDeltaProvider(
		IPromptSectionStateProvider<ToolsSectionState> stateProvider
	) : AddonSectionDeltaProvider<ToolsSectionState, ToolItem, ToolChange, ToolsSectionDelta>(stateProvider)
	{
		/// <inheritdoc/>
		protected override ToolChange? Diff(ToolItem known, ToolItem current)
		{
			bool descriptionChanged = known.Description != current.Description;
			bool argumentSchemaChanged = known.ArgumentSchema != current.ArgumentSchema;
			if (!descriptionChanged && !argumentSchemaChanged)
				return null;

			return new ToolChange
			{
				DescriptionChanged = descriptionChanged,
				NewDescription = descriptionChanged ? current.Description : null,
				ArgumentSchemaChanged = argumentSchemaChanged,
				NewArgumentSchema = argumentSchemaChanged ? current.ArgumentSchema : null
			};
		}
	}
}
