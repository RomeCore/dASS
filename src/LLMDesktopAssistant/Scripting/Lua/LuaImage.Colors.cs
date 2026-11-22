using AsyncLua;
using AsyncLua.Values;
using SixLabors.ImageSharp.PixelFormats;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Colour reading and writing of <see cref="LuaImage"/>: hex strings in, <see cref="Rgba32"/> in the middle.
	/// </summary>
	public sealed partial class LuaImage
	{
		/// <summary>
		/// Parses a colour, falling back to transparent for anything unparsable, which is what <c>image.create</c>
		/// has always done with a bad background colour.
		/// </summary>
		private static Rgba32 ParseColor(string color)
			=> TryParseColor(color, out var parsed) ? parsed : new Rgba32(0, 0, 0, 0);

		/// <summary>
		/// Parses "#rgb", "#rrggbb", "#rrggbbaa", the same values with a "0x" prefix, and bare hex.
		/// </summary>
		private static bool TryParseColor(string color, out Rgba32 parsed)
		{
			var text = color.Trim();
			if (text.StartsWith('#'))
				text = text[1..];
			else if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
				text = text[2..];

			return Rgba32.TryParseHex(text, out parsed);
		}

		/// <summary>
		/// Reads a colour a drawing call must be able to use, rejecting everything but a hex string.
		/// </summary>
		private static Rgba32 ReadColor(LuaValue value, string call)
		{
			if (value is LuaString text && TryParseColor(text.Value, out var parsed))
				return parsed;

			throw new LuaRuntimeException($"{call}: expected a color string like \"#rrggbb\" or \"#rrggbbaa\".");
		}

		/// <summary>
		/// Converts a normalized channel value into a byte.
		/// </summary>
		private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value * 255.0), 0, 255);

		/// <summary>
		/// Formats a pixel the way the API reads and writes colours: "#rrggbbaa".
		/// </summary>
		private static string ToHex(Rgba32 color)
			=> $"#{color.R:x2}{color.G:x2}{color.B:x2}{color.A:x2}";
	}
}
