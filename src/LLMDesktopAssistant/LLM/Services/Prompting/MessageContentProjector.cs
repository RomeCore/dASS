using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Execution;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	/// <summary>
	/// Builds the model-facing content of a message: the raw content shaped by the producer command's
	/// <see cref="ModelFacingMode"/> (a command may hide it or replace it with its bare token) plus the text of every
	/// <see cref="AdditionalMessageContentPart"/> a command wrote into the message.
	/// </summary>
	/// <remarks>
	/// Pure and side-effect free on purpose, so the projection can be tested without the whole rendering pipeline. The
	/// message renderer calls it for the <see cref="MessagePartsFacet.Content"/> slot.
	/// </remarks>
	public static class MessageContentProjector
	{
		/// <summary>
		/// Projects the message content for the model, or <see langword="null"/> when the content facet is not
		/// requested or nothing would be shown.
		/// </summary>
		public static string? Project(ChatMessage message, MessagePartsFacet parts)
		{
			if (!parts.HasFlag(MessagePartsFacet.Content))
				return null;

			message.AdditionalData.TryGet<SlashCommandFingerprint>(out var fingerprint);

			string? content = fingerprint?.ModelFacingMode switch
			{
				ModelFacingMode.Hidden => null,
				ModelFacingMode.Neutral => "/" + fingerprint!.RawToken,
				_ => message.Content
			};

			var segments = new List<string?> { content };
			segments.AddRange(message.AdditionalData.OfType<AdditionalMessageContentPart>().Select(part => part.Content));

			var combined = string.Join("\n\n", segments.Where(segment => !string.IsNullOrEmpty(segment)));
			return combined.Length == 0 ? null : combined;
		}
	}
}
