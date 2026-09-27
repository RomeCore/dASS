using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Prompting.ContextExpanders;
using LLMDesktopAssistant.Prompting.Management;
using LLMDesktopAssistant.Prompting.Plugins;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLTSharp;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Identity
{
	/// <summary>
	/// Captures the core prompt state by rendering the section template
	/// (the old system_prompt.llt monolith as its own render).
	/// </summary>
	[ChatService(typeof(IPromptSectionStateProvider<IdentitySectionState>))]
	public class IdentityStateProvider(
		IChatSettingsService chatSettings,
		IPromptSlotElementManager slotElementManager,
		IEnumerable<IPromptSystemContextExpander> promptSystemContextExpanders,
		IEnumerable<IPromptTemplatePlugin> promptTemplatePlugins
		) : IPromptSectionStateProvider<IdentitySectionState>
	{
		/// <inheritdoc/>
		public IdentitySectionState CaptureState(ChatAgentDescriptor agent)
		{
			var functions = new TemplateFunctionSet(promptTemplatePlugins.SelectMany(p => p.GetTemplateFunctions()));
			var promptSettings = agent.Prompts;

			var generalContext = new Dictionary<string, object?>();
			foreach (var expander in promptSystemContextExpanders)
				expander.ExpandPromptContext(generalContext);

			var effectivePersona = promptSettings.GetEffectivePersona(chatSettings.Settings);
			var effectiveSpecialization = promptSettings.GetEffectiveSpecialization(chatSettings.Settings);

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
					generalContext["params"] = LLTStructuredConverter.ToTemplateDataAccessor(selection.Parameters);
				}
				var result = part.EffectiveTemplate.Render(generalContext, functions).ToString();
				generalContext.Remove("params");
				return result;
			}

			return new IdentitySectionState
			{
				Persona = effectivePersona.UseCustomPersona ? effectivePersona.CustomPersona :
					RenderPromptPart(slotElementManager, effectivePersona, (effectivePersona.Id, PromptSlotKind.Persona)),
				Specialization = effectiveSpecialization.UseCustomSpecialization ? effectiveSpecialization.CustomSpecialization :
					RenderPromptPart(slotElementManager, effectiveSpecialization, (effectiveSpecialization.Id, PromptSlotKind.Specialization)),
				AssistantNickname = effectivePersona.Nickname
			};
		}
	}
}
