namespace LLMDesktopAssistant.Utils.Noise
{
	/// <summary>
	/// Generates Value Noise. 
	/// Unlike Perlin/Simplex which interpolate gradients (vectors), Value Noise interpolates simple random values defined at grid points.
	/// Result looks "blockier" than Perlin but is computationally cheaper and useful for specific textures (wood, marble, stylized terrain).
	/// </summary>
	public class ValueNoise : NoiseBase
	{
		private readonly int[] _perm = new int[512];

		public ValueNoise() : base()
		{
		}

		public ValueNoise(int seed) : base(seed)
		{
		}

		protected override void OnSeedSet(int seed)
		{
			var rnd = new Random(seed);

			for (int i = 0; i < 256; i++)
			{
				_perm[i] = i;
			}

			for (int i = 0; i < 256; i++)
			{
				int j = rnd.Next(256);
				(_perm[i], _perm[j]) = (_perm[j], _perm[i]);
			}

			for (int i = 0; i < 256; i++)
			{
				_perm[i + 256] = _perm[i];
			}
		}

		// Quintic curve: 6t^5 - 15t^4 + 10t^3
		private static double Fade(double t)
		{
			return t * t * t * (t * (t * 6 - 15) + 10);
		}

		private static double Lerp(double a, double b, double t)
		{
			return a + t * (b - a);
		}

		private double GetVal(int hash)
		{
			return (_perm[hash] / 127.5) - 1.0;
		}

		protected override double GenerateCore(double x)
		{
			int xi = (int)Math.Floor(x);
			double xf = x - xi;

			int X = xi & 255;

			int h0 = _perm[X];
			int h1 = _perm[X + 1];

			double u = Fade(xf);

			return Lerp(GetVal(h0), GetVal(h1), u);
		}

		protected override double GenerateCore(double x, double y)
		{
			int xi = (int)Math.Floor(x);
			int yi = (int)Math.Floor(y);

			double xf = x - xi;
			double yf = y - yi;

			int X = xi & 255;
			int Y = yi & 255;

			int a = _perm[X] + Y;
			int b = _perm[X + 1] + Y;

			int aa = _perm[a];
			int ab = _perm[a + 1];
			int ba = _perm[b];
			int bb = _perm[b + 1];

			double u = Fade(xf);
			double v = Fade(yf);

			double x1 = Lerp(GetVal(aa), GetVal(ba), u);
			double x2 = Lerp(GetVal(ab), GetVal(bb), u);

			return Lerp(x1, x2, v);
		}

		protected override double GenerateCore(double x, double y, double z)
		{
			int xi = (int)Math.Floor(x);
			int yi = (int)Math.Floor(y);
			int zi = (int)Math.Floor(z);

			double xf = x - xi;
			double yf = y - yi;
			double zf = z - zi;

			int X = xi & 255;
			int Y = yi & 255;
			int Z = zi & 255;

			int a = _perm[X] + Y;
			int aa = _perm[a] + Z;
			int ab = _perm[a + 1] + Z;
			int b = _perm[X + 1] + Y;
			int ba = _perm[b] + Z;
			int bb = _perm[b + 1] + Z;

			double v000 = GetVal(_perm[aa]);
			double v001 = GetVal(_perm[aa + 1]);
			double v010 = GetVal(_perm[ab]);
			double v011 = GetVal(_perm[ab + 1]);
			double v100 = GetVal(_perm[ba]);
			double v101 = GetVal(_perm[ba + 1]);
			double v110 = GetVal(_perm[bb]);
			double v111 = GetVal(_perm[bb + 1]);

			double u = Fade(xf);
			double v = Fade(yf);
			double w = Fade(zf);

			double x1 = Lerp(v000, v100, u);
			double x2 = Lerp(v010, v110, u);
			double x3 = Lerp(v001, v101, u);
			double x4 = Lerp(v011, v111, u);

			double y1 = Lerp(x1, x2, v);
			double y2 = Lerp(x3, x4, v);

			return Lerp(y1, y2, w);
		}

		protected override double GenerateCore(double x, double y, double z, double w)
		{
			int xi = (int)Math.Floor(x);
			int yi = (int)Math.Floor(y);
			int zi = (int)Math.Floor(z);
			int wi = (int)Math.Floor(w);

			double xf = x - xi;
			double yf = y - yi;
			double zf = z - zi;
			double wf = w - wi;

			int X = xi & 255;
			int Y = yi & 255;
			int Z = zi & 255;
			int W = wi & 255;

			double k = Fade(xf);
			double l = Fade(yf);
			double m = Fade(zf);
			double n = Fade(wf);

			double Val(int ix, int iy, int iz, int iw)
			{
				int idx = _perm[_perm[_perm[_perm[ix] + iy] + iz] + iw];
				return (idx / 127.5) - 1.0;
			}

			double i1 = Lerp(Val(X, Y, Z, W), Val(X + 1, Y, Z, W), k);
			double i2 = Lerp(Val(X, Y + 1, Z, W), Val(X + 1, Y + 1, Z, W), k);
			double i3 = Lerp(Val(X, Y, Z + 1, W), Val(X + 1, Y, Z + 1, W), k);
			double i4 = Lerp(Val(X, Y + 1, Z + 1, W), Val(X + 1, Y + 1, Z + 1, W), k);

			double i5 = Lerp(Val(X, Y, Z, W + 1), Val(X + 1, Y, Z, W + 1), k);
			double i6 = Lerp(Val(X, Y + 1, Z, W + 1), Val(X + 1, Y + 1, Z, W + 1), k);
			double i7 = Lerp(Val(X, Y, Z + 1, W + 1), Val(X + 1, Y, Z + 1, W + 1), k);
			double i8 = Lerp(Val(X, Y + 1, Z + 1, W + 1), Val(X + 1, Y + 1, Z + 1, W + 1), k);

			double j1 = Lerp(i1, i2, l);
			double j2 = Lerp(i3, i4, l);
			double j3 = Lerp(i5, i6, l);
			double j4 = Lerp(i7, i8, l);

			double m1 = Lerp(j1, j2, m);
			double m2 = Lerp(j3, j4, m);

			return Lerp(m1, m2, n);
		}
	}
}