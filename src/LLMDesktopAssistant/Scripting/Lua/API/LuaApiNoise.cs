using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Utils.Noise;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for procedural noise: <c>noise.*</c>.
	/// Creates generators that sample a field or rasterize it into an image.
	/// </summary>
	[LuaApi(chatScoped: true)]
	public class LuaApiNoise : LuaApiBase
	{
		public override string? Namespace => "noise";

		public override string? Manuals => """
			--- noise - procedural noise API

			Creates procedural noise generators: simplex, cellular (Voronoi), value, white,
			sine, checkerboard and Gabor. A generator samples a scalar field with
			:generate() and rasterizes it into an image with :render().

			FACTORIES:

			--- noise.simplex([params]) -> noise
			--- noise.voronoi([params]) -> noise
			--- noise.value([params]) -> noise
			--- noise.white([params]) -> noise
			--- noise.sine([params]) -> noise
			--- noise.checkerboard([params]) -> noise
			--- noise.gabor([params]) -> noise
			  Creates a generator. All factories accept the common parameters below.

			COMMON PARAMETERS:
			    seed: number - seed of the generator (default: random)
			    scale: number - zoom applied to every coordinate; larger = more detail (default 1)
			    scale_x, scale_y, scale_z, scale_w: number - per-axis zoom
			    octaves: number - fractal octaves, 0 disables fractal summation (default 0)
			    fractal: string - "fbm" (default), "billow", "ridged", "multifractal",
			             "hybrid", "heterogeneous"
			    persistence: number - amplitude multiplier per octave (default 0.5)
			    lacunarity: number - frequency multiplier per octave (default 2.0)
			    gain, offset, h, lacunarity_offset: number - tuning for the ridged, hybrid
			             and heterogeneous fractal types
			    distortion: number - domain warping strength, 0 disables it (default 0)
			    distortion_depth: number - warping recursion depth (default 1)
			    distortion_noise: noise - generator used for warping (default: simplex)

			VORONOI PARAMETERS:
			    metric: string - "euclidean" (default), "manhattan", "chebyshev", "quadratic"
			    feature: string - "f1" (default), "f2", "f2-f1", "average", "product", "random"
			    points_per_cell: number - feature points per cell (default 1)

			CHECKERBOARD PARAMETERS:
			    randomization: number - 0 = perfect checkerboard, 1 = random cells (default 0)

			GABOR PARAMETERS:
			    k: number - width of the Gaussian envelope, 0.5..2.0 (default 1)
			    frequency: number - stripes per blob (default 2)
			    rotation: number - orientation in radians, used when rotation is not randomized
			    randomize_rotation: boolean - random orientation per kernel (default true)

			NOISE HANDLE PROPERTIES (read/write):
			    n.type: string - factory family ("simplex", "voronoi", ...)
			    n.seed, n.scale, n.scale_x, n.scale_y, n.scale_z, n.scale_w, n.octaves,
			    n.persistence, n.lacunarity, n.gain, n.offset, n.h, n.lacunarity_offset,
			    n.distortion, n.distortion_depth: same meaning as the factory parameters
			    n.fractal: string - fractal type name

			NOISE HANDLE METHODS:

			--- n:generate(x[, y[, z[, w]]]) -> number
			  Samples the field, 1D to 4D depending on how many coordinates are given.
			  Returns a value in roughly -1..1: fractal octaves and a few generators can overshoot
			  it a little, so treat the bounds as approximate.

			--- n:render([options]) -> image
			  Rasterizes a window of the field into a new image (see the image API). The
			  window defaults to one noise unit per pixel, so the picture size and the covered
			  area are the same until rect says otherwise.
			    width: number - output pixels (default 256, max 4096)
			    height: number - output pixels (default 256, max 4096)
			    rect: table - window of noise space to draw:
			        { x = 0, y = 0, width = <image width>, height = <image height> }
			        Its width and height are noise units, not pixels, so they set the zoom:
			        a rect larger than the picture squeezes more field into it, a smaller one
			        magnifies it. Aspect ratio is not preserved, the rect is stretched onto
			        the picture.
			    x, y: number - shorthand for rect.x and rect.y with a rect the size of the picture
			    normalize: boolean - stretch the sampled min..max onto the whole gradient
			    palette: array of hex colors - evenly spaced gradient stops;
			             no palette means grayscale
			  Returns: image handle

			--- noise.generators() -> table
			  Lists the factory names.
			--- noise.fractals() -> table
			  Lists the fractal type names.
			--- noise.metrics() -> table
			  Lists the Voronoi distance metrics.
			--- noise.features() -> table
			  Lists the Voronoi features.

			EXAMPLES:

			  -- Terrain-like slice, saved to disk
			  local n = noise.simplex{ seed = 1337, scale = 0.01, octaves = 5, fractal = "ridged" }
			  local img = n:render{
			      width = 512, height = 512,
			      normalize = true,
			      palette = { "#0b1026", "#2b4c7e", "#c7d8e8", "#ffffff" }
			  }
			  img:save("terrain.png")

			  -- A 40x10 slice of noise space spread over 800x200 pixels
			  local field = noise.simplex{ seed = 7, octaves = 4 }
			  local panorama = field:render{
			      width = 800, height = 200,
			      rect = { x = 120, y = 40, width = 40, height = 10 },
			      normalize = true,
			      palette = { "#101018", "#f0e6d2" }
			  }

			  -- Cellular pattern shown in a dynamic control
			  local cells = noise.voronoi{ scale = 0.05, feature = "f2-f1" }
			  dass.tool.result.append_data(dass.ui.create_control([==[
				<Image xmlns="https://github.com/avaloniaui" Source="{Binding map}" Stretch="Uniform"/>
			  ]==], { map = cells:render{ width = 256, height = 256,
			      palette = { "#000000", "#ff7a00", "#ffe0a3" } } }))

			  -- Sample by hand
			  local worms = noise.gabor{ scale = 0.02 }
			  print(worms:generate(12.5, 40.25))

			NOTES:
			  - Sampled values are roughly -1..1. voronoi stays close to 0 (F1 is mostly negative),
				so its renders almost always want normalize = true.
			  - Normalization only affects :render().
			  - The result already is a regular image handle, so :save(), attachments and the dynamic
			    UI treat it exactly like image.load() output.
			  - A noise handle is a live generator, not a persisted value: rebuild it in the
			    script instead of storing it in a dynamic view model.
			""";

		/// <summary>
		/// Registers the factories and the enumerations of the noise API.
		/// </summary>
		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["simplex"] = new LuaCallbackFunction(Simplex);
			ns["voronoi"] = new LuaCallbackFunction(Voronoi);
			ns["value"] = new LuaCallbackFunction(Value);
			ns["white"] = new LuaCallbackFunction(White);
			ns["sine"] = new LuaCallbackFunction(Sine);
			ns["checkerboard"] = new LuaCallbackFunction(Checkerboard);
			ns["gabor"] = new LuaCallbackFunction(Gabor);
			ns["generators"] = new LuaCallbackFunction(Generators);
			ns["fractals"] = new LuaCallbackFunction(Fractals);
			ns["metrics"] = new LuaCallbackFunction(Metrics);
			ns["features"] = new LuaCallbackFunction(Features);
			return null;
		}

		/// <summary>
		/// Creates a simplex generator.
		/// </summary>
		private LuaTuple Simplex(LuaCallingContext ctx, LuaValue[] args)
			=> Wrap(new SimplexNoise(), "simplex", args);

		/// <summary>
		/// Creates a Voronoi generator, applying its metric, feature and density.
		/// </summary>
		private LuaTuple Voronoi(LuaCallingContext ctx, LuaValue[] args)
		{
			var noise = new VoronoiNoise();
			if (ReadParameters(args) is { } parameters)
			{
				noise.Metric = ParseMetric(parameters.Get("metric"));
				noise.Feature = ParseFeature(parameters.Get("feature"));
				if (parameters.Get("points_per_cell") is LuaNumber points)
					noise.PointsPerCell = Math.Max(1, (int)points.Value);
			}

			return Wrap(noise, "voronoi", args);
		}

		/// <summary>
		/// Creates a value noise generator.
		/// </summary>
		private LuaTuple Value(LuaCallingContext ctx, LuaValue[] args)
			=> Wrap(new ValueNoise(), "value", args);

		/// <summary>
		/// Creates a white noise generator.
		/// </summary>
		private LuaTuple White(LuaCallingContext ctx, LuaValue[] args)
			=> Wrap(new WhiteNoise(), "white", args);

		/// <summary>
		/// Creates a sine noise generator.
		/// </summary>
		private LuaTuple Sine(LuaCallingContext ctx, LuaValue[] args)
			=> Wrap(new SineNoise(), "sine", args);

		/// <summary>
		/// Creates a checkerboard generator, applying its randomization factor.
		/// </summary>
		private LuaTuple Checkerboard(LuaCallingContext ctx, LuaValue[] args)
		{
			var noise = new CheckerboardNoise();
			if (ReadParameters(args) is { } parameters && parameters.Get("randomization") is LuaNumber randomization)
				noise.Randomization = Math.Clamp(randomization.Value, 0.0, 1.0);

			return Wrap(noise, "checkerboard", args);
		}

		/// <summary>
		/// Creates a Gabor generator, applying its kernel parameters.
		/// </summary>
		private LuaTuple Gabor(LuaCallingContext ctx, LuaValue[] args)
		{
			var noise = new GaborNoise();
			if (ReadParameters(args) is { } parameters)
			{
				if (parameters.Get("k") is LuaNumber k)
					noise.K = k.Value;
				if (parameters.Get("frequency") is LuaNumber frequency)
					noise.Frequency = frequency.Value;
				if (parameters.Get("rotation") is LuaNumber rotation)
					noise.Rotation = rotation.Value;
				if (parameters.Get("randomize_rotation") is LuaBoolean randomizeRotation)
					noise.RandomizeRotation = randomizeRotation.Value;
			}

			return Wrap(noise, "gabor", args);
		}

		/// <summary>
		/// Lists the factory names.
		/// </summary>
		private LuaTuple Generators(LuaCallingContext ctx, LuaValue[] args)
			=> new(BuildList("simplex", "voronoi", "value", "white", "sine", "checkerboard", "gabor"));

		/// <summary>
		/// Lists the fractal type names.
		/// </summary>
		private LuaTuple Fractals(LuaCallingContext ctx, LuaValue[] args)
			=> new(BuildList("fbm", "billow", "ridged", "multifractal", "hybrid", "heterogeneous"));

		/// <summary>
		/// Lists the Voronoi distance metrics.
		/// </summary>
		private LuaTuple Metrics(LuaCallingContext ctx, LuaValue[] args)
			=> new(BuildList("euclidean", "manhattan", "chebyshev", "quadratic"));

		/// <summary>
		/// Lists the Voronoi features.
		/// </summary>
		private LuaTuple Features(LuaCallingContext ctx, LuaValue[] args)
			=> new(BuildList("f1", "f2", "f2-f1", "average", "product", "random"));

		/// <summary>
		/// Applies the parameters shared by every generator and wraps the result as user data.
		/// </summary>
		private LuaTuple Wrap(NoiseBase noise, string type, LuaValue[] args)
		{
			if (ReadParameters(args) is { } parameters)
				ApplyCommon(noise, parameters);

			return new LuaTuple(LuaValueConverter.ToLuaValue(new LuaNoise(noise, type)));
		}

		/// <summary>
		/// Returns the parameter table, when the call passed one.
		/// </summary>
		private static LuaTable? ReadParameters(LuaValue[] args)
			=> args.Length > 0 ? args[0] as LuaTable : null;

		/// <summary>
		/// Copies the parameters every generator understands onto the instance.
		/// </summary>
		private static void ApplyCommon(NoiseBase noise, LuaTable parameters)
		{
			if (parameters.Get("seed") is LuaNumber seed)
				noise.Seed = (int)seed.Value;
			if (parameters.Get("scale") is LuaNumber scale)
				noise.Scale = scale.Value;
			if (parameters.Get("scale_x") is LuaNumber scaleX)
				noise.ScaleX = scaleX.Value;
			if (parameters.Get("scale_y") is LuaNumber scaleY)
				noise.ScaleY = scaleY.Value;
			if (parameters.Get("scale_z") is LuaNumber scaleZ)
				noise.ScaleZ = scaleZ.Value;
			if (parameters.Get("scale_w") is LuaNumber scaleW)
				noise.ScaleW = scaleW.Value;
			if (parameters.Get("octaves") is LuaNumber octaves)
				noise.Octaves = Math.Max(0, (int)octaves.Value);
			if (parameters.Get("persistence") is LuaNumber persistence)
				noise.Persistence = persistence.Value;
			if (parameters.Get("lacunarity") is LuaNumber lacunarity)
				noise.Lacunarity = lacunarity.Value;
			if (parameters.Get("gain") is LuaNumber gain)
				noise.Gain = gain.Value;
			if (parameters.Get("offset") is LuaNumber offset)
				noise.Offset = offset.Value;
			if (parameters.Get("h") is LuaNumber h)
				noise.H = h.Value;
			if (parameters.Get("lacunarity_offset") is LuaNumber lacunarityOffset)
				noise.LacunarityOffset = lacunarityOffset.Value;
			if (parameters.Get("fractal") is LuaString fractal)
				noise.FractalType = LuaNoise.ParseFractal(fractal.Value);
			if (parameters.Get("distortion") is LuaNumber distortion)
				noise.Distortion = distortion.Value;
			if (parameters.Get("distortion_depth") is LuaNumber distortionDepth)
				noise.MaxDistortionRecursionDepth = Math.Max(0, (int)distortionDepth.Value);
			if (parameters.Get("distortion_noise") is LuaUserData { Target: LuaNoise distortionNoise })
				noise.DistortionNoise = distortionNoise.Source;
		}

		/// <summary>
		/// Maps a Lua-facing metric name onto the enum, falling back to Euclidean.
		/// </summary>
		private static VoronoiDistanceMetric ParseMetric(LuaValue? value)
		{
			return value is LuaString metric
				? metric.Value.Trim().ToLowerInvariant() switch
				{
					"manhattan" => VoronoiDistanceMetric.Manhattan,
					"chebyshev" => VoronoiDistanceMetric.Chebyshev,
					"quadratic" => VoronoiDistanceMetric.Quadratic,
					_ => VoronoiDistanceMetric.Euclidean
				}
				: VoronoiDistanceMetric.Euclidean;
		}

		/// <summary>
		/// Maps a Lua-facing feature name onto the enum, falling back to F1.
		/// </summary>
		private static VoronoiFeature ParseFeature(LuaValue? value)
		{
			return value is LuaString feature
				? feature.Value.Trim().ToLowerInvariant().Replace(" ", "") switch
				{
					"f2" => VoronoiFeature.F2,
					"f2-f1" or "f2minusf1" => VoronoiFeature.F2MinusF1,
					"average" or "avg" => VoronoiFeature.Average,
					"product" => VoronoiFeature.Product,
					"random" or "randomvalue" or "rnd" => VoronoiFeature.RandomValue,
					_ => VoronoiFeature.F1
				}
				: VoronoiFeature.F1;
		}

		/// <summary>
		/// Builds a Lua array out of the given strings.
		/// </summary>
		private static LuaTable BuildList(params string[] items)
		{
			var table = new LuaTable();
			for (var index = 0; index < items.Length; index++)
				table.Set(index + 1, new LuaString(items[index]));

			return table;
		}
	}
}
