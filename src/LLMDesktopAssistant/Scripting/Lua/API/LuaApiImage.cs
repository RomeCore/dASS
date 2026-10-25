using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;

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
		Uses SixLabors.ImageSharp under the hood.
		All image objects are UserData with methods.

		FUNCTIONS:

		--- image.load(path) -> Image
		  Loads an image from a file path.
		  Parameters:
		    - path: string - path to the image file
		  Returns: Image object (UserData)

		--- image.load_base64(base64) -> Image
		  Loads an image from a base64-encoded string.
		  Parameters:
		    - base64: string - base64-encoded image data
		  Returns: Image object (UserData)

		--- image.load_url(url) -> Image
		  Loads an image from a URL.
		  Parameters:
		    - url: string - URL of the image
		  Returns: Image object (UserData)

		--- image.create(width, height, [color]) -> Image
		  Creates a new blank image.
		  Parameters:
		    - width: number - width in pixels
		    - height: number - height in pixels
		    - color: string (optional) - background color (hex "#ff0000", name "red", etc.)
		  Returns: Image object (UserData)

		--- image.from_bytes(bytes, [format]) -> Image
		  Creates an image from raw byte data.
		  Parameters:
		    - bytes: string (binary data as Lua string)
		    - format: string (optional) - image format hint
		  Returns: Image object (UserData)

		--- image.info(path) -> table
		  Gets image metadata without loading the full image.
		  Parameters:
		    - path: string - path to the image file
		  Returns: table with fields: width, height, format

		--- image.formats() -> table
		  Returns a list of supported image formats.
		  Returns: array of strings

		IMAGE OBJECT METHODS:

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
		      - mode: string - "stretch" (default), "contain" (pad), "cover" (crop)

		--- img:crop(x, y, width, height)
		  Crops the image to the specified rectangle.

		--- img:rotate(degrees)
		  Rotates the image by the specified degrees.

		--- img:flip(direction)
		  Flips the image.
		  Parameters:
		    - direction: string - "horizontal", "vertical", or "both"

		--- img:clone() -> Image
		  Creates a deep copy of the image.

		--- img:dispose()
		  Manually disposes the underlying image resources.
		  Automatically called by garbage collector.

		IMAGE OBJECT PROPERTIES (read-only):
		  - img.width: number - image width in pixels
		  - img.height: number - image height in pixels
		  - img.format: string - image format ("png", "jpg", "webp", etc.)

		EXAMPLES:

		  -- Load, resize and save
		  local img = image.load("photo.jpg")
		  img:resize(800, 600, { mode = "cover" })
		  img:save("thumb.jpg", { quality = 80 })

		  -- Load from URL and attach to message
		  local img = image.load_url("https://example.com/chart.png")
		  local system_message = {
		      role = "system",
		      content = "You are a helpful assistant."
		  }
		  local user_message = {
		      role = "user",
		      content = "Check out this chart!",
		      attachments = { img }
		  }
		  local response_messages = dass.agents.execute({ system_message, user_message })

		  -- Create an image from scratch
		  local img = image.create(100, 100, "#ff0000")
		  img:save("red_square.png")

		  -- Get metadata without full load
		  local info = image.info("large.png")
		  print(info.width, info.height, info.format)

		NOTES:
		  - Image objects must be disposed manually with :dispose() or will be
		    disposed automatically by Lua garbage collector.
		  - Supported formats: png, jpg/jpeg, webp, gif, bmp
		  - base64 format is auto-detected from file header bytes.
		  - All manipulation methods modify the image in-place. Use :clone() first
		    if you need to preserve the original.
		""";

		private readonly IWorkingDirectoryAccessService _fileAccess;

		public LuaApiImage(IWorkingDirectoryAccessService fileAccess)
		{
			_fileAccess = fileAccess;
		}

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

				var luaImage = LuaImage.Load(_fileAccess, pathVal.Value);
				return new LuaTuple(LuaValueConverter.ToLuaValue(luaImage));
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

				var luaImage = LuaImage.LoadBase64(_fileAccess, base64Val.Value);
				return new LuaTuple(LuaValueConverter.ToLuaValue(luaImage));
			}
			catch (LuaRuntimeException) { throw; }
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"image.load_base64() error: {ex.Message}");
			}
		}

		private LuaTuple LoadUrl(LuaCallingContext ctx, LuaValue[] args)
		{
			try
			{
				if (args.Length < 1)
					throw new LuaRuntimeException("image.load_url(url): at least 1 argument expected.");
				if (args[0] is not LuaString urlVal)
					throw new LuaRuntimeException("image.load_url(): first argument must be a string (URL).");

				var luaImage = LuaImage.LoadUrl(_fileAccess, urlVal.Value);
				return new LuaTuple(LuaValueConverter.ToLuaValue(luaImage));
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

				var luaImage = LuaImage.Create(_fileAccess, (int)widthVal.Value, (int)heightVal.Value, color);
				return new LuaTuple(LuaValueConverter.ToLuaValue(luaImage));
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
				var luaImage = LuaImage.FromBytes(_fileAccess, bytes, format);
				return new LuaTuple(LuaValueConverter.ToLuaValue(luaImage));
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

				var fullPath = _fileAccess.AccessPath(pathVal.Value, DirectoryAccessMode.Read);
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
