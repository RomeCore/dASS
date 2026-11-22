namespace LLMDesktopAssistant.Utils.Noise
{
	/// <summary>
	/// Generates Simplex noise, a faster and smoother alternative to Perlin noise.
	/// Provides 1D, 2D, and 3D noise functions with values typically ranging from -1 to 1.
	/// </summary>
	public partial class SimplexNoise : NoiseBase
	{
		private static readonly double _sqrt3;
		private static readonly double _sqrt5;
		private static readonly double _f2;
		private static readonly double _g2;
		private static readonly double _f3;
		private static readonly double _g3;
		private static readonly double _f4;
		private static readonly double _g4;

		/// <summary>
		/// Gets the shared instance of the <see cref="SimplexNoise"/> class.
		/// </summary>
		public static SimplexNoise Shared { get; }

		static SimplexNoise()
		{
			_sqrt3 = Math.Sqrt(3.0);
			_sqrt5 = Math.Sqrt(5.0);

			// Skewing and unskewing factors for 2D
			_f2 = 0.5 * (_sqrt3 - 1.0);
			_g2 = (3.0 - _sqrt3) / 6.0;

			// Skewing and unskewing factors for 3D
			_f3 = 1.0 / 3.0;
			_g3 = 1.0 / 6.0;

			// Skewing and unskewing factors for 4D
			_f4 = (_sqrt5 - 1.0) / 4.0;
			_g4 = (5.0 - _sqrt5) / 20.0;

			Shared = new SimplexNoise();
		}

		private static int FastFloor(double x)
		{
			int xi = (int)x;
			return x < xi ? xi - 1 : xi;
		}

		private static double Grad(int hash, double x)
		{
			int h = hash & 15;
			double grad = 1.0 + (h & 7);    // Gradient value is one of 1.0, 2.0, ..., 8.0
			if ((h & 8) != 0) grad = -grad; // Set a random sign for the gradient
			return grad * x;
		}

		private static double Grad(int hash, double x, double y)
		{
			int h = hash & 7;
			double u = h < 4 ? x : y;
			double v = h < 4 ? y : x;
			return ((h & 1) != 0 ? -u : u) + ((h & 2) != 0 ? -2.0 * v : 2.0 * v);
		}

		private static double Grad(int hash, double x, double y, double z)
		{
			int h = hash & 15;
			double u = h < 8 ? x : y;
			double v = h < 4 ? y : h == 12 || h == 14 ? x : z;
			return ((h & 1) != 0 ? -u : u) + ((h & 2) != 0 ? -v : v);
		}

		private static double Grad(int hash, double x, double y, double z, double w)
		{
			int h = hash & 31;
			double u = h < 24 ? x : y;
			double v = h < 16 ? y : z;
			double s = h < 8 ? z : w;
			return ((h & 1) != 0 ? -u : u) + ((h & 2) != 0 ? -v : v) + ((h & 4) != 0 ? -s : s);
		}

		protected override void OnSeedSet(int seed)
		{
			var random = new Random(seed);
			int[] perm = new int[256];

			for (int i = 0; i < 256; i++)
			{
				perm[i] = i;
			}

			// Shuffle the array
			random.Shuffle(perm);

			_permutation ??= new int[512];
			_p ??= new int[512];

			// Extend the permutation table
			for (int i = 0; i < 512; i++)
			{
				_p[i] = perm[i % 256];
				_permutation[i] = _p[i];
			}
		}

		private int[] _permutation = null!;
		private int[] _p = null!;

		/// <summary>
		/// Initializes a new instance of <see cref="SimplexNoise"/> class with default parameters.
		/// </summary>
		public SimplexNoise()
		{
		}

		/// <summary>
		/// Initializes a new instance of <see cref="SimplexNoise"/> class with specified seed and default fractal parameters.
		/// </summary>
		/// <param name="seed">The seed value to use for the random number generator.</param>
		public SimplexNoise(int seed) : base(seed)
		{
		}

		protected override double GenerateCore(double x)
		{
			int i0 = FastFloor(x);
			int i1 = i0 + 1;

			double x0 = x - i0;
			double x1 = x0 - 1.0;

			double t0 = 1.0 - x0 * x0;
			t0 *= t0;
			double n0 = t0 * t0 * Grad(_p[i0 & 0xFF], x0);

			double t1 = 1.0 - x1 * x1;
			t1 *= t1;
			double n1 = t1 * t1 * Grad(_p[i1 & 0xFF], x1);

			return 0.395 * (n0 + n1);
		}

		protected override double GenerateCore(double x, double y)
		{
			// Skew the input space to determine which simplex cell we're in
			double s = (x + y) * _f2;
			int i = FastFloor(x + s);
			int j = FastFloor(y + s);

			double t = (i + j) * _g2;
			double x0 = x - (i - t);
			double y0 = y - (j - t);

			// Determine which simplex we are in
			int i1, j1;
			if (x0 > y0)
			{
				i1 = 1;
				j1 = 0;
			}
			else
			{
				i1 = 0;
				j1 = 1;
			}

			double x1 = x0 - i1 + _g2;
			double y1 = y0 - j1 + _g2;
			double x2 = x0 - 1.0 + 2.0 * _g2;
			double y2 = y0 - 1.0 + 2.0 * _g2;

			// Work out the hashed gradient indices of the three simplex corners
			int ii = i & 255;
			int jj = j & 255;

			// Calculate the contribution from the three corners
			double t0 = 0.5 - x0 * x0 - y0 * y0;
			double n0 = 0.0;
			if (t0 > 0)
			{
				t0 *= t0;
				n0 = t0 * t0 * Grad(_p[ii + _p[jj]], x0, y0);
			}

			double t1 = 0.5 - x1 * x1 - y1 * y1;
			double n1 = 0.0;
			if (t1 > 0)
			{
				t1 *= t1;
				n1 = t1 * t1 * Grad(_p[ii + i1 + _p[jj + j1]], x1, y1);
			}

			double t2 = 0.5 - x2 * x2 - y2 * y2;
			double n2 = 0.0;
			if (t2 > 0)
			{
				t2 *= t2;
				n2 = t2 * t2 * Grad(_p[ii + 1 + _p[jj + 1]], x2, y2);
			}

			return 70.0 * (n0 + n1 + n2);
		}

		protected override double GenerateCore(double x, double y, double z)
		{
			// Skew the input space to determine which simplex cell we're in
			double s = (x + y + z) * _f3;
			int i = FastFloor(x + s);
			int j = FastFloor(y + s);
			int k = FastFloor(z + s);

			double t = (i + j + k) * _g3;
			double x0 = x - (i - t);
			double y0 = y - (j - t);
			double z0 = z - (k - t);

			// Determine which simplex we are in
			int i1, j1, k1;
			int i2, j2, k2;

			if (x0 >= y0)
			{
				if (y0 >= z0)
				{
					i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 1; k2 = 0;
				}
				else if (x0 >= z0)
				{
					i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 0; k2 = 1;
				}
				else
				{
					i1 = 0; j1 = 0; k1 = 1; i2 = 1; j2 = 0; k2 = 1;
				}
			}
			else
			{
				if (y0 < z0)
				{
					i1 = 0; j1 = 0; k1 = 1; i2 = 0; j2 = 1; k2 = 1;
				}
				else if (x0 < z0)
				{
					i1 = 0; j1 = 1; k1 = 0; i2 = 0; j2 = 1; k2 = 1;
				}
				else
				{
					i1 = 0; j1 = 1; k1 = 0; i2 = 1; j2 = 1; k2 = 0;
				}
			}

			double x1 = x0 - i1 + _g3;
			double y1 = y0 - j1 + _g3;
			double z1 = z0 - k1 + _g3;
			double x2 = x0 - i2 + 2.0 * _g3;
			double y2 = y0 - j2 + 2.0 * _g3;
			double z2 = z0 - k2 + 2.0 * _g3;
			double x3 = x0 - 1.0 + 3.0 * _g3;
			double y3 = y0 - 1.0 + 3.0 * _g3;
			double z3 = z0 - 1.0 + 3.0 * _g3;

			int ii = i & 255;
			int jj = j & 255;
			int kk = k & 255;

			double t0 = 0.6 - x0 * x0 - y0 * y0 - z0 * z0;
			double n0 = 0.0;
			if (t0 > 0)
			{
				t0 *= t0;
				n0 = t0 * t0 * Grad(_p[ii + _p[jj + _p[kk]]], x0, y0, z0);
			}

			double t1 = 0.6 - x1 * x1 - y1 * y1 - z1 * z1;
			double n1 = 0.0;
			if (t1 > 0)
			{
				t1 *= t1;
				n1 = t1 * t1 * Grad(_p[ii + i1 + _p[jj + j1 + _p[kk + k1]]], x1, y1, z1);
			}

			double t2 = 0.6 - x2 * x2 - y2 * y2 - z2 * z2;
			double n2 = 0.0;
			if (t2 > 0)
			{
				t2 *= t2;
				n2 = t2 * t2 * Grad(_p[ii + i2 + _p[jj + j2 + _p[kk + k2]]], x2, y2, z2);
			}

			double t3 = 0.6 - x3 * x3 - y3 * y3 - z3 * z3;
			double n3 = 0.0;
			if (t3 > 0)
			{
				t3 *= t3;
				n3 = t3 * t3 * Grad(_p[ii + 1 + _p[jj + 1 + _p[kk + 1]]], x3, y3, z3);
			}

			return 32.0 * (n0 + n1 + n2 + n3);
		}

		protected override double GenerateCore(double x, double y, double z, double w)
		{
			// Skew the input space to determine which simplex cell we're in
			double s = (x + y + z + w) * _f4;
			int i = FastFloor(x + s);
			int j = FastFloor(y + s);
			int k = FastFloor(z + s);
			int l = FastFloor(w + s);

			double t = (i + j + k + l) * _g4;
			double x0 = x - (i - t);
			double y0 = y - (j - t);
			double z0 = z - (k - t);
			double w0 = w - (l - t);

			// Determine simplex ordering
			int rankx = 0;
			int ranky = 0;
			int rankz = 0;
			int rankw = 0;

			if (x0 > y0) rankx++; else ranky++;
			if (x0 > z0) rankx++; else rankz++;
			if (x0 > w0) rankx++; else rankw++;
			if (y0 > z0) ranky++; else rankz++;
			if (y0 > w0) ranky++; else rankw++;
			if (z0 > w0) rankz++; else rankw++;

			// Calculate simplex vertices
			int i1 = rankx >= 3 ? 1 : 0;
			int j1 = ranky >= 3 ? 1 : 0;
			int k1 = rankz >= 3 ? 1 : 0;
			int l1 = rankw >= 3 ? 1 : 0;

			int i2 = rankx >= 2 ? 1 : 0;
			int j2 = ranky >= 2 ? 1 : 0;
			int k2 = rankz >= 2 ? 1 : 0;
			int l2 = rankw >= 2 ? 1 : 0;

			int i3 = rankx >= 1 ? 1 : 0;
			int j3 = ranky >= 1 ? 1 : 0;
			int k3 = rankz >= 1 ? 1 : 0;
			int l3 = rankw >= 1 ? 1 : 0;

			int i4 = 1;
			int j4 = 1;
			int k4 = 1;
			int l4 = 1;

			// Calculate the five simplex corners
			double x1 = x0 - i1 + _g4;
			double y1 = y0 - j1 + _g4;
			double z1 = z0 - k1 + _g4;
			double w1 = w0 - l1 + _g4;

			double x2 = x0 - i2 + 2.0 * _g4;
			double y2 = y0 - j2 + 2.0 * _g4;
			double z2 = z0 - k2 + 2.0 * _g4;
			double w2 = w0 - l2 + 2.0 * _g4;

			double x3 = x0 - i3 + 3.0 * _g4;
			double y3 = y0 - j3 + 3.0 * _g4;
			double z3 = z0 - k3 + 3.0 * _g4;
			double w3 = w0 - l3 + 3.0 * _g4;

			double x4 = x0 - i4 + 4.0 * _g4;
			double y4 = y0 - j4 + 4.0 * _g4;
			double z4 = z0 - k4 + 4.0 * _g4;
			double w4 = w0 - l4 + 4.0 * _g4;

			double x5 = x0 - 1.0 + 5.0 * _g4;
			double y5 = y0 - 1.0 + 5.0 * _g4;
			double z5 = z0 - 1.0 + 5.0 * _g4;
			double w5 = w0 - 1.0 + 5.0 * _g4;

			int ii = i & 255;
			int jj = j & 255;
			int kk = k & 255;
			int ll = l & 255;

			// Calculate contribution from the five corners
			double t0 = 0.6 - x0 * x0 - y0 * y0 - z0 * z0 - w0 * w0;
			double n0 = 0.0;
			if (t0 > 0)
			{
				t0 *= t0;
				n0 = t0 * t0 * Grad(_p[ii + _p[jj + _p[kk + _p[ll]]]], x0, y0, z0, w0);
			}

			double t1 = 0.6 - x1 * x1 - y1 * y1 - z1 * z1 - w1 * w1;
			double n1 = 0.0;
			if (t1 > 0)
			{
				t1 *= t1;
				n1 = t1 * t1 * Grad(_p[ii + i1 + _p[jj + j1 + _p[kk + k1 + _p[ll + l1]]]], x1, y1, z1, w1);
			}

			double t2 = 0.6 - x2 * x2 - y2 * y2 - z2 * z2 - w2 * w2;
			double n2 = 0.0;
			if (t2 > 0)
			{
				t2 *= t2;
				n2 = t2 * t2 * Grad(_p[ii + i2 + _p[jj + j2 + _p[kk + k2 + _p[ll + l2]]]], x2, y2, z2, w2);
			}

			double t3 = 0.6 - x3 * x3 - y3 * y3 - z3 * z3 - w3 * w3;
			double n3 = 0.0;
			if (t3 > 0)
			{
				t3 *= t3;
				n3 = t3 * t3 * Grad(_p[ii + i3 + _p[jj + j3 + _p[kk + k3 + _p[ll + l3]]]], x3, y3, z3, w3);
			}

			double t4 = 0.6 - x4 * x4 - y4 * y4 - z4 * z4 - w4 * w4;
			double n4 = 0.0;
			if (t4 > 0)
			{
				t4 *= t4;
				n4 = t4 * t4 * Grad(_p[ii + i4 + _p[jj + j4 + _p[kk + k4 + _p[ll + l4]]]], x4, y4, z4, w4);
			}

			double t5 = 0.6 - x5 * x5 - y5 * y5 - z5 * z5 - w5 * w5;
			double n5 = 0.0;
			if (t5 > 0)
			{
				t5 *= t5;
				n5 = t5 * t5 * Grad(_p[ii + 1 + _p[jj + 1 + _p[kk + 1 + _p[ll + 1]]]], x5, y5, z5, w5);
			}

			return 27.0 * (n0 + n1 + n2 + n3 + n4 + n5);
		}
	}
}