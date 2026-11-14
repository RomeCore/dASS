using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.MVVM.Additional.Chat;
using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// The parsed options table of an attachable Lua call: where the resolved payload is shown and how it behaves.
	/// </summary>
	/// <remarks>
	/// Every member is an override, never a default: an option the script did not set leaves the payload as it is.
	/// A rail placement without <c>surface = "rail"</c> is refused rather than silently dropped, because the author
	/// would have no way to find where the payload went.
	/// </remarks>
	public sealed class LuaAttachmentDataOptions : LuaAttachmentOptions
	{
		/// <summary>
		/// The only surface a script may ask for today.
		/// </summary>
		public const string RailSurface = "rail";

		private static readonly string[] _allowedKeys =
			["visible", "temporary", "replacement_key", "surface", "horizontal", "vertical"];

		/// <summary>
		/// Gets the value of <c>visible</c>, or <see langword="null"/> when it was not given.
		/// </summary>
		public bool? Visible { get; private init; }

		/// <summary>
		/// Gets the value of <c>temporary</c>, or <see langword="null"/> when it was not given.
		/// </summary>
		public bool? Temporary { get; private init; }

		/// <summary>
		/// Gets the key the payload replaces an older item by, or <see langword="null"/> when it was not given.
		/// </summary>
		public string? ReplacementKey { get; private init; }

		/// <summary>
		/// Gets the surface the payload was asked to appear on, or <see langword="null"/> when none was asked for.
		/// </summary>
		private string? Surface { get; init; }

		/// <summary>
		/// Gets the rail group the payload was asked for.
		/// </summary>
		private ChatInputRailHorizontalPlacement Horizontal { get; init; } = ChatInputRailHorizontalPlacement.Left;

		/// <summary>
		/// Gets the rail strip the payload was asked for.
		/// </summary>
		private ChatInputRailVerticalPlacement Vertical { get; init; } = ChatInputRailVerticalPlacement.Bottom;

		private LuaAttachmentDataOptions()
		{
		}

		/// <summary>
		/// Parses the argument the script passed in the options position.
		/// </summary>
		/// <param name="value">The options table, <c>nil</c>, or <see langword="null"/> when nothing was passed.</param>
		/// <param name="call">The Lua call being executed, for the error message.</param>
		/// <exception cref="LuaRuntimeException">The options table is malformed.</exception>
		public static LuaAttachmentDataOptions Parse(object? value, string call)
		{
			if (value is null or LuaNil)
				return new LuaAttachmentDataOptions();

			var table = OpenTable(value, call, _allowedKeys, "options must be a table.");

			var surface = ReadString(table, "surface", call);
			var horizontal = table.Get("horizontal");
			var vertical = table.Get("vertical");

			if (surface is null && (horizontal is not LuaNil || vertical is not LuaNil))
				throw new LuaRuntimeException(
					$"{call}: options.horizontal and options.vertical require options.surface = \"{RailSurface}\".");

			return new LuaAttachmentDataOptions
			{
				Visible = ReadBool(table, "visible", call),
				Temporary = ReadBool(table, "temporary", call),
				ReplacementKey = ReadString(table, "replacement_key", call),
				Surface = surface,
				Horizontal = horizontal is LuaNil
					? ChatInputRailHorizontalPlacement.Left
					: ReadHorizontal(horizontal, call),
				Vertical = vertical is LuaNil
					? ChatInputRailVerticalPlacement.Bottom
					: ReadVertical(vertical, call)
			};
		}

		/// <summary>
		/// Applies the payload-level flags and, when a surface was asked for, wraps the payload for that surface.
		/// </summary>
		/// <param name="payload">The payload the handle resolved to.</param>
		/// <param name="isChatHost">Whether the host is the chat itself, the only host the rail exists on.</param>
		/// <param name="call">The Lua call being executed, for the error message.</param>
		/// <returns>The object to hand to the host.</returns>
		/// <exception cref="LuaRuntimeException">The surface is unknown, unavailable on this host, or the payload is already placed.</exception>
		public AdditionalChatData Apply(AdditionalChatData payload, bool isChatHost, string call)
		{
			// A rail item of a rail item would have to be searched for recursively and has no use: one payload, one surface.
			if (payload is AdditionalChatDataItemBase)
				throw new LuaRuntimeException($"{call}: the payload is already placed on a surface.");

			if (Visible is { } visible)
				payload.IsVisible = visible;

			if (Temporary is { } temporary)
				payload.IsTemporary = temporary;

			// Written before the envelope is built: the envelope copies the payload's key when it is constructed.
			if (ReplacementKey is { } replacementKey)
				payload.ReplacementKey = replacementKey;

			if (Surface is null)
				return payload;

			if (!string.Equals(Surface, RailSurface, StringComparison.Ordinal))
				throw new LuaRuntimeException(
					$"{call}: options.surface '{Surface}' is unknown. Supported: \"{RailSurface}\".");

			if (!isChatHost)
				throw new LuaRuntimeException(
					$"{call}: the rail is a chat surface - append it to dass.chat.");

			return new AdditionalChatDataInputRailItem
			{
				Content = payload,
				HorizontalPlacement = Horizontal,
				VerticalPlacement = Vertical,
				// A temporary payload inside a persisted envelope is a row pointing at an object the database never saw.
				IsTemporary = payload.IsTemporary
			};
		}

		/// <summary>
		/// Checks whether the host already holds this key with this very payload, where appending would dispose the
		/// payload the new item is built from - a live terminal, for example, would die and come back dead.
		/// </summary>
		/// <param name="host">The host the item is about to be appended to.</param>
		/// <param name="item">The item the call is about to append.</param>
		/// <returns><see langword="true"/> when appending it again would replace an item holding the same payload.</returns>
		public static bool IsAlreadyAttached(IChatAttachmentHost host, AdditionalChatData item)
		{
			if (item.ReplacementKey is not { } key)
				return false;

			foreach (var attachment in host.GetAttachments())
			{
				if (attachment is not AdditionalChatData existing || existing.ReplacementKey != key)
					continue;

				if (ReferenceEquals(existing, item))
					return true;

				return existing is AdditionalChatDataItemBase left && item is AdditionalChatDataItemBase right
					&& ReferenceEquals(left.Content, right.Content);
			}

			return false;
		}

		private static ChatInputRailHorizontalPlacement ReadHorizontal(LuaValue value, string call)
		{
			if (value is LuaString name)
			{
				if (string.Equals(name.Value, "left", StringComparison.OrdinalIgnoreCase))
					return ChatInputRailHorizontalPlacement.Left;

				if (string.Equals(name.Value, "right", StringComparison.OrdinalIgnoreCase))
					return ChatInputRailHorizontalPlacement.Right;
			}

			throw new LuaRuntimeException($"{call}: options.horizontal must be \"left\" or \"right\".");
		}

		private static ChatInputRailVerticalPlacement ReadVertical(LuaValue value, string call)
		{
			if (value is LuaString name)
			{
				if (string.Equals(name.Value, "top", StringComparison.OrdinalIgnoreCase))
					return ChatInputRailVerticalPlacement.Top;

				if (string.Equals(name.Value, "bottom", StringComparison.OrdinalIgnoreCase))
					return ChatInputRailVerticalPlacement.Bottom;
			}

			throw new LuaRuntimeException($"{call}: options.vertical must be \"top\" or \"bottom\".");
		}
	}
}
