using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Utils.Noise;
using SixLabors.ImageSharp.PixelFormats;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// A procedural noise generator exposed to Lua as user data: <c>noise:generate(...)</c> samples it and <c>noise:render{...}</c> rasterizes it.
	/// </summary>
	public sealed class LuaNoise(NoiseBase noise, string type)
	{
		private const int DefaultRenderSide = 256;
		private const int MaxRenderSide = 4096;

		/// <summary>
		/// Gets the factory family this object was created from: <c>simplex</c>, <c>voronoi</c> and so on.
		/// </summary>
		public string Type => type;

		/// <summary>
		/// Gets the wrapped generator, so the API factories can configure parameters that are specific to one family.
		/// </summary>
		[LuaHidden]
		public NoiseBase Source => noise;

		/// <summary>
		/// Gets or sets the seed; assigning a new one rebuilds whatever state the generator derives from it.
		/// </summary>
		public int Seed
		{
			get => noise.Seed;
			set => noise.Seed = value;
		}

		/// <summary>
		/// Gets or sets the zoom factor applied to every coordinate at once.
		/// </summary>
		public double Scale
		{
			get => noise.ScaleX;
			set => noise.Scale = value;
		}

		/// <summary>
		/// Gets or sets the zoom factor applied to the x coordinate.
		/// </summary>
		public double ScaleX
		{
			get => noise.ScaleX;
			set => noise.ScaleX = value;
		}

		/// <summary>
		/// Gets or sets the zoom factor applied to the y coordinate.
		/// </summary>
		public double ScaleY
		{
			get => noise.ScaleY;
			set => noise.ScaleY = value;
		}

		/// <summary>
		/// Gets or sets the zoom factor applied to the z coordinate.
		/// </summary>
		public double ScaleZ
		{
			get => noise.ScaleZ;
			set => noise.ScaleZ = value;
		}

		/// <summary>
		/// Gets or sets the zoom factor applied to the w coordinate.
		/// </summary>
		public double ScaleW
		{
			get => noise.ScaleW;
			set => noise.ScaleW = value;
		}

		/// <summary>
		/// Gets or sets the domain warping strength; zero disables warping.
		/// </summary>
		public double Distortion
		{
			get => noise.Distortion;
			set => noise.Distortion = value;
		}

		/// <summary>
		/// Gets or sets the maximum recursion depth of domain warping.
		/// </summary>
		public int DistortionDepth
		{
			get => noise.MaxDistortionRecursionDepth;
			set => noise.MaxDistortionRecursionDepth = value;
		}

		/// <summary>
		/// Gets or sets the fractal summation type by its Lua-facing name.
		/// </summary>
		public string Fractal
		{
			get => FractalNames[noise.FractalType];
			set => noise.FractalType = ParseFractal(value);
		}

		/// <summary>
		/// Gets or sets the number of fractal octaves; zero disables fractal summation.
		/// </summary>
		public int Octaves
		{
			get => noise.Octaves;
			set => noise.Octaves = Math.Max(0, value);
		}

		/// <summary>
		/// Gets or sets the amplitude multiplier applied per fractal octave.
		/// </summary>
		public double Persistence
		{
			get => noise.Persistence;
			set => noise.Persistence = value;
		}

		/// <summary>
		/// Gets or sets the frequency multiplier applied per fractal octave.
		/// </summary>
		public double Lacunarity
		{
			get => noise.Lacunarity;
			set => noise.Lacunarity = value;
		}

		/// <summary>
		/// Gets or sets the gain used by the ridged and hybrid fractal types.
		/// </summary>
		public double Gain
		{
			get => noise.Gain;
			set => noise.Gain = value;
		}

		/// <summary>
		/// Gets or sets the offset used by the ridged, hybrid and heterogeneous fractal types.
		/// </summary>
		public double Offset
		{
			get => noise.Offset;
			set => noise.Offset = value;
		}

		/// <summary>
		/// Gets or sets the roughness exponent of the heterogeneous fractal type.
		/// </summary>
		public double H
		{
			get => noise.H;
			set => noise.H = value;
		}

		/// <summary>
		/// Gets or sets the lacunarity offset of the heterogeneous fractal type.
		/// </summary>
		public double LacunarityOffset
		{
			get => noise.LacunarityOffset;
			set => noise.LacunarityOffset = value;
		}

		/// <summary>
		/// Explicit Lua-facing names of <see cref="FractalType"/>, since the enum spells them for C#.
		/// </summary>
		private static readonly Dictionary<FractalType, string> FractalNames = new()
		{
			[FractalType.fBM] = "fbm",
			[FractalType.Billow] = "billow",
			[FractalType.Ridged] = "ridged",
			[FractalType.MultiFractal] = "multifractal",
			[FractalType.HybridMultiFractal] = "hybrid",
			[FractalType.HeterogeneousTerrain] = "heterogeneous"
		};

		/// <summary>
		/// Maps a Lua-facing fractal name onto the enum, falling back to <see cref="FractalType.fBM"/>.
		/// </summary>
		[LuaHidden]
		public static FractalType ParseFractal(string? name)
		{
			return name?.Trim().ToLowerInvariant().Replace("_", "") switch
			{
				"billow" => FractalType.Billow,
				"ridged" => FractalType.Ridged,
				"multifractal" => FractalType.MultiFractal,
				"hybrid" or "hybridmultifractal" => FractalType.HybridMultiFractal,
				"heterogeneous" or "hetero" or "terrain" => FractalType.HeterogeneousTerrain,
				_ => FractalType.fBM
			};
		}

		/// <summary>
		/// Samples the field at one coordinate.
		/// </summary>
		public double Generate(double x) => noise.Generate(x);

		/// <summary>
		/// Samples the field at two coordinates.
		/// </summary>
		public double Generate(double x, double y) => noise.Generate(x, y);

		/// <summary>
		/// Samples the field at three coordinates.
		/// </summary>
		public double Generate(double x, double y, double z) => noise.Generate(x, y, z);

		/// <summary>
		/// Samples the field at four coordinates.
		/// </summary>
		public double Generate(double x, double y, double z, double w) => noise.Generate(x, y, z, w);

		/// <summary>
		/// Rasterizes a window of the field into a new image.
		/// </summary>
		/// <remarks>
		/// Reads <c>width</c>, <c>height</c>, <c>rect</c>, <c>x</c>, <c>y</c>, <c>normalize</c> and <c>palette</c> from the
		/// options table; every option has a default, so <c>n:render()</c> is a valid 256x256 grayscale picture covering
		/// one noise unit per pixel.
		/// </remarks>
		public LuaImage Render(LuaTable? options = null)
		{
			try
			{
				var width = ReadInt(options, "width", DefaultRenderSide);
				var height = ReadInt(options, "height", DefaultRenderSide);
				var region = ReadRegion(options, width, height);
				var normalize = options?.Get("normalize") is LuaBoolean { Value: true };
				var palette = ReadPalette(options);

				var image = NoiseRenderer.Render(noise, width, height, region, normalize, palette);
				return new LuaImage(image, "png");
			}
			catch (LuaRuntimeException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"noise:render() error: {ex.Message}");
			}
		}

		/// <summary>
		/// Reads an integer option and clamps it to the render side limits.
		/// </summary>
		private static int ReadInt(LuaTable? options, string key, int fallback)
		{
			if (options?.Get(key) is not LuaNumber number)
				return fallback;

			return (int)Math.Clamp(number.Value, 1.0, MaxRenderSide);
		}

		/// <summary>
		/// Reads the noise-space window: <c>rect = { x, y, width, height }</c> in full, or the <c>x</c> / <c>y</c> shorthand
		/// that moves a window the size of the picture.
		/// </summary>
		private static NoiseRenderRegion? ReadRegion(LuaTable? options, int width, int height)
		{
			if (options?.Get("rect") is LuaTable rect)
			{
				return new NoiseRenderRegion(
					ReadDouble(rect, "x"),
					ReadDouble(rect, "y"),
					ReadDouble(rect, "width", width),
					ReadDouble(rect, "height", height));
			}

			if (options?.Get("x") is not LuaNumber && options?.Get("y") is not LuaNumber)
				return null;

			return new NoiseRenderRegion(ReadDouble(options, "x"), ReadDouble(options, "y"), width, height);
		}

		/// <summary>
		/// Reads a floating point option, defaulting to the given fallback.
		/// </summary>
		private static double ReadDouble(LuaTable? options, string key, double fallback = 0.0)
			=> options?.Get(key) is LuaNumber number ? number.Value : fallback;

		/// <summary>
		/// Reads the optional gradient stops, aborting on a colour that cannot be parsed.
		/// </summary>
		private static Rgba32[]? ReadPalette(LuaTable? options)
		{
			if (options?.Get("palette") is not LuaTable colors)
				return null;

			var palette = new List<Rgba32>(colors.Length);
			for (var index = 1; index <= colors.Length; index++)
			{
				if (colors.Get(index) is not LuaString color)
					continue;

				palette.Add(ParseColor(color.Value));
			}

			return palette.Count == 0 ? null : [.. palette];
		}

		/// <summary>
		/// Parses "#rgb", "#rrggbb", "#rrggbbaa", "0x" prefixed variants and bare hex.
		/// </summary>
		private static Rgba32 ParseColor(string color)
		{
			var text = color.Trim();
			if (text.StartsWith('#'))
				text = text[1..];
			else if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
				text = text[2..];

			if (Rgba32.TryParseHex(text, out var parsed))
				return parsed;

			throw new LuaRuntimeException($"noise:render(): palette color '{color}' is not a hex color.");
		}
	}
}
