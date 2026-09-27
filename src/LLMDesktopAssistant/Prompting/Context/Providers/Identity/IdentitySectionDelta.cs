namespace LLMDesktopAssistant.Prompting.Context.Providers.Identity
{
	public class IdentitySectionDelta : PromptSectionDeltaBase
	{
		public bool PersonaChanged { get; init; }
		public string? NewPersona { get; init; }

		public bool SpecializationChanged { get; init; }
		public string? NewSpecialization { get; init; }

		public bool AssistantNicknameChanged { get; init; }
		public string? NewAssistantNickname { get; init; }
	}
}
