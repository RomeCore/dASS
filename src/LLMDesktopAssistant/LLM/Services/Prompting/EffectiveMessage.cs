using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Prompting;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	public record EffectiveMessage(BranchedMessage BranchedMessage, MessagePartsFacet Facets,
		ContextCheckpointKind AffectedCheckpoints, MessageAuthorIdentity Identity);
}
