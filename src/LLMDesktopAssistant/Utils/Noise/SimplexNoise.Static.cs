namespace LLMDesktopAssistant.Utils.Noise
{
	public partial class SimplexNoise
	{
		/// <summary>
		/// 32-bit mix (Murmur/xxhash-like style). Fast and decent avalanche for grid hashing.
		/// </summary>
		private static int Mix32(int x)
		{
			unchecked
			{
				uint v = (uint)x;
				v ^= v >> 16;
				v *= 0x7FEB352Du;
				v ^= v >> 15;
				v *= 0x846CA68Bu;
				v ^= v >> 16;
				return (int)v;
			}
		}

		private static int Hash1(int i, int seed)
		{
			unchecked
			{
				int h = seed;
				h ^= i * 0x27D4EB2D;
				return Mix32(h);
			}
		}

		private static int Hash2(int i, int j, int seed)
		{
			unchecked
			{
				int h = seed;
				h ^= i * 0x27D4EB2D;
				h ^= j * 0x165667B1;
				return Mix32(h);
			}
		}

		private static int Hash3(int i, int j, int k, int seed)
		{
			unchecked
			{
				int h = seed;
				h ^= i * 0x27D4EB2D;
				h ^= j * 0x165667B1;
				h ^= k * 0x1B873593;
				return Mix32(h);
			}
		}

		private static int Hash4(int i, int j, int k, int l, int seed)
		{
			unchecked
			{
				int h = seed;
				h ^= i * 0x27D4EB2D;
				h ^= j * 0x165667B1;
				h ^= k * 0x1B873593;
				h ^= l * 0x75EBCA6B;
				return Mix32(h);
			}
		}

		/// <summary>
		/// 1D simplex-like gradient noise without permutation table. Uses (x, seed) hashing.
		/// </summary>
		public static double GenerateHashed(double x, int seed)
		{
			int i0 = FastFloor(x);
			int i1 = i0 + 1;

			double x0 = x - i0;
			double x1 = x0 - 1.0;

			double t0 = 1.0 - x0 * x0;
			t0 *= t0;
			double n0 = t0 * t0 * Grad(Hash1(i0, seed), x0);

			double t1 = 1.0 - x1 * x1;
			t1 *= t1;
			double n1 = t1 * t1 * Grad(Hash1(i1, seed), x1);

			return 0.395 * (n0 + n1);
		}

		/// <summary>
		/// 2D simplex noise without permutation table. Uses (i, j, seed) hashing per corner.
		/// </summary>
		public static double GenerateHashed(double x, double y, int seed)
		{
			double s = (x + y) * _f2;
			int i = FastFloor(x + s);
			int j = FastFloor(y + s);

			double t = (i + j) * _g2;
			double x0 = x - (i - t);
			double y0 = y - (j - t);

			int i1, j1;
			if (x0 > y0) { i1 = 1; j1 = 0; }
			else { i1 = 0; j1 = 1; }

			double x1 = x0 - i1 + _g2;
			double y1 = y0 - j1 + _g2;
			double x2 = x0 - 1.0 + 2.0 * _g2;
			double y2 = y0 - 1.0 + 2.0 * _g2;

			double n0 = 0.0;
			double t0 = 0.5 - x0 * x0 - y0 * y0;
			if (t0 > 0.0)
			{
				t0 *= t0;
				n0 = t0 * t0 * Grad(Hash2(i, j, seed), x0, y0);
			}

			double n1 = 0.0;
			double t1 = 0.5 - x1 * x1 - y1 * y1;
			if (t1 > 0.0)
			{
				t1 *= t1;
				n1 = t1 * t1 * Grad(Hash2(i + i1, j + j1, seed), x1, y1);
			}

			double n2 = 0.0;
			double t2 = 0.5 - x2 * x2 - y2 * y2;
			if (t2 > 0.0)
			{
				t2 *= t2;
				n2 = t2 * t2 * Grad(Hash2(i + 1, j + 1, seed), x2, y2);
			}

			return 70.0 * (n0 + n1 + n2);
		}

		/// <summary>
		/// 3D simplex noise without permutation table. Uses (i, j, k, seed) hashing per corner.
		/// </summary>
		public static double GenerateHashed(double x, double y, double z, int seed)
		{
			double s = (x + y + z) * _f3;
			int i = FastFloor(x + s);
			int j = FastFloor(y + s);
			int k = FastFloor(z + s);

			double t = (i + j + k) * _g3;
			double x0 = x - (i - t);
			double y0 = y - (j - t);
			double z0 = z - (k - t);

			int i1, j1, k1;
			int i2, j2, k2;

			if (x0 >= y0)
			{
				if (y0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 1; k2 = 0; }
				else if (x0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 0; k2 = 1; }
				else { i1 = 0; j1 = 0; k1 = 1; i2 = 1; j2 = 0; k2 = 1; }
			}
			else
			{
				if (y0 < z0) { i1 = 0; j1 = 0; k1 = 1; i2 = 0; j2 = 1; k2 = 1; }
				else if (x0 < z0) { i1 = 0; j1 = 1; k1 = 0; i2 = 0; j2 = 1; k2 = 1; }
				else { i1 = 0; j1 = 1; k1 = 0; i2 = 1; j2 = 1; k2 = 0; }
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

			double n0 = 0.0;
			double t0 = 0.6 - x0 * x0 - y0 * y0 - z0 * z0;
			if (t0 > 0.0)
			{
				t0 *= t0;
				n0 = t0 * t0 * Grad(Hash3(i, j, k, seed), x0, y0, z0);
			}

			double n1 = 0.0;
			double t1 = 0.6 - x1 * x1 - y1 * y1 - z1 * z1;
			if (t1 > 0.0)
			{
				t1 *= t1;
				n1 = t1 * t1 * Grad(Hash3(i + i1, j + j1, k + k1, seed), x1, y1, z1);
			}

			double n2 = 0.0;
			double t2 = 0.6 - x2 * x2 - y2 * y2 - z2 * z2;
			if (t2 > 0.0)
			{
				t2 *= t2;
				n2 = t2 * t2 * Grad(Hash3(i + i2, j + j2, k + k2, seed), x2, y2, z2);
			}

			double n3 = 0.0;
			double t3 = 0.6 - x3 * x3 - y3 * y3 - z3 * z3;
			if (t3 > 0.0)
			{
				t3 *= t3;
				n3 = t3 * t3 * Grad(Hash3(i + 1, j + 1, k + 1, seed), x3, y3, z3);
			}

			return 32.0 * (n0 + n1 + n2 + n3);
		}

		/// <summary>
		/// 4D simplex noise without permutation table. Uses (i, j, k, l, seed) hashing per corner.
		/// </summary>
		public static double GenerateHashed(double x, double y, double z, double w, int seed)
		{
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

			int rankx = 0, ranky = 0, rankz = 0, rankw = 0;
			if (x0 > y0) rankx++; else ranky++;
			if (x0 > z0) rankx++; else rankz++;
			if (x0 > w0) rankx++; else rankw++;
			if (y0 > z0) ranky++; else rankz++;
			if (y0 > w0) ranky++; else rankw++;
			if (z0 > w0) rankz++; else rankw++;

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

			// Corner offsets
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

			double x4 = x0 - 1.0 + 4.0 * _g4;
			double y4 = y0 - 1.0 + 4.0 * _g4;
			double z4 = z0 - 1.0 + 4.0 * _g4;
			double w4 = w0 - 1.0 + 4.0 * _g4;

			double n0 = 0.0;
			double t0 = 0.6 - x0 * x0 - y0 * y0 - z0 * z0 - w0 * w0;
			if (t0 > 0.0)
			{
				t0 *= t0;
				n0 = t0 * t0 * Grad(Hash4(i, j, k, l, seed), x0, y0, z0, w0);
			}

			double n1 = 0.0;
			double t1 = 0.6 - x1 * x1 - y1 * y1 - z1 * z1 - w1 * w1;
			if (t1 > 0.0)
			{
				t1 *= t1;
				n1 = t1 * t1 * Grad(Hash4(i + i1, j + j1, k + k1, l + l1, seed), x1, y1, z1, w1);
			}

			double n2 = 0.0;
			double t2 = 0.6 - x2 * x2 - y2 * y2 - z2 * z2 - w2 * w2;
			if (t2 > 0.0)
			{
				t2 *= t2;
				n2 = t2 * t2 * Grad(Hash4(i + i2, j + j2, k + k2, l + l2, seed), x2, y2, z2, w2);
			}

			double n3 = 0.0;
			double t3 = 0.6 - x3 * x3 - y3 * y3 - z3 * z3 - w3 * w3;
			if (t3 > 0.0)
			{
				t3 *= t3;
				n3 = t3 * t3 * Grad(Hash4(i + i3, j + j3, k + k3, l + l3, seed), x3, y3, z3, w3);
			}

			double n4 = 0.0;
			double t4 = 0.6 - x4 * x4 - y4 * y4 - z4 * z4 - w4 * w4;
			if (t4 > 0.0)
			{
				t4 *= t4;
				n4 = t4 * t4 * Grad(Hash4(i + 1, j + 1, k + 1, l + 1, seed), x4, y4, z4, w4);
			}

			return 27.0 * (n0 + n1 + n2 + n3 + n4);
		}
	}
}