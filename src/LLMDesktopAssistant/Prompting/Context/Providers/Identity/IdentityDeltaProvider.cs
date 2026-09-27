using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting.ContextExpanders;
using LLMDesktopAssistant.Prompting.Management;
using LLMDesktopAssistant.Prompting.Plugins;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Identity
{
	/// <summary>
	/// Delta provider of the core prompt section (stub: no deltas yet).
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaProvider<IdentitySectionState, IdentitySectionDelta>))]
	public class IdentityDeltaProvider(
		IPromptSectionStateProvider<IdentitySectionState> stateProvider
	) : IPromptSectionDeltaProvider<IdentitySectionState, IdentitySectionDelta>
	{
		/// <inheritdoc/>
		public IdentitySectionDelta? CalculateDelta(IdentitySectionState? anchorState,
			IEnumerable<IdentitySectionDelta> existingDeltas, EffectiveChatContext context)
		{
			string? currentPersona = anchorState?.Persona;
			string? currentSpecialization = anchorState?.Specialization;
			string? currentNickname = anchorState?.AssistantNickname;

			foreach (var delta in existingDeltas)
			{
				if (delta.PersonaChanged)
					currentPersona = delta.NewPersona;
				if (delta.SpecializationChanged)
					currentSpecialization = delta.NewSpecialization;
				if (delta.AssistantNicknameChanged)
					currentNickname = delta.NewAssistantNickname;
			}

			var currentState = stateProvider.CaptureState(context.Agent);

			bool personaChanged = currentState?.Persona != currentPersona;
			bool specializationChanged = currentState?.Specialization != currentSpecialization;
			bool nicknameChanged = currentState?.AssistantNickname != currentNickname;

			if (personaChanged || specializationChanged || nicknameChanged)
				return new IdentitySectionDelta
				{
					PersonaChanged = personaChanged,
					NewPersona = currentState?.Persona,
					SpecializationChanged = specializationChanged,
					NewSpecialization = currentState?.Specialization,
					AssistantNicknameChanged = nicknameChanged,
					NewAssistantNickname = currentState?.AssistantNickname
				};

			return null;
		}
	}
}
