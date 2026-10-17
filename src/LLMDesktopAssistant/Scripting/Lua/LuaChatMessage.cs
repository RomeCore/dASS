using System.Runtime.CompilerServices;
using AsyncLua;
using AsyncLua.Values;
using Avalonia.Media;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services.Attachments;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Utils.Attachments;

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
		public static LuaUserData Wrap(BranchedMessage message)
		{
			var handle = _cache.GetValue(message, m => new LuaChatMessage(m));
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

		private LuaChatMessage(BranchedMessage message)
		{
			Message = message;
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
			Message.Message.AdditionalData.Add(new AdditionalMessagePart
			{
				ChipTitle = ToTitle(title),
				ChipIcon = ParseIcon(icon),
				ChipColor = ParseColor(color),
				IsRestorable = false
			});
		}

		public void AppendContentPart(string? content, string? title, string? icon, string? color)
		{
			Message.Message.AdditionalData.Add(new AdditionalMessageContentPart
			{
				Content = content ?? string.Empty,
				ChipTitle = ToTitle(title),
				ChipIcon = ParseIcon(icon),
				ChipColor = ParseColor(color),
				IsRestorable = false
			});
		}

		public void AppendAttachmentPart(object? attachment, string? title, string? icon, string? color)
		{
			if (attachment is not LuaImage image)
				throw new LuaRuntimeException(
					"chat_message:append_attachment_part(attachment, ...): 'attachment' must be an image handle " +
					"created by image.load()/image.create().");

			Message.Message.AdditionalData.Add(new NativeAttachmentMessagePart
			{
				NativeAttachment = new SerializableImageAttachment(image.Format, image.ToBase64()),
				ChipTitle = ToTitle(title),
				ChipIcon = ParseIcon(icon),
				ChipColor = ParseColor(color),
				IsRestorable = false
			});
		}

		public void AppendData(params object?[] controls)
		{
			foreach (var control in controls)
			{
				if (control is not LuaUiControl uiControl)
					throw new LuaRuntimeException(
						"chat_message:append_data(...): arguments must be 'uicontrol' userdata created by " +
						"dass.ui.create_control().");

				var additional = new DynamicAxamlControlAdditionalData(uiControl.Xaml, uiControl.ViewModel);

				// Follow the handle: ui:set_xaml(xaml) re-renders every control appended from it.
				void OnXamlChanged(object? _, EventArgs __) => additional.Axaml = uiControl.Xaml;
				uiControl.XamlChanged += OnXamlChanged;
				additional.OnDispose += (_, _) => uiControl.XamlChanged -= OnXamlChanged;

				Message.Message.AdditionalData.Add(additional);
			}
		}

		// === Helpers ===

		private static LocaleKeyBase? ToTitle(string? title)
			=> string.IsNullOrWhiteSpace(title) ? null : Locale.GetConstKey(title);

		private static VisualIconKind? ParseIcon(string? icon)
		{
			if (string.IsNullOrWhiteSpace(icon))
				return null;

			try
			{
				var kind = VisualIconKind.Parse(icon);
				return VisualIconDataHandler.Resolve(kind).IsOk ? kind : null;
			}
			catch
			{
				return null;
			}
		}

		private static Color? ParseColor(string? color)
		{
			if (string.IsNullOrWhiteSpace(color))
				return null;

			try
			{
				return Color.Parse(color);
			}
			catch
			{
				return null;
			}
		}
	}
}
