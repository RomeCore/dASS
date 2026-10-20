using Avalonia.Media;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Localization;
using RCLargeLanguageModels.Messages.Attachments;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Builds the attachment parts the Lua API exposes through <c>append_part</c>,
	/// <c>append_content_part</c> and <c>append_attachment_part</c>.
	/// </summary>
	/// <remarks>
	/// Shared by <see cref="LuaChatMessage"/>, the chat-level functions and <c>dass.tool.result.*</c>,
	/// so the three surfaces cannot drift apart in how they treat title, icon and colour.
	/// Parts created from Lua are never restorable: the draft restorer has nothing to put back.
	/// </remarks>
	internal static class LuaAttachmentParts
	{
		public static AdditionalMessagePart CreatePart(string? title, string? icon, string? color)
			=> new()
			{
				ChipTitle = ToTitle(title),
				ChipIcon = ParseIcon(icon),
				ChipColor = ParseColor(color),
				IsRestorable = false
			};

		public static AdditionalMessageContentPart CreateContentPart(string? content, string? title, string? icon,
			string? color)
			=> new()
			{
				Content = content ?? string.Empty,
				ChipTitle = ToTitle(title),
				ChipIcon = ParseIcon(icon),
				ChipColor = ParseColor(color),
				IsRestorable = false
			};

		public static NativeAttachmentMessagePart CreateNativePart(IAttachment? native, string? title, string? icon,
			string? color)
			=> new()
			{
				NativeAttachment = native,
				ChipTitle = ToTitle(title),
				ChipIcon = ParseIcon(icon),
				ChipColor = ParseColor(color),
				IsRestorable = false
			};

		public static LocaleKeyBase? ToTitle(string? title)
			=> string.IsNullOrWhiteSpace(title) ? null : Locale.GetConstKey(title);

		public static VisualIconKind? ParseIcon(string? icon)
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

		public static Color? ParseColor(string? color)
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
