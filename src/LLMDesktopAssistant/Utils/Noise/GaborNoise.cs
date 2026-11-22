namespace LLMDesktopAssistant.Utils.Noise
{
	/// <summary>
	/// Gabor Noise implementation.
	/// Sparse convolution noise consisting of Gaussian-windowed sinusoidal impulses.
	/// Great for creating textures like fabric, brush strokes, or marble.
	/// </summary>
	public class GaborNoise : NoiseBase
	{
		/// <summary>
		/// Controls the width of the Gaussian envelope. 
		/// Higher values make the kernels narrower (spikier). 
		/// Typical range: 0.5 to 2.0.
		/// </summary>
		public double K { get; set; } = 1.0;

		/// <summary>
		/// Frequency of the cosine wave inside the kernel.
		/// Controls the density of stripes within each blob.
		/// </summary>
		public double Frequency { get; set; } = 2.0;

		/// <summary>
		/// Preferred orientation of the noise in radians.
		/// Used only if RandomizeRotation is false.
		/// </summary>
		public double Rotation { get; set; } = 0.0;

		/// <summary>
		/// If true, each kernel will have a random orientation.
		/// Result looks like "worms" or isotropic texture.
		/// If false, noise will be directional (anisotropic).
		/// </summary>
		public bool RandomizeRotation { get; set; } = true;

		/// <summary>
		/// Density of impulses per unit cell. 
		/// Currently simplified to implicitly 1 impulse per grid cell with jitter.
		/// </summary>
		private const double KernelRadius = 1.5; // Radius to check neighbor cells

		public GaborNoise() : base()
		{
		}

		public GaborNoise(int seed) : base(seed)
		{
		}

		// Helper for hashing coordinates into a pseudo-random value
		private int Hash(int x)
		{
			int h = Seed ^ x * 374761393;
			h = (h ^ (h >> 13)) * 1274126177;
			return h ^ (h >> 16);
		}

		private int Hash(int x, int y)
		{
			int h = Seed ^ x * 374761393 ^ y * 668265263;
			h = (h ^ (h >> 13)) * 1274126177;
			return h ^ (h >> 16);
		}

		private int Hash(int x, int y, int z)
		{
			int h = Seed ^ x * 374761393 ^ y * 668265263 ^ z * 437985613;
			h = (h ^ (h >> 13)) * 1274126177;
			return h ^ (h >> 16);
		}

		private int Hash(int x, int y, int z, int w)
		{
			int h = Seed ^ x * 374761393 ^ y * 668265263 ^ z * 437985613 ^ w * 532845617;
			h = (h ^ (h >> 13)) * 1274126177;
			return h ^ (h >> 16);
		}

		private double Val(int hash)
		{
			return (hash & 0x7FFFFFFF) / (double)int.MaxValue;
		}

		// Gabor kernel function
		// d: distance vector magnitude squared
		// p: projection of distance vector onto direction vector
		private double EvaluateKernel(double dx, double dy, double dz, double dw, double directionX, double directionY, double directionZ, double directionW)
		{
			// Squared magnitude
			double r2 = dx * dx + dy * dy + dz * dz + dw * dw;

			// Gaussian envelope: e^(-pi * K^2 * r^2)
			double gaussian = Math.Exp(-Math.PI * K * K * r2);

			// Harmonic: cos(2 * pi * F * (dir . dist))
			// Dot product representing projection along the wave direction
			double projection = dx * directionX + dy * directionY + dz * directionZ + dw * directionW;
			double harmonic = Math.Cos(2.0 * Math.PI * Frequency * projection);

			return gaussian * harmonic;
		}

		protected override double GenerateCore(double x)
		{
			int ix = (int)Math.Floor(x);
			double sum = 0.0;

			// Check neighbor cells (1D)
			for (int i = -1; i <= 1; i++)
			{
				int cx = ix + i;
				int h = Hash(cx);

				// Jitter position inside the cell
				double px = cx + Val(h);

				double dx = x - px;

				// Optimization: skip if far
				if (Math.Abs(dx) > KernelRadius) continue;

				// 1D "Direction" is just 1.0 (or -1.0, but symmetry makes it equal)
				// RandomizeRotation in 1D could mean random phase, implemented here as sign flip
				double dir = 1.0;
				if (RandomizeRotation && (h & 1) == 0) dir = -1.0;

				sum += EvaluateKernel(dx, 0, 0, 0, dir, 0, 0, 0);
			}
			return Math.Max(-1, Math.Min(1, sum));
		}

		protected override double GenerateCore(double x, double y)
		{
			int ix = (int)Math.Floor(x);
			int iy = (int)Math.Floor(y);

			double sum = 0.0;

			// 3x3 search window
			for (int i = -1; i <= 1; i++)
			{
				for (int j = -1; j <= 1; j++)
				{
					int cx = ix + i;
					int cy = iy + j;
					int h = Hash(cx, cy);

					// Jitter
					double px = cx + Val(h);
					double py = cy + Val(h ^ 0x12345678); // De-correlate Y jitter

					double dx = x - px;
					double dy = y - py;

					// Distance check
					if (dx * dx + dy * dy > KernelRadius * KernelRadius) continue;

					// Determine orientation
					double angle = Rotation;
					if (RandomizeRotation)
					{
						// Random angle 0..2PI
						angle = Val(h ^ 0x55555555) * Math.PI * 2.0;
					}

					double dirX = Math.Cos(angle);
					double dirY = Math.Sin(angle);

					sum += EvaluateKernel(dx, dy, 0, 0, dirX, dirY, 0, 0);
				}
			}

			// Normalization is tricky for Gabor, clamping is safe
			return Math.Max(-1, Math.Min(1, sum * 0.7)); // 0.7 empirical scaling
		}

		protected override double GenerateCore(double x, double y, double z)
		{
			int ix = (int)Math.Floor(x);
			int iy = (int)Math.Floor(y);
			int iz = (int)Math.Floor(z);

			double sum = 0.0;

			for (int i = -1; i <= 1; i++)
				for (int j = -1; j <= 1; j++)
					for (int k = -1; k <= 1; k++)
					{
						int cx = ix + i;
						int cy = iy + j;
						int cz = iz + k;
						int h = Hash(cx, cy, cz);

						double px = cx + Val(h);
						double py = cy + Val(h ^ 0x11111111);
						double pz = cz + Val(h ^ 0x22222222);

						double dx = x - px;
						double dy = y - py;
						double dz = z - pz;

						if (dx * dx + dy * dy + dz * dz > KernelRadius * KernelRadius) continue;

						double dirX, dirY, dirZ;

						if (RandomizeRotation)
						{
							// Simple random unit vector on sphere approximation
							double theta = Val(h ^ 0x33333333) * Math.PI * 2.0;
							double phi = (Val(h ^ 0x44444444) - 0.5) * Math.PI; // -pi/2 to pi/2
							double cosPhi = Math.Cos(phi);

							dirX = Math.Cos(theta) * cosPhi;
							dirY = Math.Sin(theta) * cosPhi;
							dirZ = Math.Sin(phi);
						}
						else
						{
							// Fixed orientation logic for 3D is complex (Euler angles).
							// Here we map Rotation to rotation around Y axis for simplicity
							// or just use fixed vector (1,0,0) rotated.
							// For full control, class would need Vector3 Orientation property.
							// Using simple 2D-ish rotation around Z axis:
							dirX = Math.Cos(Rotation);
							dirY = Math.Sin(Rotation);
							dirZ = 0;
						}

						sum += EvaluateKernel(dx, dy, dz, 0, dirX, dirY, dirZ, 0);
					}

			return Math.Max(-1, Math.Min(1, sum * 0.5));
		}

		protected override double GenerateCore(double x, double y, double z, double w)
		{
			// 4D implementation: simplified loop, similar logic
			int ix = (int)Math.Floor(x);
			int iy = (int)Math.Floor(y);
			int iz = (int)Math.Floor(z);
			int iw = (int)Math.Floor(w);

			double sum = 0.0;

			// Checking 3^4 = 81 cells is heavy. 
			// Often 4D noise is optimized by not checking all corners or using Simplex grid.
			// Keeping it simple rectangular for consistency.
			for (int i = -1; i <= 1; i++)
				for (int j = -1; j <= 1; j++)
					for (int k = -1; k <= 1; k++)
						for (int l = -1; l <= 1; l++)
						{
							int cx = ix + i;
							int cy = iy + j;
							int cz = iz + k;
							int cw = iw + l;
							int h = Hash(cx, cy, cz, cw);

							double px = cx + Val(h);
							double py = cy + Val(h ^ 0x999);
							double pz = cz + Val(h ^ 0x888);
							double pw = cw + Val(h ^ 0x777);

							double dx = x - px;
							double dy = y - py;
							double dz = z - pz;
							double dw_val = w - pw; // rename to avoid local var conflict

							if (dx * dx + dy * dy + dz * dz + dw_val * dw_val > KernelRadius * KernelRadius) continue;

							// 4D orientation random
							// Simplified: random projection
							double dirX = Val(h ^ 0xA) - 0.5;
							double dirY = Val(h ^ 0xB) - 0.5;
							double dirZ = Val(h ^ 0xC) - 0.5;
							double dirW = Val(h ^ 0xD) - 0.5;

							// Normalize roughly
							double len = Math.Sqrt(dirX * dirX + dirY * dirY + dirZ * dirZ + dirW * dirW) + 1e-6;
							dirX /= len; dirY /= len; dirZ /= len; dirW /= len;

							sum += EvaluateKernel(dx, dy, dz, dw_val, dirX, dirY, dirZ, dirW);
						}

			return Math.Max(-1, Math.Min(1, sum * 0.4));
		}
	}
}