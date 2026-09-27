using RCLargeLanguageModels.Messages.Attachments;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	public record MessageRenderingResult(string Content, IReadOnlyList<IAttachment> NativeAttachments);
}
