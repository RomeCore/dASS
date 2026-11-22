namespace LLMDesktopAssistant.Utils.Noise
{
	/// <summary>
	/// Type of fractal noise algorithm to apply.
	/// </summary>
	public enum FractalType
	{
		/// <summary>
		/// Standard fractional Brownian motion. The most common fractal noise type.
		/// </summary>
		fBM,

		/// <summary>
		/// Billow noise that creates cloud-like, billowy patterns.
		/// </summary>
		Billow,

		/// <summary>
		/// Ridged multi-fractal noise that creates sharp, mountain-like ridges.
		/// </summary>
		Ridged,

		/// <summary>
		/// Multi-fractal noise with variable roughness and detail.
		/// </summary>
		MultiFractal,

		/// <summary>
		/// Hybrid multi-fractal noise that combines fBM and ridged characteristics.
		/// </summary>
		HybridMultiFractal,

		/// <summary>
		/// Heterogeneous terrain-like fractal noise.
		/// </summary>
		HeterogeneousTerrain
	}

	/// <summary>
	/// Base class for generating various types of noise.
	/// </summary>
	public abstract class NoiseBase
	{
		/// <summary>
		/// Gets or sets the scale factor applied to the x coordinate in noise space.
		/// </summary>
		public double ScaleX { get; set; }

		/// <summary>
		/// Gets or sets the scale factor applied to the y coordinate in noise space.
		/// </summary>
		public double ScaleY { get; set; }

		/// <summary>
		/// Gets or sets the scale factor applied to the z coordinate in noise space.
		/// </summary>
		public double ScaleZ { get; set; }

		/// <summary>
		/// Gets or sets the scale factor applied to the w coordinate in noise space.
		/// </summary>
		public double ScaleW { get; set; }

		/// <summary>
		/// Sets a single scale factor applied to all coordinates in noise space.
		/// </summary>
		public double Scale { set => ScaleX = ScaleY = ScaleZ = ScaleW = value; }

		/// <summary>
		/// Gets or sets distortion factor applied to the noise, which can create more complex patterns.
		/// </summary>
		public double Distortion { get; set; }

		/// <summary>
		/// Gets or sets the maximum distortion recursion depth. Useful when noise call itself for domain warping to distort output noise. Defaults to 1.
		/// </summary>
		public int MaxDistortionRecursionDepth { get; set; }

		/// <summary>
		/// Gets or sets the noise generator used for distortion. Defaults to SimplexNoise if not specified.
		/// </summary>
		public NoiseBase? DistortionNoise { get; set; }

		/// <summary>
		/// Gets or sets the type of fractal algorithm to use for multi-octave noise generation.
		/// </summary>
		public FractalType FractalType { get; set; }

		/// <summary>
		/// Gets or sets the number of octaves (layers) of noise to combine for fractal noise generation. Set to 0 to disable fractal noise.
		/// </summary>
		public int Octaves { get; set; }

		/// <summary>
		/// Gets or sets the amplitude multiplier for each subsequent octave for fractal noise generation.
		/// </summary>
		public double Persistence { get; set; }

		/// <summary>
		/// Gets or sets the frequency multiplier for each subsequent octave for fractal noise generation.
		/// </summary>
		public double Lacunarity { get; set; }

		/// <summary>
		/// Gets or sets the gain factor used in certain fractal types like HybridMultiFractal.
		/// </summary>
		public double Gain { get; set; }

		/// <summary>
		/// Gets or sets the offset value used in certain fractal types like Ridged and HybridMultiFractal.
		/// </summary>
		public double Offset { get; set; }

		/// <summary>
		/// Gets or sets the H parameter (roughness) for heterogeneous terrain fractal type.
		/// </summary>
		public double H { get; set; }

		/// <summary>
		/// Gets or sets the lacunarity offset for heterogeneous terrain fractal type.
		/// </summary>
		public double LacunarityOffset { get; set; }

		private int? _seed;
		/// <summary>
		/// Gets or sets the seed for the noise generation.
		/// </summary>
		public int Seed
		{
			get
			{
				return _seed ?? 0;
			}
			set
			{
				if (value != _seed)
				{
					_seed = value;
					OnSeedSet(value);
				}
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="NoiseBase"/> class with default parameters.
		/// </summary>
		public NoiseBase() : this(Random.Shared.Next())
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="NoiseBase"/> class with default parameters.
		/// </summary>
		public NoiseBase(int seed)
		{
			Scale = 1.0;
			Distortion = 0.0;
			MaxDistortionRecursionDepth = 1;
			FractalType = FractalType.fBM;
			Octaves = 0;
			Persistence = 0.5;
			Lacunarity = 2.0;
			Gain = 1.0;
			Offset = 1.0;
			H = 1.0;
			LacunarityOffset = 0.0;
			Seed = seed;
		}

		/// <summary>
		/// Sets the seed for the random number generator used by the noise algorithm.
		/// </summary>
		/// <param name="seed">The seed value to use for the random number generator.</param>
		protected virtual void OnSeedSet(int seed) { }

		/// <summary>
		/// Generates 1D noise.
		/// </summary>
		/// <param name="x">The x coordinate in noise space.</param>
		/// <returns>Noise value in range [-1, 1].</returns>
		protected abstract double GenerateCore(double x);

		/// <summary>
		/// Generates 2D noise.
		/// </summary>
		/// <param name="x">The x coordinate in noise space.</param>
		/// <param name="y">The y coordinate in noise space.</param>
		/// <returns>Noise value in range [-1, 1].</returns>
		protected abstract double GenerateCore(double x, double y);

		/// <summary>
		/// Generates 3D noise.
		/// </summary>
		/// <param name="x">The x coordinate in noise space.</param>
		/// <param name="y">The y coordinate in noise space.</param>
		/// <param name="z">The z coordinate in noise space.</param>
		/// <returns>Noise value in range [-1, 1].</returns>
		protected abstract double GenerateCore(double x, double y, double z);

		/// <summary>
		/// Generates 4D noise.
		/// </summary>
		/// <param name="x">The x coordinate in noise space.</param>
		/// <param name="y">The y coordinate in noise space.</param>
		/// <param name="z">The z coordinate in noise space.</param>
		/// <param name="w">The w coordinate in noise space.</param>
		/// <returns>Noise value in range [-1, 1].</returns>
		protected abstract double GenerateCore(double x, double y, double z, double w);

		/// <summary>
		/// Generates 1D fractal noise.
		/// </summary>
		/// <param name="x">The x coordinate in noise space.</param>
		/// <returns>Combined and distorted noise value from multiple octaves ranged from -1 to 1.</returns>
		public double Generate(double x)
		{
			return Generate(x, MaxDistortionRecursionDepth);
		}

		/// <summary>
		/// Generates 2D fractal noise.
		/// </summary>
		/// <param name="x">The x coordinate in noise space.</param>
		/// <param name="y">The y coordinate in noise space.</param>
		/// <returns>Combined and distorted noise value from multiple octaves ranged from -1 to 1.</returns>
		public double Generate(double x, double y)
		{
			return Generate(x, y, MaxDistortionRecursionDepth);
		}

		/// <summary>
		/// Generates 3D fractal noise.
		/// </summary>
		/// <param name="x">The x coordinate in noise space.</param>
		/// <param name="y">The y coordinate in noise space.</param>
		/// <param name="z">The z coordinate in noise space.</param>
		/// <returns>Combined and distorted noise value from multiple octaves ranged from -1 to 1.</returns>
		public double Generate(double x, double y, double z)
		{
			return Generate(x, y, z, MaxDistortionRecursionDepth);
		}

		/// <summary>
		/// Generates 4D fractal noise.
		/// </summary>
		/// <param name="x">The x coordinate in noise space.</param>
		/// <param name="y">The y coordinate in noise space.</param>
		/// <param name="z">The z coordinate in noise space.</param>
		/// <param name="w">The w coordinate in noise space.</param>
		/// <returns>Combined and distorted noise value from multiple octaves ranged from -1 to 1.</returns>
		public double Generate(double x, double y, double z, double w)
		{
			return Generate(x, y, z, w, MaxDistortionRecursionDepth);
		}



		private double Generate(double x, int maxDistRecDepth)
		{
			x *= ScaleX;

			if (Distortion != 0 && maxDistRecDepth > 0)
			{
				maxDistRecDepth--;
				double dx;
				var distortionNoise = DistortionNoise;

				if (distortionNoise != null)
				{
					dx = distortionNoise.Generate(x + 1.3, maxDistRecDepth) * Distortion;
				}
				else
				{
					dx = SimplexNoise.GenerateHashed(x + 1.3, Seed) * Distortion;
				}

				x += dx;
			}

			if (Octaves < 1)
				return GenerateCore(x);

			return GenerateFractal(x, 0, 0, 0, 1);
		}

		private double Generate(double x, double y, int maxDistRecDepth)
		{
			x *= ScaleX; y *= ScaleY;

			if (Distortion != 0 && maxDistRecDepth > 0)
			{
				maxDistRecDepth--;
				double dx, dy;
				var distortionNoise = DistortionNoise;

				if (distortionNoise != null)
				{
					dx = distortionNoise.Generate(x + 1.3, y, maxDistRecDepth) * Distortion;
					dy = distortionNoise.Generate(x, y + 5.3, maxDistRecDepth) * Distortion;
				}
				else
				{
					dx = SimplexNoise.GenerateHashed(x + 1.3, y, Seed) * Distortion;
					dy = SimplexNoise.GenerateHashed(x, y + 5.3, Seed) * Distortion;
				}

				x += dx;
				y += dy;
			}

			if (Octaves < 1)
				return GenerateCore(x, y);

			return GenerateFractal(x, y, 0, 0, 2);
		}

		private double Generate(double x, double y, double z, int maxDistRecDepth)
		{
			x *= ScaleX; y *= ScaleY; z *= ScaleZ;

			if (Distortion != 0 && maxDistRecDepth > 0)
			{
				maxDistRecDepth--;
				double dx, dy, dz;
				var distortionNoise = DistortionNoise;
				
				if (distortionNoise != null)
				{
					dx = distortionNoise.Generate(x + 1.3, y, z, maxDistRecDepth) * Distortion;
					dy = distortionNoise.Generate(x, y + 5.3, z, maxDistRecDepth) * Distortion;
					dz = distortionNoise.Generate(x, y, z + 9.4, maxDistRecDepth) * Distortion;
				}
				else
				{
					dx = SimplexNoise.GenerateHashed(x + 1.3, y, z, Seed) * Distortion;
					dy = SimplexNoise.GenerateHashed(x, y + 5.3, z, Seed) * Distortion;
					dz = SimplexNoise.GenerateHashed(x, y, z + 9.4, Seed) * Distortion;
				}

				x += dx;
				y += dy;
				z += dz;
			}

			if (Octaves < 1)
				return GenerateCore(x, y, z);

			return GenerateFractal(x, y, z, 0, 3);
		}

		private double Generate(double x, double y, double z, double w, int maxDistRecDepth)
		{
			x *= ScaleX; y *= ScaleY; z *= ScaleZ; w *= ScaleW;

			if (Distortion != 0 && maxDistRecDepth > 0)
			{
				maxDistRecDepth--;
				double dx, dy, dz, dw;
				var distortionNoise = DistortionNoise;

				if (distortionNoise != null)
				{
					dx = distortionNoise.Generate(x + 1.3, y, z, w, maxDistRecDepth) * Distortion;
					dy = distortionNoise.Generate(x, y + 5.3, z, w, maxDistRecDepth) * Distortion;
					dz = distortionNoise.Generate(x, y, z + 9.4, w, maxDistRecDepth) * Distortion;
					dw = distortionNoise.Generate(x, y, z, w + 3.2, maxDistRecDepth) * Distortion;
				}
				else
				{
					dx = SimplexNoise.GenerateHashed(x + 1.3, y, z, w, Seed) * Distortion;
					dy = SimplexNoise.GenerateHashed(x, y + 5.3, z, w, Seed) * Distortion;
					dz = SimplexNoise.GenerateHashed(x, y, z + 9.4, w, Seed) * Distortion;
					dw = SimplexNoise.GenerateHashed(x, y, z, w + 3.2, Seed) * Distortion;
				}

				x += dx;
				y += dy;
				z += dz;
				w += dw;
			}

			if (Octaves < 1)
				return GenerateCore(x, y, z, w);

			return GenerateFractal(x, y, z, w, 4);
		}



		private double SampleCore(double x, double y, double z, double w, double freq, int dimensions)
		{
			return dimensions switch
			{
				1 => GenerateCore(x * freq),
				2 => GenerateCore(x * freq, y * freq),
				3 => GenerateCore(x * freq, y * freq, z * freq),
				4 => GenerateCore(x * freq, y * freq, z * freq, w * freq),
				_ => 0.0
			};
		}

		private const double Epsilon = 1e-12;

		private static double Clamp(double v, double lo, double hi)
			=> v < lo ? lo : (v > hi ? hi : v);

		private static double Clamp01(double v)
			=> v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);

		private static double ToSigned(double v01) // [0,1] -> [-1,1]
			=> (v01 * 2.0) - 1.0;

		private double SpectralWeight(int octave)
		{
			return Math.Pow(Lacunarity, -H * octave);
		}

		private double SumSpectralWeights(int octaves, int start = 0)
		{
			double sum = 0.0;
			for (int i = start; i < octaves; i++)
				sum += SpectralWeight(i);
			return sum;
		}

		private double RidgedSignal(double n)
		{
			double s = Offset - Math.Abs(n);
			s *= s;
			return s;
		}

		private double GenerateFractal(double x, double y, double z, double w, int dimensions)
		{
			switch (FractalType)
			{
				case FractalType.fBM:
					return FractalFBM(x, y, z, w, dimensions);

				case FractalType.Billow:
					return FractalBillow(x, y, z, w, dimensions);

				case FractalType.Ridged:
					return FractalRidged(x, y, z, w, dimensions);

				case FractalType.MultiFractal:
					return FractalMulti(x, y, z, w, dimensions);

				case FractalType.HybridMultiFractal:
					return FractalHybrid(x, y, z, w, dimensions);

				case FractalType.HeterogeneousTerrain:
					return FractalHetero(x, y, z, w, dimensions);

				default:
					return FractalFBM(x, y, z, w, dimensions);
			}
		}

		private double FractalFBM(double x, double y, double z, double w, int dimensions)
		{
			double sum = 0.0;
			double amp = 1.0;
			double freq = 1.0;
			double max = 0.0;

			for (int i = 0; i < Octaves; i++)
			{
				double n = SampleCore(x, y, z, w, freq, dimensions);
				sum += n * amp;
				max += amp;

				amp *= Persistence;
				freq *= Lacunarity;
			}

			return (max > Epsilon) ? (sum / max) : 0.0;
		}

		private double FractalBillow(double x, double y, double z, double w, int dimensions)
		{
			double sum = 0.0;
			double amp = 1.0;
			double freq = 1.0;
			double max = 0.0;

			for (int i = 0; i < Octaves; i++)
			{
				double n = SampleCore(x, y, z, w, freq, dimensions);
				double b = 2.0 * Math.Abs(n) - 1.0;

				sum += b * amp;
				max += amp;

				amp *= Persistence;
				freq *= Lacunarity;
			}

			return (max > Epsilon) ? (sum / max) : 0.0;
		}

		private double FractalRidged(double x, double y, double z, double w, int dimensions)
		{
			double sum = 0.0;
			double freq = 1.0;
			double weight = 1.0;

			double offset2 = Offset * Offset;
			double max = offset2 * SumSpectralWeights(Octaves);

			if (max < Epsilon)
				return 0.0;

			for (int i = 0; i < Octaves; i++)
			{
				double n = SampleCore(x, y, z, w, freq, dimensions);

				double s = RidgedSignal(n);
				s *= weight;

				double sw = SpectralWeight(i);
				sum += s * sw;

				weight = s * Gain;
				weight = Clamp01(weight);

				freq *= Lacunarity;
			}

			double v01 = Clamp01(sum / max);
			return ToSigned(v01);
		}

		private double FractalMulti(double x, double y, double z, double w, int dimensions)
		{
			double freq = 1.0;

			double result = 1.0;
			double pMin = 1.0;
			double pMax = 1.0;

			for (int i = 0; i < Octaves; i++)
			{
				double sw = SpectralWeight(i);
				double n = SampleCore(x, y, z, w, freq, dimensions);

				result *= (1.0 + n * sw);

				double a = 1.0 - sw;
				if (a < 0.0) a = 0.0;

				pMin *= a;
				pMax *= (1.0 + sw);

				freq *= Lacunarity;
			}

			double denom = pMax - pMin;
			if (denom < Epsilon)
				return 0.0;

			double v01 = (result - pMin) / denom;
			v01 = Clamp01(v01);
			return ToSigned(v01);
		}

		private double FractalHybrid(double x, double y, double z, double w, int dimensions)
		{
			double freq = 1.0;

			double max = (1.0 + Offset) * SumSpectralWeights(Octaves);
			if (max < Epsilon)
				return 0.0;

			double n0 = SampleCore(x, y, z, w, freq, dimensions);
			double w0 = SpectralWeight(0);

			double result = (n0 + Offset) * w0;

			double weight = result * Gain;
			weight = Clamp01(weight);

			freq *= Lacunarity;

			for (int i = 1; i < Octaves; i++)
			{
				double n = SampleCore(x, y, z, w, freq, dimensions);
				double wi = SpectralWeight(i);

				double signal = (n + Offset) * wi;
				result += weight * signal;

				weight *= Gain * signal;
				weight = Clamp01(weight);

				freq *= Lacunarity;
			}

			double v = result / max;
			return Clamp(v, -1.0, 1.0);
		}

		private double FractalHetero(double x, double y, double z, double w, int dimensions)
		{
			double freq = 1.0;

			double n0 = SampleCore(x, y, z, w, freq, dimensions);
			double value = Offset + n0;

			double sumW = SumSpectralWeights(Octaves, start: 1);
			double baseW = Offset + 1.0 + LacunarityOffset;
			if (baseW < 0.0) baseW = 0.0;

			double wMax = baseW + sumW;
			double maxAbs = Math.Abs(Offset) + 1.0 + (wMax * sumW);
			if (maxAbs < Epsilon)
				return 0.0;

			freq *= Lacunarity;

			for (int i = 1; i < Octaves; i++)
			{
				double n = SampleCore(x, y, z, w, freq, dimensions);
				double wi = SpectralWeight(i);

				double weight = value + LacunarityOffset;
				if (weight < 0.0) weight = 0.0;

				value += n * wi * weight;

				freq *= Lacunarity;
			}

			double v = value / maxAbs;
			return Clamp(v, -1.0, 1.0);
		}
	}
}