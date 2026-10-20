using System.Runtime.CompilerServices;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Domain;
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
		private static readonly ConditionalWeakTable<BranchedMessage, LuaChatMessage> _cache = new();

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

		/// <summary>
		/// The underlying branched message. Hidden from Lua.
		/// </summary>
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

		public int Index => Message.MessageIndex;

		public int BranchIndex => Message.SelectedBranchIndex;

		public int BranchesCount => Message.AvailableBranchesCount;

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
					"chat_message:append_attachment_part(attachment, ...): 'attachment' must implement " +
					"ILuaNativeAttachable (e.g. an image handle created by image.load()/image.create()).");

			Message.Message.AdditionalData.Add(
				LuaAttachmentParts.CreateNativePart(attachable.GetNativeAttachment(), title, icon, color));
		}

		public void AppendData(params object?[] controls)
		{
			foreach (var control in controls)
				_dispatcher.Attach(Message.Message, control, "chat_message:append_data(...)");
		}
	}
}
