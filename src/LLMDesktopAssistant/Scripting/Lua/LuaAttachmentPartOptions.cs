using AsyncLua;
using AsyncLua.Values;
using Avalonia.Media;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// The parsed spec table of <c>append_part</c>: one part described by named fields instead of by position.
	/// </summary>
	/// <remarks>
	/// The spec describes the part's whole state, never a patch: a field that is absent means its default, so
	/// updating an existing part in place and replacing it produce the same result.
	/// </remarks>
	public sealed class LuaAttachmentPartOptions : LuaAttachmentOptions
	{
		private static readonly string[] _allowedKeys =
			["content", "attachment", "title", "icon", "color", "visible", "temporary", "replacement_key"];

		/// <summary>
		/// Gets the text this part contributes to the model-facing content of its host, or <see langword="null"/>.
		/// </summary>
		public string? Content { get; private init; }

		/// <summary>
		/// Gets the handle this part renders as a native attachment, or <see langword="null"/>.
		/// </summary>
		public ILuaNativeAttachable? Attachment { get; private init; }

		/// <summary>
		/// Gets the key the part replaces an older part by, or <see langword="null"/>.
		/// </summary>
		public string? ReplacementKey { get; private init; }

		/// <summary>
		/// Gets the chip title, or <see langword="null"/>.
		/// </summary>
		private string? ChipTitle { get; init; }

		/// <summary>
		/// Gets the chip icon name, or <see langword="null"/>.
		/// </summary>
		private string? ChipIcon { get; init; }

		/// <summary>
		/// Gets the chip accent color, or <see langword="null"/>.
		/// </summary>
		private string? ChipColor { get; init; }

		/// <summary>
		/// Gets the visibility the part asked for, or <see langword="null"/> when it was not given.
		/// </summary>
		private bool? Visible { get; init; }

		/// <summary>
		/// Gets the persistence flag the part asked for, or <see langword="null"/> when it was not given.
		/// </summary>
		private bool? Temporary { get; init; }

		/// <summary>
		/// Gets the part type this spec describes: text, native attachment or a plain chip.
		/// </summary>
		public Type PartType => Attachment is not null
			? typeof(NativeAttachmentMessagePart)
			: Content is not null
				? typeof(AdditionalMessageContentPart)
				: typeof(AdditionalMessagePart);

		private LuaAttachmentPartOptions()
		{
		}

		/// <summary>
		/// Parses the argument the script passed to <c>append_part</c>.
		/// </summary>
		/// <param name="value">The spec table, or the value the script passed in its place.</param>
		/// <param name="call">The Lua call being executed, for the error message.</param>
		/// <exception cref="LuaRuntimeException">The argument is not a table or the table is malformed.</exception>
		public static LuaAttachmentPartOptions Parse(object? value, string call)
		{
			var table = OpenTable(value, call, _allowedKeys,
				"a table is expected, e.g. { title = \"Done\", icon = \"Check\" }.");

			var content = ReadString(table, "content", call);
			var attachment = ReadAttachment(table, call);

			if (content is not null && attachment is not null)
				throw new LuaRuntimeException(
					$"{call}: 'content' and 'attachment' are mutually exclusive - a part is either text or an attachment.");

			return new LuaAttachmentPartOptions
			{
				Content = content,
				Attachment = attachment,
				ChipTitle = ReadString(table, "title", call),
				ChipIcon = ReadString(table, "icon", call),
				ChipColor = ReadString(table, "color", call),
				Visible = ReadBool(table, "visible", call),
				Temporary = ReadBool(table, "temporary", call),
				ReplacementKey = ReadString(table, "replacement_key", call)
			};
		}

		/// <summary>
		/// Builds a new part of the type this spec describes.
		/// </summary>
		/// <returns>The part, not attached to any host yet.</returns>
		public AdditionalMessagePart Create()
		{
			AdditionalMessagePart part = Attachment is not null
				? new NativeAttachmentMessagePart()
				: Content is not null
					? new AdditionalMessageContentPart()
					: new AdditionalMessagePart();

			ApplyTo(part);

			// A part created from Lua has no draft to be restored from, and the restorer never gets one.
			part.IsRestorable = false;
			part.ReplacementKey = ReplacementKey;

			return part;
		}

		/// <summary>
		/// Writes every field of this spec onto an existing part of the same type, so a repeated
		/// <c>append_part</c> under one <c>replacement_key</c> updates one part instead of replacing it.
		/// </summary>
		/// <param name="part">The part to overwrite; its key and its type are kept.</param>
		public void ApplyTo(AdditionalMessagePart part)
		{
			part.ChipTitle = ToTitle(ChipTitle);
			part.ChipIcon = ParseIcon(ChipIcon);
			part.ChipColor = ParseColor(ChipColor);
			part.IsVisible = Visible ?? true;
			part.IsTemporary = Temporary ?? false;

			if (part is AdditionalMessageContentPart content)
				content.Content = Content ?? string.Empty;

			if (part is NativeAttachmentMessagePart native)
				native.NativeAttachment = Attachment?.GetNativeAttachment();
		}

		/// <summary>
		/// Resolves the attachment handle to the native attachment the part carries.
		/// </summary>
		private static ILuaNativeAttachable? ReadAttachment(LuaTable table, string call)
			=> table.Get("attachment") switch
			{
				LuaNil => null,
				LuaUserData userData when userData.Target is ILuaNativeAttachable attachable => attachable,
				_ => throw new LuaRuntimeException(
					$"{call}: 'attachment' must be an attachable handle " +
					"(e.g. an image created by image.load()/image.create()).")
			};

		/// <summary>
		/// Turns the title into a locale key, so the chip relocalizes when the language changes.
		/// </summary>
		private static LocaleKeyBase? ToTitle(string? title)
			=> string.IsNullOrWhiteSpace(title) ? null : Locale.GetConstKey(title);

		/// <summary>
		/// Resolves an icon name against the icon catalogue, dropping a name that has no data.
		/// </summary>
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

		/// <summary>
		/// Parses a hex accent color, dropping a value that does not parse.
		/// </summary>
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
