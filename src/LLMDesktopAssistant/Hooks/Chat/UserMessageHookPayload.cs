using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.MVVM.Additional.Chat;
using LLMDesktopAssistant.Utils;
using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.Hooks.Chat
{
	/// <summary>
	/// The document every <c>message.user.*</c> hook receives.
	/// </summary>
	/// <remarks>
	/// One type for all four events on purpose: <c>hook-type: "message.user.*"</c> matches the whole family, so the
	/// shape must not change between a submit and an insert. A field that the moment does not know yet is
	/// <see langword="null"/> - <see cref="MessageId"/> and <see cref="Index"/> before the write, <see cref="Generate"/>
	/// after it - instead of being absent.
	/// </remarks>
	public sealed class UserMessageHookPayload
	{
		/// <summary>
		/// The chat the message belongs to, or <see langword="null"/> when the world has none.
		/// </summary>
		public int? ChatId { get; init; }

		/// <summary>
		/// The database id of the message, or <see langword="null"/> before it reaches the history.
		/// </summary>
		public int? MessageId { get; init; }

		/// <summary>
		/// The 1-based index of the message in the history, or <see langword="null"/> before it reaches the history.
		/// </summary>
		public int? Index { get; init; }

		/// <summary>
		/// The login of the sender.
		/// </summary>
		public string SenderId { get; init; } = string.Empty;

		/// <summary>
		/// The display name of the sender, or <see langword="null"/> when the login does not resolve.
		/// </summary>
		public string? Sender { get; init; }

		/// <summary>
		/// The text as the user typed it: the slash command token is still in it and a <c>//</c> escape is not undone
		/// yet.
		/// </summary>
		public string Content { get; init; } = string.Empty;

		/// <summary>
		/// The text the attached parts contribute to the model-facing content, or <see langword="null"/> when they
		/// contribute none.
		/// </summary>
		public string? AdditionalContent { get; init; }

		/// <summary>
		/// The attached parts, described by kind and title, or <see langword="null"/> when nothing is attached.
		/// </summary>
		public UserMessagePartInfo[]? Parts { get; init; }

		/// <summary>
		/// The visibility of the message in kebab case: <c>always</c>, <c>reveal-after-send</c>, <c>only-users</c> or
		/// <c>only-agents</c>.
		/// </summary>
		public string Visibility { get; init; } = string.Empty;

		/// <summary>
		/// The logins and agent ids the message is addressed to, or <see langword="null"/> when it is addressed to
		/// everyone.
		/// </summary>
		public string[]? VisibleTo { get; init; }

		/// <summary>
		/// Whether <see cref="VisibleTo"/> is a whitelist filter rather than a blacklist one.
		/// </summary>
		public bool IsWhitelist { get; init; }

		/// <summary>
		/// Whether the message commits an edit instead of appending a new one.
		/// </summary>
		public bool IsEdit { get; init; }

		/// <summary>
		/// Whether the send asked for an answer, or <see langword="null"/> when the moment does not know it.
		/// </summary>
		public bool? Generate { get; init; }

		/// <summary>
		/// The slash command the message carries, or <see langword="null"/> when it carries none.
		/// </summary>
		public UserMessageCommandInfo? Command { get; init; }

		/// <summary>
		/// Builds the document from whatever the caller already knows.
		/// </summary>
		/// <param name="chatId">The chat the message belongs to.</param>
		/// <param name="senderId">The login of the sender.</param>
		/// <param name="sender">The display name of the sender, when it resolves.</param>
		/// <param name="content">The message text as it stands.</param>
		/// <param name="parts">The parts attached to the message.</param>
		/// <param name="visibility">The visibility of the message.</param>
		/// <param name="visibleTo">The logins and agent ids the message is addressed to.</param>
		/// <param name="isWhitelist">Whether <paramref name="visibleTo"/> is a whitelist.</param>
		/// <param name="isEdit">Whether the message commits an edit.</param>
		/// <param name="generate">Whether the send asked for an answer, or <see langword="null"/> when unknown.</param>
		/// <param name="command">The slash command the message carries, when one was resolved.</param>
		/// <param name="messageId">The database id, once the message has one.</param>
		/// <param name="index">The 1-based history index, once the message has one.</param>
		public static UserMessageHookPayload Create(int? chatId, string senderId, string? sender, string content,
			IEnumerable<AdditionalChatData> parts, MessageVisibility visibility, IReadOnlyList<string> visibleTo,
			bool isWhitelist, bool isEdit, bool? generate, UserMessageCommandInfo? command,
			int? messageId = null, int? index = null)
		{
			var described = parts as AdditionalChatData[] ?? [.. parts];

			var additionals = described
				.OfType<IContentChatAttachment>()
				.Select(part => part.Content)
				.Where(text => !string.IsNullOrEmpty(text))
				.ToArray();

			return new UserMessageHookPayload
			{
				ChatId = chatId,
				MessageId = messageId,
				Index = index,
				SenderId = senderId,
				Sender = sender,
				Content = content,
				AdditionalContent = additionals.Length == 0 ? null : string.Join("\n\n", additionals),
				Parts = described.Length == 0 ? null : [.. described.Select(UserMessagePartInfo.Describe)],
				Visibility = KebabEnumNames<MessageVisibility>.Serialize(visibility) ?? string.Empty,
				VisibleTo = visibleTo.Count == 0 ? null : [.. visibleTo],
				IsWhitelist = isWhitelist,
				IsEdit = isEdit,
				Generate = generate,
				Command = command
			};
		}

		/// <summary>
		/// One attached part: what it is and what it is called.
		/// </summary>
		public sealed class UserMessagePartInfo
		{
			/// <summary>
			/// The kind of the payload in kebab case: <c>content</c>, <c>part</c>, <c>overlay</c>, <c>rail</c> or
			/// <c>data</c> for anything else.
			/// </summary>
			public string Kind { get; init; } = string.Empty;

			/// <summary>
			/// The label the part shows, or <see langword="null"/> when it has none.
			/// </summary>
			public string? Title { get; init; }

			/// <summary>
			/// Describes a part without exposing its payload: a script needs to know that an image or a control is
			/// attached, not to receive it.
			/// </summary>
			/// <param name="part">The attached part.</param>
			public static UserMessagePartInfo Describe(AdditionalChatData part)
			{
				return part switch
				{
					AdditionalMessageContentPart content => new UserMessagePartInfo { Kind = "content", Title = content.ChipTitle?.Value },
					AdditionalMessagePart messagePart => new UserMessagePartInfo { Kind = "part", Title = messagePart.ChipTitle?.Value },
					AdditionalChatDataOverlayItem overlay => new UserMessagePartInfo { Kind = "overlay", Title = overlay.Title },
					AdditionalChatDataInputRailItem => new UserMessagePartInfo { Kind = "rail" },
					_ => new UserMessagePartInfo { Kind = "data" }
				};
			}
		}

		/// <summary>
		/// The slash command a message carries, resolved as far as the moment allows.
		/// </summary>
		public sealed class UserMessageCommandInfo
		{
			/// <summary>
			/// The token exactly as the user typed it, without the leading slash.
			/// </summary>
			public string Token { get; init; } = string.Empty;

			/// <summary>
			/// The arguments exactly as the user typed them.
			/// </summary>
			public string? Arguments { get; init; }

			/// <summary>
			/// Whether the token resolves to a command, or <see langword="null"/> when the moment has not resolved it
			/// yet.
			/// </summary>
			public bool? Valid { get; init; }
		}
	}
}
