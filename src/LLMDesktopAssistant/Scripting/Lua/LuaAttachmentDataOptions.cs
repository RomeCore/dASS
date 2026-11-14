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
	/// A placement key without its surface is refused rather than silently dropped, because the author would have no
	/// way to find where the payload went - and a key that belongs to another surface is refused for the same reason.
	/// </remarks>
	public sealed class LuaAttachmentDataOptions : LuaAttachmentOptions
	{
		/// <summary>
		/// The surface that puts the payload on the input rail.
		/// </summary>
		public const string RailSurface = "rail";

		/// <summary>
		/// The surface that opens the payload as a floating window over the messages.
		/// </summary>
		public const string OverlaySurface = "overlay";

		private static readonly string[] _allowedKeys =
		[
			"visible", "temporary", "replacement_key", "surface", "horizontal", "vertical",
			"title", "closeable", "left", "top", "width", "height",
		];

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

		/// <summary>
		/// Gets the title of the overlay window, or <see langword="null"/> when it was not given.
		/// </summary>
		private string? Title { get; init; }

		/// <summary>
		/// Gets the value of <c>closeable</c>, or <see langword="null"/> when it was not given.
		/// </summary>
		private bool? Closeable { get; init; }

		/// <summary>
		/// Gets the offset of the overlay window from the left edge of the messages area, as a fraction.
		/// </summary>
		private double? Left { get; init; }

		/// <summary>
		/// Gets the offset of the overlay window from the top edge of the messages area, as a fraction.
		/// </summary>
		private double? Top { get; init; }

		/// <summary>
		/// Gets the width of the overlay window, as a fraction of the messages area.
		/// </summary>
		private double? Width { get; init; }

		/// <summary>
		/// Gets the height of the overlay window, as a fraction of the messages area.
		/// </summary>
		private double? Height { get; init; }

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
			bool isRail = string.Equals(surface, RailSurface, StringComparison.Ordinal);
			bool isOverlay = string.Equals(surface, OverlaySurface, StringComparison.Ordinal);

			if (surface is not null && !isRail && !isOverlay)
				throw new LuaRuntimeException(
					$"{call}: options.surface '{surface}' is unknown. Supported: \"{RailSurface}\", \"{OverlaySurface}\".");

			var horizontal = table.Get("horizontal");
			var vertical = table.Get("vertical");
			var title = ReadString(table, "title", call);
			var closeable = ReadBool(table, "closeable", call);
			var left = ReadFraction(table, "left", call);
			var top = ReadFraction(table, "top", call);
			var width = ReadFraction(table, "width", call);
			var height = ReadFraction(table, "height", call);

			if (!isRail && (horizontal is not LuaNil || vertical is not LuaNil))
				throw new LuaRuntimeException(
					$"{call}: options.horizontal and options.vertical require options.surface = \"{RailSurface}\".");

			List<string> overlayOnly = [];

			if (title is not null)
				overlayOnly.Add("title");

			if (closeable is not null)
				overlayOnly.Add("closeable");

			if (left is not null)
				overlayOnly.Add("left");

			if (top is not null)
				overlayOnly.Add("top");

			if (width is not null)
				overlayOnly.Add("width");

			if (height is not null)
				overlayOnly.Add("height");

			if (!isOverlay && overlayOnly.Count > 0)
				throw new LuaRuntimeException(
					$"{call}: options.{string.Join(", options.", overlayOnly)} require options.surface = \"{OverlaySurface}\".");

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
					: ReadVertical(vertical, call),
				Title = title,
				Closeable = closeable,
				Left = left,
				Top = top,
				Width = width,
				Height = height
			};
		}

		/// <summary>
		/// Applies the payload-level flags and, when a surface was asked for, wraps the payload for that surface.
		/// </summary>
		/// <param name="payload">The payload the handle resolved to.</param>
		/// <param name="isChatHost">Whether the host is the chat itself, the only host surfaces exist on.</param>
		/// <param name="call">The Lua call being executed, for the error message.</param>
		/// <returns>The object to hand to the host.</returns>
		/// <exception cref="LuaRuntimeException">The surface is unavailable on this host, or the payload is already placed.</exception>
		public AdditionalChatData Apply(AdditionalChatData payload, bool isChatHost, string call)
		{
			// A placed item would have to be searched for recursively and has no use: one payload, one surface.
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

			if (!isChatHost)
				throw new LuaRuntimeException(
					$"{call}: '{Surface}' is a chat surface - append it to dass.chat.");

			if (string.Equals(Surface, OverlaySurface, StringComparison.Ordinal))
				return new AdditionalChatDataOverlayItem
				{
					Content = payload,
					Title = Title,
					CanClose = Closeable ?? false,
					Left = Left,
					Top = Top,
					Width = Width,
					Height = Height,
					// A temporary payload inside a persisted envelope is a row pointing at an object the database never saw.
					IsTemporary = payload.IsTemporary
				};

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

		/// <summary>
		/// Reads a fraction member: a number in 0..1, or <see langword="null"/> when the script did not set it.
		/// </summary>
		/// <exception cref="LuaRuntimeException">The member holds a value of another type or outside the range.</exception>
		private static double? ReadFraction(LuaTable table, string key, string call)
			=> table.Get(key) switch
			{
				LuaNil => null,
				LuaNumber number when number.Value is >= 0 and <= 1 => number.Value,
				LuaNumber => throw new LuaRuntimeException($"{call}: options.{key} must be between 0 and 1."),
				_ => throw new LuaRuntimeException($"{call}: options.{key} must be a number between 0 and 1.")
			};
	}
}
