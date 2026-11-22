using AsyncLua.Values;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Geometric and structural changes of <see cref="LuaImage"/>: resize, crop, rotate, flip and clone.
	/// </summary>
	public sealed partial class LuaImage
	{
		/// <summary>
		/// Resizes the image.
		/// </summary>
		public void Resize(int width, int height, LuaTable? optionsTable = null)
		{
			EnsureNotDisposed();

			var mode = ResizeMode.Stretch;
			if (optionsTable != null)
			{
				var modeStr = optionsTable.Get("mode") is LuaString modeStrVal ? modeStrVal.Value : null;
				mode = modeStr?.ToLowerInvariant() switch
				{
					"stretch" => ResizeMode.Stretch,
					"min" => ResizeMode.Min,
					"max" => ResizeMode.Max,
					"pad" => ResizeMode.BoxPad,
					"crop" => ResizeMode.Crop,
					_ => ResizeMode.Stretch
				};
			}

			var resizeOptions = new ResizeOptions
			{
				Size = new Size(width, height),
				Mode = mode,
				Sampler = KnownResamplers.Lanczos3
			};

			_image.Mutate(ctx => ctx.Resize(resizeOptions));
			Revision++;
		}

		/// <summary>
		/// Crops the image to the specified rectangle.
		/// </summary>
		public void Crop(int x, int y, int width, int height)
		{
			EnsureNotDisposed();
			_image.Mutate(ctx => ctx.Crop(new Rectangle(x, y, width, height)));
			Revision++;
		}

		/// <summary>
		/// Rotates the image by the specified number of degrees.
		/// </summary>
		public void Rotate(float degrees)
		{
			EnsureNotDisposed();
			_image.Mutate(ctx => ctx.Rotate(degrees));
			Revision++;
		}

		/// <summary>
		/// Flips the image.
		/// </summary>
		public void Flip(string direction)
		{
			EnsureNotDisposed();
			var flipMode = direction?.ToLowerInvariant() switch
			{
				"horizontal" or "h" => FlipMode.Horizontal,
				"vertical" or "v" => FlipMode.Vertical,
				"both" or "hv" => FlipMode.Horizontal | FlipMode.Vertical,
				_ => throw new ArgumentException($"Invalid flip direction: '{direction}'. Use 'horizontal', 'vertical', or 'both'.")
			};
			_image.Mutate(ctx => ctx.Flip(flipMode));
			Revision++;
		}

		/// <summary>
		/// Creates a deep copy of the image.
		/// </summary>
		public LuaImage Clone()
		{
			EnsureNotDisposed();
			return new LuaImage(_image.Clone(ctx => { }), Format);
		}

		/// <summary>
		/// Gets image information as a Lua table.
		/// </summary>
		public LuaTable GetInfoTable()
		{
			EnsureNotDisposed();
			var result = new LuaTable();
			result["width"] = new LuaNumber(Width);
			result["height"] = new LuaNumber(Height);
			result["format"] = new LuaString(Format);
			return result;
		}
	}
}
