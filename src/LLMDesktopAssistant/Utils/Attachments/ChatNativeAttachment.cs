using RCLargeLanguageModels.Messages.Attachments;

namespace LLMDesktopAssistant.Utils.Attachments
{
	/// <summary>
	/// A native attachment a tool produces. It either wraps an existing <see cref="IAttachment"/> or
	/// builds one from a kind and a url (which may already carry base64 data).
	/// </summary>
	/// <remarks>
	/// The wrapping constructor preserves the original attachment subtype and its source; the kind/url
	/// constructor is the convenience form for callers that only have those two values.
	/// </remarks>
	public sealed class ChatNativeAttachment : INativeChatAttachment
	{
		private readonly IAttachment? _native;
		private readonly AttachmentKind? _kind;
		private readonly string? _url;

		/// <summary>
		/// Wraps an existing native attachment.
		/// </summary>
		/// <param name="native">The native attachment.</param>
		public ChatNativeAttachment(IAttachment native)
		{
			_native = native ?? throw new ArgumentNullException(nameof(native));
		}

		/// <summary>
		/// Builds a native attachment from a kind and a url.
		/// </summary>
		/// <param name="kind">The attachment kind.</param>
		/// <param name="url">The url; generated content already carries base64 data.</param>
		public ChatNativeAttachment(AttachmentKind kind, string url)
		{
			_kind = kind;
			_url = url ?? throw new ArgumentNullException(nameof(url));
		}

		/// <inheritdoc/>
		public IAttachment? Native => _native ?? _kind switch
		{
			AttachmentKind.Image => new ImageAttachment(_url!),
			AttachmentKind.Audio => new AudioAttachment(_url!),
			AttachmentKind.Video => new VideoAttachment(_url!),
			_ => null
		};
	}
}
