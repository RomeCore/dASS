namespace LLMDesktopAssistant.Utils.Noise
{
	/// <summary>
	/// Deterministic White Noise (Value Noise without interpolation).
	/// Returns a pseudo-random value for every integer coordinate unit.
	/// Useful for dithering, star fields, or as a raw source for other algorithms.
	/// </summary>
	public class WhiteNoise : NoiseBase
	{
		private const int PrimeX = 501125321;
		private const int PrimeY = 1136930381;
		private const int PrimeZ = 1720413743;
		private const int PrimeW = 1066037191;

		private double GetHash(int x, int y, int z, int w)
		{
			int hash = Seed;
			hash ^= x * PrimeX;
			hash ^= y * PrimeY;
			hash ^= z * PrimeZ;
			hash ^= w * PrimeW;

			hash *= 0x27d4eb2d;
			hash ^= (hash << 13) ^ (hash >> 15);
			hash *= 0x27d4eb2d;
			hash ^= (hash << 13) ^ (hash >> 15);

			int positiveHash = hash & 0x7FFFFFFF;
			double normalized = (double)positiveHash / int.MaxValue;
			return normalized * 2.0 - 1.0;
		}

		private static int DHash(double f)
		{
			long bits = BitConverter.DoubleToInt64Bits(f);
			var packed = bits ^ (bits >> 32);
			return (int)(packed & 0x7FFFFFFF);
		}

		public WhiteNoise() : base()
		{
		}

		public WhiteNoise(int seed) : base(seed)
		{
		}

		protected override double GenerateCore(double x)
		{
			return GetHash(DHash(x), 0, 0, 0);
		}

		protected override double GenerateCore(double x, double y)
		{
			return GetHash(DHash(x), DHash(y), 0, 0);
		}

		protected override double GenerateCore(double x, double y, double z)
		{
			return GetHash(DHash(x), DHash(y), DHash(z), 0);
		}

		protected override double GenerateCore(double x, double y, double z, double w)
		{
			return GetHash(DHash(x), DHash(y), DHash(z), DHash(w));
		}
	}
}