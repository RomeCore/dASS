namespace LLMDesktopAssistant.Utils.Noise
{
	/// <summary>
	/// Generates a checkerboard pattern alternating between -1 and 1.
	/// Useful for debugging, tile generation, or creating "digital" looking noise structures when combined with fractals.
	/// </summary>
	public class CheckerboardNoise : NoiseBase
	{
		/// <summary>
		/// Gets or sets the randomization factor in range [0, 1].
		/// 0.0 = Perfect checkerboard.
		/// 1.0 = Pure white noise (random cells).
		/// Values in between replace a percentage of cells with random values.
		/// </summary>
		public double Randomization { get; set; } = 0.0;

		protected override double GenerateCore(double x)
		{
			int ix = (int)Math.Floor(x);
			return GetValue(ix, 0, 0, 0);
		}

		protected override double GenerateCore(double x, double y)
		{
			int ix = (int)Math.Floor(x);
			int iy = (int)Math.Floor(y);
			return GetValue(ix, iy, 0, 0);
		}

		protected override double GenerateCore(double x, double y, double z)
		{
			int ix = (int)Math.Floor(x);
			int iy = (int)Math.Floor(y);
			int iz = (int)Math.Floor(z);
			return GetValue(ix, iy, iz, 0);
		}

		protected override double GenerateCore(double x, double y, double z, double w)
		{
			int ix = (int)Math.Floor(x);
			int iy = (int)Math.Floor(y);
			int iz = (int)Math.Floor(z);
			int iw = (int)Math.Floor(w);
			return GetValue(ix, iy, iz, iw);
		}

		/// <summary>
		/// Calculates the value for a cell based on coordinates and randomization settings.
		/// </summary>
		private double GetValue(int x, int y, int z, int w)
		{
			int sum = x + y + z + w;
			if (Randomization <= 0.0)
			{
				return (sum & 1) == 0 ? 1.0 : -1.0;
			}

			int h = Seed;
			h ^= x * 374761393;
			h ^= y * 668265263;
			h ^= z * 437985223;
			h ^= w * 534795221;

			h = (h ^ h >> 13) * 1274126177;
			h ^= h >> 16;

			double probability = (h & 0x7FFFFFFF) / (double)int.MaxValue;

			if (probability < Randomization)
			{
				return (h & 1) == 0 ? 1.0 : -1.0;
			}
			else
			{
				return (sum & 1) == 0 ? 1.0 : -1.0;
			}
		}
	}
}