using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Prompting.ContextExpanders;
using LLMDesktopAssistant.Prompting.Management;
using LLMDesktopAssistant.Prompting.Plugins;
using LLMDesktopAssistant.StructuredValues.Converters;
using LiteDB;
using LLTSharp;

namespace LLMDesktopAssistant.Prompting.Context.Providers.SystemSlot
{
	/// <summary>
	/// Captures the system slot state: the system prompt text (custom or a system slot element)
	/// and the prompt components.
	/// </summary>
	[ChatService(typeof(IPromptSectionStateProvider<SystemSlotSectionState>))]
	public class SystemSlotStateProvider(
		ITemplateLibraryAccessor templates,
		IChatSettingsService chatSettings,
		IPromptSlotElementManager slotElementManager,
		IPromptComponentManager componentManager,
		IEnumerable<IPromptSystemContextExpander> promptSystemContextExpanders,
		IEnumerable<IPromptTemplatePlugin> promptTemplatePlugins
		) : IPromptSectionStateProvider<SystemSlotSectionState>
	{
		/// <inheritdoc/>
		public SystemSlotSectionState CaptureState(ChatAgentDescriptor agent)
		{
			var template = templates.GetTextTemplate("system_slot_system_section");
			var functions = new TemplateFunctionSet(promptTemplatePlugins.SelectMany(p => p.GetTemplateFunctions()));
			var promptSettings = agent.Prompts;

			var generalContext = new Dictionary<string, object?>();
			foreach (var expander in promptSystemContextExpanders)
				expander.ExpandPromptContext(generalContext);
			var partsContext = generalContext.ToDictionary();

			var effectiveSystemPrompt = promptSettings.GetEffectiveSystemPrompt(chatSettings.Settings);
			var effectiveComponents = promptSettings.GetEffectivePromptComponents(chatSettings.Settings);

			string? RenderPromptPart<K, V>(IPromptPartManager<K, V> manager, PromptPartSelection selection, K key)
				where K : notnull
				where V : PromptPartBase
			{
				var part = manager.TryGet(key);
				if (part is null)
					return null;
				if (part.ParameterSchema is not null)
				{
					selection.Parameters = part.ParameterSchema.Root.CreateOrFixValue(selection.Parameters, []);
					partsContext["params"] = LLTStructuredConverter.ToTemplateDataAccessor(selection.Parameters);
				}
				var result = part.EffectiveTemplate.Render(partsContext, functions).ToString();
				partsContext.Remove("params");
				return result;
			}

			generalContext["prompt"] =
				effectiveSystemPrompt.UseCustomSystemPrompt ? effectiveSystemPrompt.CustomSystemPrompt :
				RenderPromptPart(slotElementManager, effectiveSystemPrompt, (effectiveSystemPrompt.Id, PromptSlotKind.System));
			generalContext["components"] = effectiveComponents
				.Select(c => RenderPromptPart(componentManager, c, c.Id))
				.Where(c => !string.IsNullOrWhiteSpace(c))
				.ToArray();

			return new SystemSlotSectionState
			{
				Text = template.Render(generalContext, functions)
			};
		}
	}
}
