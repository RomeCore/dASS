using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using AsyncLua.Values;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Const;
using LLMDesktopAssistant.StructuredValues.Reactive;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua.Converters
{
	/// <summary>
	/// Keeps a <see cref="LuaImage"/> through a structured value as a marker string carrying its format
	/// and encoded bytes.
	/// </summary>
	/// <remarks>
	/// The marker travels wherever a structured value does: a dynamic view model snapshot, a store or a
	/// BSON field. A revived image has no working directory, so it can be displayed and manipulated but
	/// not saved with <c>image:save</c>.
	/// </remarks>
	[LuaValueNodeConverter(order: 1)]
	public class LuaImageNodeConverter : LuaValueNodeConverter
	{
		/// <summary>The prefix that identifies a serialized image inside a string value.</summary>
		public const string ImageMarker = "\u0000dass:lua-image:v1:";

		private static readonly ConditionalWeakTable<LuaImage, ImageDump> Dumps = [];

		/// <inheritdoc/>
		public override INodeValue? TryConvertFromLua(LuaValue? value, bool isConst)
		{
			if (value is not LuaUserData { Target: LuaImage image })
				return null;

			string marker;
			try
			{
				marker = DumpImage(image);
			}
			catch (Exception ex)
			{
				// A disposed image cannot be encoded; dropping the value beats failing the whole save.
				Log.Warning(ex, "Failed to serialize a Lua image; keeping the value out of the snapshot.");
				return null;
			}

			return isConst ? new ConstNodeStringValue { Value = marker } : new ReactiveNodeStringValue { Value = marker };
		}

		/// <inheritdoc/>
		public override LuaValue? TryConvertToLua(INodeValue? value)
		{
			if (value is INodeStringValue text && TryReviveImage(text.Value, out var image))
				return LuaValueConverter.ToLuaValue(image);
			return null;
		}

		/// <summary>
		/// Encodes a live image into the marker string that carries it through a structured value.
		/// </summary>
		/// <remarks>
		/// The dump is cached per image and per <see cref="LuaImage.Revision"/>: a widget re-serializes its
		/// whole value on every change, and re-encoding a large image each time would stall the UI thread.
		/// </remarks>
		/// <param name="image">The image to encode.</param>
		/// <returns>The marker string, ready to be kept as a string value.</returns>
		public static string DumpImage(LuaImage image)
		{
			var cached = Dumps.GetValue(image, static i => new ImageDump(i.Revision, Compose(i)));
			if (cached.Revision == image.Revision)
				return cached.Marker;

			var dump = new ImageDump(image.Revision, Compose(image));
			Dumps.AddOrUpdate(image, dump);
			return dump.Marker;
		}

		/// <summary>
		/// Determines whether the text is a serialized image marker.
		/// </summary>
		/// <param name="text">The text to test.</param>
		/// <returns><see langword="true"/> if the text starts with <see cref="ImageMarker"/>.</returns>
		public static bool IsImageMarker(string? text)
		{
			return text is not null && text.StartsWith(ImageMarker, StringComparison.Ordinal);
		}

		/// <summary>
		/// Decodes a marker string into the original image format and the encoded bytes.
		/// </summary>
		/// <param name="text">The marker string.</param>
		/// <param name="format">The image format, or an empty string when the payload is malformed.</param>
		/// <param name="bytes">The decoded bytes, or an empty array when the payload is malformed.</param>
		/// <returns><see langword="true"/> if the string is a well-formed marker.</returns>
		public static bool TryParseMarker(string? text, out string format, out byte[] bytes)
		{
			format = string.Empty;
			bytes = [];

			if (!IsImageMarker(text))
				return false;

			var payload = text![ImageMarker.Length..];
			var separator = payload.IndexOf(':');
			if (separator <= 0)
				return false;

			try
			{
				format = payload[..separator];
				bytes = Convert.FromBase64String(payload[(separator + 1)..]);
				return bytes.Length > 0;
			}
			catch (FormatException)
			{
				return false;
			}
		}

		/// <summary>
		/// Revives an image serialized by <see cref="DumpImage"/>.
		/// </summary>
		/// <remarks>
		/// A marker whose payload does not decode stays a string, so the caller keeps the original value
		/// instead of erasing it from history.
		/// </remarks>
		/// <param name="text">The marker string.</param>
		/// <param name="image">The revived image, or <see langword="null"/>.</param>
		/// <returns><see langword="true"/> if the image was revived.</returns>
		public static bool TryReviveImage(string? text, [NotNullWhen(true)] out LuaImage? image)
		{
			image = null;
			if (!TryParseMarker(text, out var format, out var bytes))
				return false;

			try
			{
				image = LuaImage.FromSnapshot(bytes, format);
				return true;
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to revive a serialized Lua image; keeping it as data.");
				return false;
			}
		}

		private static string Compose(LuaImage image)
		{
			return ImageMarker + image.Format + ":" + image.ToBase64();
		}

		private sealed record ImageDump(int Revision, string Marker);
	}
}
