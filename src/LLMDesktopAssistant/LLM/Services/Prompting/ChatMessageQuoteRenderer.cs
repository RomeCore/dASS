using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Prompting.ContextExpanders;
using LLMDesktopAssistant.Prompting.Plugins;
using LLMDesktopAssistant.Users;
using LLMDesktopAssistant.Utils.Files;
using LLTSharp;
using RCLargeLanguageModels.Messages.Attachments;

namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	/// <inheritdoc cref="IChatMessageQuoteRenderer"/>
	[ChatService(typeof(IChatMessageQuoteRenderer))]
	public class ChatMessageQuoteRenderer(
		ITemplateLibraryAccessor templates,
		IUserManagementService userManager,
		IAgentManagementService agentManager,
		IEnumerable<IPromptSystemContextExpander> promptSystemContextExpanders,
		IEnumerable<IPromptMessageContextExpander> promptMessageContextExpanders,
		IEnumerable<IPromptTemplatePlugin> promptTemplatePlugins
		) : IChatMessageQuoteRenderer
	{
		private const int briefReasoningCharacters = 250;
		private const int briefTcArgumentsCharacters = 200;
		private const int briefTcResultCharacters = 250;

		/// <inheritdoc/>
		public MessageRenderingResult Render(BranchedMessage branchedMessage,
			MessagePartsFacet parts = MessagePartsFacet.Default,
			MessageAuthorIdentity identity = MessageAuthorIdentity.Default,
			ContextCheckpointKind appliedCheckpoints = ContextCheckpointKind.None)
		{
			var message = branchedMessage.Message;
			var functions = new TemplateFunctionSet(promptTemplatePlugins.SelectMany(p => p.GetTemplateFunctions()));
			string name;
			bool isUserLike;

			switch (message)
			{
				case RawUserMessage rawUserMessage:
					return new MessageRenderingResult(rawUserMessage.Content, NativeAttachments: []);

				case UserMessage userMessage:
					name = userManager.FindByLogin(userMessage.SenderLogin)?.GetAgentShownName() ?? userMessage.SenderLogin;
					isUserLike = true;
					break;

				case AssistantMessage assistantMessage:
					var senderDescriptor = agentManager.GetAgentDescriptor(assistantMessage.SenderAgentId);
					name = senderDescriptor.Info.Name ?? senderDescriptor.Id.ToString()[..8];
					isUserLike = assistantMessage.IsUserLike;
					break;

				default:
					throw new InvalidOperationException($"Unsupported message type: {message.GetType()}.");
			}

			var template = templates.GetTextTemplate("message_quote_prompt");

			var context = new Dictionary<string, object?>();
			foreach (var expander in promptSystemContextExpanders)
				expander.ExpandPromptContext(context);
			foreach (var expander in promptMessageContextExpanders)
				expander.ExpandPromptContext(branchedMessage, null, context);

			context["author_identity"] = identity switch
			{
				MessageAuthorIdentity.Anon => "anon",
				MessageAuthorIdentity.UnnamedUser => "anon-user",
				MessageAuthorIdentity.UnnamedAgent => "anon-agent",
				MessageAuthorIdentity.NamedUser => "user",
				MessageAuthorIdentity.NamedAgent => "agent",
				_ => isUserLike ? "user" : "agent"
			};
			context["author_name"] = name;
			context["time_sent"] = FormatSentTime(message.CreatedAt);

			bool reasoningVisible = !appliedCheckpoints.HasFlag(ContextCheckpointKind.ReasoningCompaction)
				&& (parts.HasFlag(MessagePartsFacet.BriefReasoning) || parts.HasFlag(MessagePartsFacet.Reasoning));
			string? reasoningContent = (message as AssistantMessage)?.ReasoningContent;
			context["reasoning_content"] = reasoningVisible
				? (parts.HasFlag(MessagePartsFacet.Reasoning)
					? reasoningContent
					: reasoningContent?[..Math.Min(reasoningContent.Length, briefReasoningCharacters)])
				: null;

			context["content"] = parts.HasFlag(MessagePartsFacet.Content) ? message.Content : null;

			List<IAttachment> nativeAttachments = [.. message.GetNativeAttachments()];
			context["attachments"] = parts.HasFlag(MessagePartsFacet.Attachments)
				? message.AdditionalData.OfType<AttachmentMessagePart>().Select(ConvertAttachment).ToArray()
				: null;

			bool showToolCallFacts = parts.HasFlag(MessagePartsFacet.ToolCallFacts);
			bool showFullToolCallArguments = parts.HasFlag(MessagePartsFacet.ToolCallArguments);
			bool showBriefToolCallArguments = parts.HasFlag(MessagePartsFacet.BriefToolCallArguments);
			bool showFullToolCallResults = parts.HasFlag(MessagePartsFacet.ToolCallResults);
			bool showBriefToolCallResults = parts.HasFlag(MessagePartsFacet.BriefToolCallResults);
			bool showToolCallNativeAttachments = parts.HasFlag(MessagePartsFacet.ToolCallNativeAttachments);

			// Tool calls are listed when their facts are requested, or when any part of them is requested.
			bool showToolCalls = showToolCallFacts
				|| showFullToolCallArguments || showBriefToolCallArguments
				|| showFullToolCallResults || showBriefToolCallResults
				|| showToolCallNativeAttachments;

			bool forcedToolCompaction = appliedCheckpoints.HasFlag(ContextCheckpointKind.ForcedToolCompaction);
			bool toolCompactionAllowed = forcedToolCompaction || appliedCheckpoints.HasFlag(ContextCheckpointKind.ToolCompaction);

			context["tool_calls"] = showToolCalls ? message.ToolCalls.Select(tc =>
			{
				string? arguments = showFullToolCallArguments
					? tc.Arguments
					: showBriefToolCallArguments ? Brief(tc.Arguments, briefTcArgumentsCharacters) : null;

				// Compaction only ever replaces a result that would have been shown otherwise.
				string? resultContent = null;
				if (showFullToolCallResults || showBriefToolCallResults)
				{
					bool toolCompacted = forcedToolCompaction || (tc.CanBeCompacted && toolCompactionAllowed);
					resultContent = toolCompacted
						? GetCompactedToolResultContent(tc.Status)
						: showFullToolCallResults ? tc.ResultContent : Brief(tc.ResultContent, briefTcResultCharacters);
				}

				if (showToolCallNativeAttachments)
					nativeAttachments.AddRange(tc.GetNativeAttachments());

				return new
				{
					name = tc.ToolName,
					arguments,
					result_content = resultContent,
				};
			}).ToArray() : null;

			return new MessageRenderingResult(template.Render(context, functions),
				nativeAttachments);
		}

		private static string? Brief(string? value, int maxCharacters)
			=> value is { Length: > 0 } text && text.Length > maxCharacters
				? text[..maxCharacters]
				: value;

		private static string GetCompactedToolResultContent(ToolStatus status) => status switch
		{
			ToolStatus.Success => "[TOOL RESULT WAS COMPACTED, SUCCESSFUL BEFORE]",
			ToolStatus.Error => "[TOOL RESULT WAS COMPACTED, FAULTED BEFORE]",
			ToolStatus.Cancelled => "[TOOL RESULT WAS COMPACTED, CANCELLED BEFORE]",
			_ => "[TOOL RESULT WAS COMPACTED]"
		};

		private static string FormatSentTime(DateTime time)
		{
			return $"{time:yyyy-MM-dd HH:mm:ss}";
		}

		private static object ConvertAttachment(AttachmentMessagePart attachment)
		{
			return new
			{
				local_path = attachment.LocalPath,
				display_size = FileUtils.BytesToDisplaySize(attachment.Size)
			};
		}
	}
}
