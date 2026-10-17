using LLMDesktopAssistant.Utils.Attachments;
using RCLargeLanguageModels.Messages.Attachments;

namespace LLMDesktopAssistant.Agents.Tasks
{
	/// <summary>
	/// A native attachment of the task world. It carries the kind and the url (which may already carry
	/// base64 data) and materializes the <see cref="IAttachment"/> when the model is called.
	/// </summary>
	public sealed class AgentNativeAttachment : AgentAttachment, INativeChatAttachment
	{
		/// <summary>
		/// The type of attachment.
		/// </summary>
		public required AttachmentKind Type { get; init; }

		/// <summary>
		/// The URL of the attachment. Can contain encoded base64 data
		/// (e.g. <c>data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAUA...</c>).
		/// </summary>
		public required string Url { get; init; }

		/// <inheritdoc/>
		public IAttachment? Native => Type switch
		{
			AttachmentKind.Image => new ImageAttachment(Url),
			AttachmentKind.Audio => new AudioAttachment(Url),
			AttachmentKind.Video => new VideoAttachment(Url),
			_ => null
		};

		/// <summary>
		/// Creates a native attachment from base64 data.
		/// </summary>
		/// <param name="type">The type of attachment.</param>
		/// <param name="format">The data format (e.g. "png").</param>
		/// <param name="base64Data">The base64 encoded data.</param>
		/// <param name="source">The source of the attachment, if applicable.</param>
		/// <returns>The native attachment.</returns>
		public static AgentNativeAttachment FromBase64(AttachmentKind type, string format, string base64Data,
			object? source = null)
		{
			var dataType = type switch
			{
				AttachmentKind.Image => "image",
				AttachmentKind.Audio => "audio",
				AttachmentKind.Video => "video",
				_ => throw new ArgumentOutOfRangeException(nameof(type), "Invalid attachment type.")
			};
			return new AgentNativeAttachment
			{
				Type = type,
				Url = $"data:{dataType}/{format};base64,{base64Data}",
				Source = source
			};
		}

		/// <summary>
		/// Attempts to convert a native attachment into a task attachment. Returns <see langword="null"/>
		/// when the attachment kind is not supported.
		/// </summary>
		/// <param name="attachment">The native attachment to convert.</param>
		/// <param name="source">The source of the attachment, if applicable.</param>
		/// <returns>The converted attachment, or <see langword="null"/>.</returns>
		public static AgentNativeAttachment? TryFromNative(IAttachment? attachment, object? source = null)
		{
			switch (attachment)
			{
				case IImageAttachment imageAttachment:
					return new AgentNativeAttachment
					{
						Type = AttachmentKind.Image,
						Url = imageAttachment.Url,
						Source = source ?? attachment
					};
				case IAudioAttachment audioAttachment:
					return new AgentNativeAttachment
					{
						Type = AttachmentKind.Audio,
						Url = audioAttachment.Url,
						Source = source ?? attachment
					};
				case IVideoAttachment videoAttachment:
					return new AgentNativeAttachment
					{
						Type = AttachmentKind.Video,
						Url = videoAttachment.Url,
						Source = source ?? attachment
					};
			}

			return null;
		}
	}
}
