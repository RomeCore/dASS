using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.MVVM.Additional.Chat;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// The parsed options table of an attachable Lua call: where the resolved payload is shown and how it behaves.
	/// </summary>
	/// <remarks>
	/// Parsing is strict on purpose: an unknown key, an unknown value or a placement without
	/// <c>surface = "rail"</c> is a script mistake, and a silent fallback would put the payload somewhere the
	/// author cannot find it.
	/// </remarks>
	public sealed class LuaAttachmentOptions
	{
		/// <summary>
		/// The only surface a script may ask for today.
		/// </summary>
		public const string RailSurface = "rail";

		private static readonly string[] _allowedKeys =
			["visible", "temporary", "surface", "horizontal", "vertical"];

		/// <summary>
		/// Gets the value of <c>options.visible</c>, or <see langword="null"/> when it was not given.
		/// </summary>
		public bool? Visible { get; private init; }

		/// <summary>
		/// Gets the value of <c>options.temporary</c>, or <see langword="null"/> when it was not given.
		/// </summary>
		public bool? Temporary { get; private init; }

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

		private LuaAttachmentOptions()
		{
		}

		/// <summary>
		/// Parses the argument the script passed in the options position.
		/// </summary>
		/// <param name="value">The options table, <c>nil</c>, or <see langword="null"/> when nothing was passed.</param>
		/// <param name="call">The Lua call being executed, for the error message.</param>
		/// <exception cref="LuaRuntimeException">The options table is malformed.</exception>
		public static LuaAttachmentOptions Parse(object? value, string call)
		{
			if (value is null or LuaNil)
				return new LuaAttachmentOptions();

			if (value is not LuaTable table)
				throw new LuaRuntimeException($"{call}: options must be a table.");

			foreach (var (key, _) in table)
			{
				if (key is LuaString name && !_allowedKeys.Contains(name.Value))
					throw new LuaRuntimeException(
						$"{call}: options.{name.Value} is unknown. Expected one of: {string.Join(", ", _allowedKeys)}.");
			}

			var surface = ReadSurface(table, call);
			var horizontal = table.Get("horizontal");
			var vertical = table.Get("vertical");

			if (surface is null && (horizontal is not LuaNil || vertical is not LuaNil))
				throw new LuaRuntimeException(
					$"{call}: options.horizontal and options.vertical require options.surface = \"{RailSurface}\".");

			return new LuaAttachmentOptions
			{
				Visible = ReadBool(table, "visible", call),
				Temporary = ReadBool(table, "temporary", call),
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

		private static string? ReadSurface(LuaTable table, string call)
			=> table.Get("surface") switch
			{
				LuaNil => null,
				LuaString name => name.Value,
				_ => throw new LuaRuntimeException($"{call}: options.surface must be a string.")
			};

		private static bool? ReadBool(LuaTable table, string key, string call)
			=> table.Get(key) switch
			{
				LuaNil => null,
				LuaBoolean value => value.Value,
				_ => throw new LuaRuntimeException($"{call}: options.{key} must be a boolean.")
			};

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
