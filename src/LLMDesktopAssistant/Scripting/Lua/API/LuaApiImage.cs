using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for image loading, manipulation and saving: <c>image.*</c>.
	/// Provides access to SixLabors.ImageSharp functionality.
	/// Registered in the global namespace as "image".
	/// </summary>
	[LuaApi(chatScoped: true)]
	public class LuaApiImage : LuaApiBase
	{
		public override string? Namespace => "image";

		public override string? Manuals => """
		--- image - Image loading, manipulation and saving API

		Provides functions to load, create, manipulate and save images.

		Every image handle is a userdata with methods.

		FUNCTIONS:

		--- image.load(path) -> image
		  Loads an image from a file path.
		  Parameters:
		    - path: string - path to the image file
		  Returns: image handle

		--- image.load_base64(base64) -> image
		  Loads an image from a base64-encoded string.
		  Parameters:
		    - base64: string - base64-encoded image data
		  Returns: image handle

		--- async image.load_url(url) -> image
		  Loads an image from a URL.
		  Parameters:
		    - url: string - URL of the image
		  Returns: image handle

		--- image.create(width, height, [color]) -> image
		  Creates a new blank image.
		  Parameters:
		    - width: number - width in pixels
		    - height: number - height in pixels
		    - color: string (optional) - background color (hex "#ff0000", name "red", etc.)
		  Returns: image handle

		--- image.from_bytes(bytes, [format]) -> image
		  Creates an image from raw byte data.
		  Parameters:
		    - bytes: string (binary data as Lua string)
		    - format: string (optional) - image format hint
		  Returns: image handle

		--- image.info(path) -> table
		  Gets image metadata without loading the full image.
		  Parameters:
		    - path: string - path to the image file
		  Returns: table with fields: width, height, format

		--- image.formats() -> table
		  Returns a list of supported image formats.
		  Returns: array of strings

		IMAGE HANDLE METHODS:

		--- img:save(path, [options])
		  Saves the image to a file.
		  Parameters:
		    - path: string - output file path
		    - options: table (optional):
		      - quality: number - JPEG/WebP quality (1-100, default 85)

		--- img:to_base64([format]) -> string
		  Converts the image to a base64-encoded string.
		  Parameters:
		    - format: string (optional) - output format ("png", "jpg", "webp", default: original)
		  Returns: string

		--- img:resize(width, height, [options])
		  Resizes the image.
		  Parameters:
		    - width: number - new width
		    - height: number - new height
		    - options: table (optional):
		      - mode: string - "stretch" (default), "min", "max", "pad", "crop"

		--- img:crop(x, y, width, height)
		  Crops the image to the specified rectangle.

		--- img:rotate(degrees)
		  Rotates the image by the specified degrees.

		--- img:flip(direction)
		  Flips the image.
		  Parameters:
		    - direction: string - "horizontal", "vertical", or "both"

		--- img:clear([color])
		  Fills the whole image with one color; without a color the pixels become transparent.
		  Parameters:
		    - color: string or tuple
		
		--- img:set_pixel(x, y, color)
		  Writes one pixel.
		  Parameters:
		    - x, y: number - pixel coordinates, must be inside the image
		    - color: string - "#rrggbb" or "#rrggbbaa"
		
		--- img:set_pixel(x, y, r, g, b, [a])
		  Writes one pixel.
		  Parameters:
		    - x, y: number - pixel coordinates, must be inside the image
		    - r, g, b, a: number - RGBA values (0-255)
		
		--- img:get_pixel(x, y, [mode]) -> string or tuple
		  Reads one pixel. Mode can be either "hex" (default) or "tuple".
		  Returns: string - "#rrggbbaa" or tuple (r, g, b, a - numbers)

		--- img:paint(fn, [options])
		  Fills a rectangle of pixels from a function, one Lua call per pixel.
		  Parameters:
		    - fn: function - fn(x, y, color) -> color | nil; color is the pixel's current
		      "#rrggbbaa" or r, g, b, a (if "tuple" mode is used), and nil leaves the pixel
		      untouched. x and y are absolute image coordinates.
		    - options: table (optional):
		      - mode: string - "hex" or "tuple": whether to use hex string or tuple of numbers
		        notation for the pixel colors
		      - rect: table - the area to paint: { x = 0, y = 0, width = <image width>,
		        height = <image height> }; it must fit inside the image
		  The callback must not mutate the image it is painting.

		--- img:composite(other, [options])
		  Draws another image over this one.
		  Parameters:
		    - other: image - the image to draw
		    - options: table (optional):
		      - x, y: number - where to start in this image (default 0)
		      - width, height: number - size of the pasted area; other is scaled to it
		        (default: other's own size)
		      - opacity: number - 0..1 (default 1)
		      - mode: string - "normal" (default), "add", "subtract", "multiply", "screen",
		        "overlay", "darken", "lighten", "difference"
		  The pasted area is clipped to this image, so an overlay hanging over an edge is fine.

		--- img:invert([alpha])
		  Inverts the colour channels.
		  Parameters:
			- alpha: boolean (optional, default false) - invert the alpha channel too

		--- img:grayscale()
		  Drops colour, keeping luminance.

		--- img:sepia()
		  Applies the sepia tone.

		--- img:brightness(amount)
		  Multiplies brightness: 1 leaves the image as it is, 0 is black.

		--- img:contrast(amount)
		  Pushes contrast away from mid grey: 1 leaves the image as it is, 0 is flat grey.

		--- img:opacity(amount)
		  Scales the alpha channel, 0..1.

		--- img:blur(sigma)
		  Gaussian blur; sigma must be positive.

		--- img:threshold(value)
		  Makes every pixel black or white, cutting at value (0..1).

		--- img:pixelate(size)
		  Averages blocks of size x size pixels.

		--- img:fill_rect(x, y, width, height, color)
		--- img:fill_rect(x, y, width, height, r, g, b, [a])
		  Fills a rectangle; parts outside the image are clipped.

		--- img:draw_rect(x, y, width, height, color, [thickness])
		--- img:draw_rect(x, y, width, height, r, g, b, [a], [thickness])
		  Strokes a rectangle.

		--- img:fill_circle(x, y, radius, color)
		--- img:fill_circle(x, y, radius, r, g, b, [a])
		  Fills a circle centred on x, y.

		--- img:draw_circle(x, y, radius, color, [thickness])
		--- img:draw_circle(x, y, radius, r, g, b, [a], [thickness])
		  Strokes a circle.

		--- img:fill_ellipse(x, y, radius_x, radius_y, color)
		--- img:fill_ellipse(x, y, radius_x, radius_y, r, g, b, [a])
		  Fills an axis-aligned ellipse centred on x, y.

		--- img:draw_ellipse(x, y, radius_x, radius_y, color, [thickness])
		--- img:draw_ellipse(x, y, radius_x, radius_y, r, g, b, [a], [thickness])
		  Strokes an axis-aligned ellipse; the thickness stays measured in pixels even where the
		  curve is strongly elongated.

		--- img:draw_line(x1, y1, x2, y2, color, [thickness])
		--- img:draw_line(x1, y1, x2, y2, r, g, b, [a], [thickness])
		  Draws a line between two points.

		--- img:fill_polygon(points, color)
		--- img:fill_polygon(points, r, g, b, [a])
		  Fills a polygon with the even-odd rule; points are { x = ..., y = ... } or { x, y }.

		--- img:draw_polygon(points, color, [thickness])
		--- img:draw_polygon(points, r, g, b, [a], [thickness])
		  Strokes a closed polygon.

		--- img:draw_polyline(points, color, [thickness])
		--- img:draw_polyline(points, r, g, b, [a], [thickness])
		  Strokes an open polyline: the last point is not joined back to the first.

		SHAPES:

		  Every shape exists in two forms: a hex colour string, or the channels as
		  r, g, b, [a], [thickness]. Alpha comes before thickness, so a thickness always needs
		  an explicit alpha. Shapes are clipped to the image, so drawing past an edge is safe.

		--- img:clone() -> image
		  Creates a copy of the image.

		--- img:dispose()
		  Manually disposes the underlying image resources.

		IMAGE HANDLE PROPERTIES (read-only):
		  - img.width: number - image width in pixels
		  - img.height: number - image height in pixels
		  - img.format: string - image format ("png", "jpg", "webp", etc.)

		EXAMPLES:

		  -- Load, resize and save
		  local img = image.load("photo.jpg")
		  img:resize(800, 600, { mode = "cover" })
		  img:save("thumb.jpg", { quality = 80 })

		  -- Load from URL and attach to message
		  local img = await image.load_url("https://example.com/chart.png")
		  local system_message = {
		      role = "system",
		      content = "You are a helpful assistant."
		  }
		  local user_message = {
		      role = "user",
		      content = "Check out this chart!",
		      attachments = { img }
		  }
		  local response_messages = await dass.agent.execute_task({ messages = { system_message, user_message } })

		  -- Create an image from scratch
		  local img = image.create(100, 100, "#ff0000")
		  img:save("red_square.png")

		  -- Get metadata without full load
		  local info = image.info("large.png")
		  print(info.width, info.height, info.format)
		
		  -- paint: hex strings in and out, the current colour arrives as the third argument
		  local img = image.create(256, 256)
		  img:paint(function(x, y, color)
		      return x > y and "#1e1e1e" or "#f0f0f0"
		  end)
		
		  -- paint using RGBA tuples: paint the top-left corner red
		  img:paint(function(x, y, r, g, b, a)
		      if x < 8 or y < 8 then return 255, 0, 0, a end
		  end, { mode = "tuple" })
		
		DYNAMIC UI:

		  An image handle is a value like any other in a dynamic view model: bind it to Image.Source.
		  An array of images binds as an ItemsControl list, one Image per item.

		  local vm = {
			  avatar = image.load("avatar.png"),
			  gallery = { image.load("a.png"), image.load("b.png") }
		  }
		  dass.tool.result.append_data(dass.ui.create_control([==[
			<StackPanel xmlns="https://github.com/avaloniaui" Spacing="4">
			  <Image Source="{Binding avatar}" Width="64"/>
			  <ItemsControl ItemsSource="{Binding gallery}">
				<ItemsControl.ItemTemplate>
				  <DataTemplate><Image Source="{Binding}" Width="32"/></DataTemplate>
				</ItemsControl.ItemTemplate>
			  </ItemsControl>
			</StackPanel>
		  ]==], vm))

		  Images are stored with the view model, so a restored widget shows them again after a
		  chat reload. A restored image keeps every manipulation method, img:save() included:
		  the working directory comes from the script that is running, not from the image.

		NOTES:
		  - Image objects must be disposed manually with :dispose().
		  - Supported formats: png, jpg/jpeg, webp, gif, bmp
		  - Paths are resolved against the working directory of the running script; absolute paths
			are used as they are.
		  - base64 format is auto-detected from file header bytes.
		  - All manipulation methods modify the image in-place. Use :clone() first
			if you need to preserve the original.
		  - paint() and map() call Lua once per pixel: a 512x512 image is 262k calls. Keep
			the rect tight, or use noise:render() when the whole field comes from a generator.
		""";

		/// <summary>
		/// Resolves the working directory service from the ambient context, so the API itself holds no state.
		/// </summary>
		private static IWorkingDirectoryAccessService FileAccess(LuaCallingContext ctx)
			=> ctx.GetAmbient().Services.GetRequiredService<IWorkingDirectoryAccessService>();

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["load"] = new LuaCallbackFunction(Load);
			ns["load_base64"] = new LuaCallbackFunction(LoadBase64);
			ns["load_url"] = new LuaCallbackFunction(LoadUrl);
			ns["create"] = new LuaCallbackFunction(Create);
			ns["from_bytes"] = new LuaCallbackFunction(FromBytes);
			ns["info"] = new LuaCallbackFunction(Info);
			ns["formats"] = new LuaCallbackFunction(Formats);
			return null;
		}

		private LuaTuple Load(LuaCallingContext ctx, LuaValue[] args)
		{
			try
			{
				if (args.Length < 1)
					throw new LuaRuntimeException("image.load(path): at least 1 argument expected.");
				if (args[0] is not LuaString pathVal || string.IsNullOrEmpty(pathVal.Value))
					throw new LuaRuntimeException("image.load(path): path must be a non-empty string.");

				var luaImage = LuaImage.Load(FileAccess(ctx).GetFullPath(pathVal.Value));
				return new LuaTuple(LuaImage.Wrap(luaImage));
			}
			catch (LuaRuntimeException) { throw; }
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"image.load() error: {ex.Message}");
			}
		}

		private LuaTuple LoadBase64(LuaCallingContext ctx, LuaValue[] args)
		{
			try
			{
				if (args.Length < 1)
					throw new LuaRuntimeException("image.load_base64(base64): at least 1 argument expected.");
				if (args[0] is not LuaString base64Val)
					throw new LuaRuntimeException("image.load_base64(): first argument must be a string (base64).");

				var luaImage = LuaImage.LoadBase64(base64Val.Value);
				return new LuaTuple(LuaImage.Wrap(luaImage));
			}
			catch (LuaRuntimeException) { throw; }
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"image.load_base64() error: {ex.Message}");
			}
		}

		private async Task<LuaTuple> LoadUrl(LuaCallingContext ctx, LuaValue[] args)
		{
			try
			{
				if (args.Length < 1)
					throw new LuaRuntimeException("image.load_url(url): at least 1 argument expected.");
				if (args[0] is not LuaString urlVal)
					throw new LuaRuntimeException("image.load_url(): first argument must be a string (URL).");

				var luaImage = await LuaImage.LoadUrlAsync(urlVal.Value, ctx.CancellationToken);
				return new LuaTuple(LuaImage.Wrap(luaImage));
			}
			catch (LuaRuntimeException) { throw; }
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"image.load_url() error: {ex.Message}");
			}
		}

		private LuaTuple Create(LuaCallingContext ctx, LuaValue[] args)
		{
			try
			{
				if (args.Length < 2)
					throw new LuaRuntimeException("image.create(width, height, [color]): at least 2 arguments expected.");
				if (args[0] is not LuaNumber widthVal || args[1] is not LuaNumber heightVal)
					throw new LuaRuntimeException("image.create(): width and height must be numbers.");

				string? color = null;
				if (args.Length > 2 && args[2] is LuaString colorVal)
					color = colorVal.Value;

				var luaImage = LuaImage.Create((int)widthVal.Value, (int)heightVal.Value, color);
				return new LuaTuple(LuaImage.Wrap(luaImage));
			}
			catch (LuaRuntimeException) { throw; }
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"image.create() error: {ex.Message}");
			}
		}

		private LuaTuple FromBytes(LuaCallingContext ctx, LuaValue[] args)
		{
			try
			{
				if (args.Length < 1)
					throw new LuaRuntimeException("image.from_bytes(bytes, [format]): at least 1 argument expected.");
				if (args[0] is not LuaString bytesVal)
					throw new LuaRuntimeException("image.from_bytes(): first argument must be a string (binary data).");

				string? format = null;
				if (args.Length > 1 && args[1] is LuaString formatVal)
					format = formatVal.Value;

				// Lua string as raw byte data
				var bytes = System.Text.Encoding.UTF8.GetBytes(bytesVal.Value);
				var luaImage = LuaImage.FromBytes(bytes, format);
				return new LuaTuple(LuaImage.Wrap(luaImage));
			}
			catch (LuaRuntimeException) { throw; }
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"image.from_bytes() error: {ex.Message}");
			}
		}

		private LuaTuple Info(LuaCallingContext ctx, LuaValue[] args)
		{
			try
			{
				if (args.Length < 1)
					throw new LuaRuntimeException("image.info(path): at least 1 argument expected.");
				if (args[0] is not LuaString pathVal)
					throw new LuaRuntimeException("image.info(): first argument must be a string (path).");

				var fullPath = FileAccess(ctx).GetFullPath(pathVal.Value);
				var info = LuaImage.GetInfo(fullPath);
				return new LuaTuple(info);
			}
			catch (LuaRuntimeException) { throw; }
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"image.info() error: {ex.Message}");
			}
		}

		private LuaTuple Formats(LuaCallingContext ctx, LuaValue[] args)
		{
			try
			{
				return new LuaTuple(LuaImage.GetFormats());
			}
			catch (LuaRuntimeException) { throw; }
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"image.formats() error: {ex.Message}");
			}
		}
	}
}
