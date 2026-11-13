using System.Runtime.CompilerServices;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional.Context;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// A curated, mutable Lua handle over a <see cref="BranchedMessage"/>, exposed to Lua as the
	/// <c>chat_message</c> userdata returned by <c>dass.chat.get_messages()</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Deliberately a hand-written surface rather than a raw reflection proxy over the domain model: reading and
	/// writing <see cref="Content"/>, <see cref="Error"/> and <see cref="DisabledForAgents"/> is the whole point, while
	/// tool calls, agent tasks and the underlying <see cref="BranchedMessage"/> must stay hidden.
	/// </para>
	/// <para>
	/// Handles are cached per <see cref="BranchedMessage"/>, so two <c>get_messages()</c> results return the same
	/// userdata for the same message and Lua identity comparison keeps working.
	/// </para>
	/// </remarks>
	public sealed class LuaChatMessage
	{
		private static readonly ConditionalWeakTable<BranchedMessage, LuaChatMessage> _cache = [];

		/// <summary>
		/// Wraps a branched message into a <c>chat_message</c> userdata, reusing the cached handle when one exists.
		/// </summary>
		[LuaHidden]
		public static LuaUserData Wrap(BranchedMessage message, LuaDataAttachmentDispatcher dispatcher)
		{
			var handle = _cache.GetValue(message, m => new LuaChatMessage(m, dispatcher));
			return new LuaUserData(handle, "chat_message")
			{
				Metatable = UserDataMetatableGenerator.GetOrCreate(typeof(LuaChatMessage))
			};
		}

		[LuaHidden]
		public BranchedMessage Message { get; }

		private readonly LuaDataAttachmentDispatcher _dispatcher;

		private LuaChatMessage(BranchedMessage message, LuaDataAttachmentDispatcher dispatcher)
		{
			Message = message;
			_dispatcher = dispatcher;
		}

		// === Read/write ===

		public string Content
		{
			get => Message.Message.Content;
			set => Message.Message.Content = value ?? string.Empty;
		}

		public string? ReasoningSignature
		{
			get => (Message.Message as AssistantMessage)?.ReasoningSignature;
			set
			{
				if (Message.Message is not AssistantMessage assistant)
					throw new LuaRuntimeException("chat_message.reasoning_signature can only be set on assistant messages");
				assistant.ReasoningSignature = value;
			}
		}

		public string? ReasoningContent
		{
			get => (Message.Message as AssistantMessage)?.ReasoningContent;
			set
			{
				if (Message.Message is not AssistantMessage assistant)
					throw new LuaRuntimeException("chat_message.reasoning_content can only be set on assistant messages");
				assistant.ReasoningContent = value;
			}
		}

		public string? Error
		{
			get => Message.Message.Error?.Value;
			set => Message.Message.Error = string.IsNullOrEmpty(value) ? null : Locale.GetConstKey(value);
		}

		public bool DisabledForAgents
		{
			get => Message.Message.IsDisabledForAgents;
			set => Message.Message.IsDisabledForAgents = value;
		}

		// === Read-only ===

		public string Role
		{
			get
			{
				var name = Message.Message.GetType().Name;
				if (name.EndsWith("Message", StringComparison.Ordinal))
					name = name[..^"Message".Length];
				return name.ToLowerInvariant();
			}
		}

		public int Id => Message.MessageId;

		public int Index => Message.MessageIndex + 1;

		public int BranchIndex => Message.SelectedBranchIndex + 1;

		public int BranchesCount => Message.AvailableBranchesCount;

		public bool? IsUserLike
		{
			get
			{
				if (Message.Message is AssistantMessage assistant)
					return assistant.IsUserLike;
				return null;
			}
		}

		public LuaChatMessageUsage? Usage => field ??= Message.Message.AdditionalData.TryGet<TokenCostViewModel>() is { } tokenCost ?
			new LuaChatMessageUsage(tokenCost) : null;

		// === Appending parts ===

		public void AppendPart(string? title, string? icon, string? color)
		{
			Message.Message.AdditionalData.Add(LuaAttachmentParts.CreatePart(title, icon, color));
		}

		public void AppendContentPart(string? content, string? title, string? icon, string? color)
		{
			Message.Message.AdditionalData.Add(LuaAttachmentParts.CreateContentPart(content, title, icon, color));
		}

		public void AppendAttachmentPart(object? attachment, string? title, string? icon, string? color)
		{
			if (attachment is not ILuaNativeAttachable attachable)
				throw new LuaRuntimeException(
					"chat_message:append_attachment_part(attachment, ...): 'attachment' must be " +
					"attachable (e.g. an image handle created by image.load()/image.create()).");

			Message.Message.AdditionalData.Add(
				LuaAttachmentParts.CreateNativePart(attachable.GetNativeAttachment(), title, icon, color));
		}

		/// <summary>
		/// Appends one attachable handle to the message.
		/// </summary>
		/// <param name="handle">The Lua-side attachable handle.</param>
		/// <param name="options">The presentation options, or <see langword="null"/> when none were passed.</param>
		public void AppendData(object? handle, object? options = null)
		{
			const string call = "chat_message:append_data(handle, options)";

			var parsed = LuaAttachmentOptions.Parse(options, call);
			var payload = _dispatcher.TryResolve(handle) ?? throw _dispatcher.CreateError(handle, call);

			Message.Message.AddAttachment(parsed.Apply(payload, isChatHost: false, call));
		}

		/// <summary>
		/// Removes everything the handle appended to the message.
		/// </summary>
		/// <param name="handle">The Lua-side attachable handle.</param>
		/// <returns><see langword="true"/> when something of this handle was attached to this message.</returns>
		public bool RemoveData(object? handle)
			=> _dispatcher.TryRemove(Message.Message, handle);
	}
}
