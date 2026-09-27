using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Identity
{
	/// <summary>
	/// Delta renderer of the core prompt section (stub: no deltas yet).
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaRenderer<IdentitySectionDelta>))]
	public class IdentityDeltaRenderer(
		ITemplateLibraryAccessor templates
	) : IPromptSectionDeltaRenderer<IdentitySectionDelta>
	{
		/// <inheritdoc/>
		public string Render(IdentitySectionDelta delta)
		{
			return templates.GetTextTemplate("identity_system_section_delta").Render(new
			{
				persona_changed = delta.PersonaChanged,
				persona = delta.NewPersona,
				specialization_changed = delta.SpecializationChanged,
				specialization = delta.NewSpecialization,
				assistant_nickname_changed = delta.AssistantNicknameChanged,
				assistant_nickname = delta.NewAssistantNickname
			});
		}
	}
}
