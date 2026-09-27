using LLMDesktopAssistant.LLM.MVVM.Additional;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	/// <summary>
	/// Defines the facet of message parts that can be included in a prompt.
	/// </summary>
	[Flags]
	public enum MessagePartsFacet
	{
		None = 0,

		Default = BriefReasoning | Content | Attachments | BriefToolCallArguments | BriefToolCallResults,

		All = BriefReasoning | Reasoning | Content |
			NativeAttachments | Attachments |
			ToolCallFacts | BriefToolCallArguments | BriefToolCallResults | ToolCallArguments | ToolCallResults,

		/// <summary>
		/// Only N first characters of the reasoning are shown. Not requires <see cref="Reasoning"/> to be included.
		/// </summary>
		BriefReasoning = 1 << 0,

		/// <summary>
		/// The entire reasoning contents are shown, this implies <see cref="BriefReasoning"/>.
		/// </summary>
		Reasoning = 1 << 1,

		/// <summary>
		/// The main content of the message is shown, including UI-collapsed message parts (see <see cref="AdditionalMessagePart"/>).
		/// </summary>
		Content = 1 << 2,

		/// <summary>
		/// The native attachments are shown (see <see cref="NativeAttachmentMessagePart"/>).
		/// </summary>
		NativeAttachments = 1 << 3,

		/// <summary>
		/// The non-native prompt injection-based attachments are shown (file link-based, for example, see <see cref="AttachmentMessagePart"/>).
		/// </summary>
		Attachments = 1 << 4,

		/// <summary>
		/// Only the tool call names are shown, not the tool call arguments and results.
		/// </summary>
		ToolCallFacts = 1 << 5,

		/// <summary>
		/// Only the N first characters of the tool call arguments are shown. Not requires <see cref="ToolCallFacts"/> to be included.
		/// </summary>
		BriefToolCallArguments = 1 << 6,

		/// <summary>
		/// Only the N first characters of the tool call results are shown. Not requires <see cref="ToolCallFacts"/> to be included.
		/// </summary>
		BriefToolCallResults = 1 << 7,

		/// <summary>
		/// The entire tool call arguments are shown, this implies <see cref="ToolCallFacts"/>.
		/// </summary>
		ToolCallArguments = 1 << 8,

		/// <summary>
		/// The entire tool call results are shown, this implies <see cref="ToolCallFacts"/>.
		/// </summary>
		ToolCallResults = 1 << 9,

		/// <summary>
		/// The native attachments of the tool call are shown.
		/// </summary>
		ToolCallNativeAttachments = 1 << 10,
	}
}
