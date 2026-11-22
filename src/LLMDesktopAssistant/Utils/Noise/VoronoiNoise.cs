namespace LLMDesktopAssistant.Utils.Noise
{
	/// <summary>
	/// The method used to calculate distance between points in Voronoi noise.
	/// </summary>
	public enum VoronoiDistanceMetric
	{
		/// <summary>
		/// Euclidean distance: sqrt((x2-x1)² + (y2-y1)²)
		/// </summary>
		Euclidean,

		/// <summary>
		/// Manhattan distance: |x2-x1| + |y2-y1|
		/// </summary>
		Manhattan,

		/// <summary>
		/// Chebyshev distance: max(|x2-x1|, |y2-y1|)
		/// </summary>
		Chebyshev,

		/// <summary>
		/// Quadratic distance: (x2-x1)² + (y2-y1)² (faster, no square root)
		/// </summary>
		Quadratic
	}

	/// <summary>
	/// The feature to return from the Voronoi calculation.
	/// </summary>
	public enum VoronoiFeature
	{
		/// <summary>
		/// Distance to the closest feature point (F1).
		/// </summary>
		F1,

		/// <summary>
		/// Distance to the second closest feature point (F2).
		/// </summary>
		F2,

		/// <summary>
		/// Difference between F2 and F1 (F2 - F1).
		/// </summary>
		F2MinusF1,

		/// <summary>
		/// Average of F1 and F2.
		/// </summary>
		Average,

		/// <summary>
		/// F1 multiplied by F2.
		/// </summary>
		Product,

		/// <summary>
		/// Returns a pseudo-random value based on the closest feature point.
		/// </summary>
		RandomValue
	}

	/// <summary>
	/// Generates Voronoi noise, also known as Worley noise or cellular noise.
	/// Creates a pattern of cells with distance-based features.
	/// </summary>
	public class VoronoiNoise : NoiseBase
	{
		/// <summary>
		/// Gets the shared instance of the <see cref="VoronoiNoise"/> class.
		/// </summary>
		public static VoronoiNoise Shared { get; }

		static VoronoiNoise()
		{
			Shared = new VoronoiNoise();
		}

		/// <summary>
		/// Gets or sets the number of feature points per cell.
		/// </summary>
		public int PointsPerCell { get; set; }

		/// <summary>
		/// Gets or sets the distance metric used for calculations.
		/// </summary>
		public VoronoiDistanceMetric Metric { get; set; }

		/// <summary>
		/// Gets or sets the feature to return from Voronoi calculations.
		/// </summary>
		public VoronoiFeature Feature { get; set; }

		/// <summary>
		/// Initializes a new instance of <see cref="VoronoiNoise"/> class with default parameters.
		/// </summary>
		public VoronoiNoise() : this(Random.Shared.Next(), 1, VoronoiDistanceMetric.Euclidean, VoronoiFeature.F1)
		{
		}

		/// <summary>
		/// Initializes a new instance of <see cref="VoronoiNoise"/> class with specified seed.
		/// </summary>
		/// <param name="seed">The seed value to use for the random number generator.</param>
		public VoronoiNoise(int seed) : this(seed, 1, VoronoiDistanceMetric.Euclidean, VoronoiFeature.F1)
		{
		}

		/// <summary>
		/// Initializes a new instance of <see cref="VoronoiNoise"/> class with specified parameters.
		/// </summary>
		/// <param name="seed">The seed value to use for the random number generator.</param>
		/// <param name="pointsPerCell">Number of feature points per cell.</param>
		/// <param name="metric">The distance metric to use.</param>
		/// <param name="feature">The Voronoi feature to return.</param>
		/// <param name="returnSquaredDistances">Whether to return squared distances.</param>
		public VoronoiNoise(int seed, int pointsPerCell = 1,
			VoronoiDistanceMetric metric = VoronoiDistanceMetric.Euclidean,
			VoronoiFeature feature = VoronoiFeature.F1) : base(seed)
		{
			PointsPerCell = pointsPerCell;
			Metric = metric;
			Feature = feature;
		}

		protected override double GenerateCore(double x)
		{
			int cellX = (int)Math.Floor(x);

			double minDist1 = double.MaxValue;
			double minDist2 = double.MaxValue;

			int nPointX = 0, nPointI = 0;

			// Check the cell and its neighbors
			for (int nx = -1; nx <= 1; nx++)
			{
				int currentCellX = cellX + nx;

				// Generate feature points for this cell
				for (int i = 0; i < PointsPerCell; i++)
				{
					// Get deterministic random offsets for this cell and point index
					double offsetX = GetRandomOffset(currentCellX, i);

					double pointX = currentCellX + offsetX;

					// Calculate distance
					double dx = pointX - x;
					double dist = CalculateDistance(dx, 0, 0, 0, 1);

					// Update closest distances
					if (dist < minDist1)
					{
						nPointX = currentCellX;
						nPointI = i;

						minDist2 = minDist1;
						minDist1 = dist;
					}
					else if (dist < minDist2)
					{
						minDist2 = dist;
					}
				}
			}

			int pointHash = 0;
			pointHash ^= nPointX * 0x7123a8c1;
			pointHash ^= nPointI * 0x5123a8c1;
			double featureValue = GetFeatureValue(minDist1, minDist2, pointHash);
			return featureValue * 2.0 - 1.0;
		}

		protected override double GenerateCore(double x, double y)
		{
			int cellX = (int)Math.Floor(x);
			int cellY = (int)Math.Floor(y);

			double minDist1 = double.MaxValue;
			double minDist2 = double.MaxValue;

			int nPointX = 0, nPointY = 0, nPointI = 0;

			// Check the cell and its neighbors
			for (int nx = -1; nx <= 1; nx++)
			{
				for (int ny = -1; ny <= 1; ny++)
				{
					int currentCellX = cellX + nx;
					int currentCellY = cellY + ny;

					// Generate feature points for this cell
					for (int i = 0; i < PointsPerCell; i++)
					{
						// Get deterministic random offsets for this cell and point index
						double offsetX = GetRandomOffset(currentCellX, currentCellY, i * 2);
						double offsetY = GetRandomOffset(currentCellX, currentCellY, i * 2 + 1);

						double pointX = currentCellX + offsetX;
						double pointY = currentCellY + offsetY;

						// Calculate distance
						double dx = pointX - x;
						double dy = pointY - y;
						double dist = CalculateDistance(dx, dy, 0, 0, 2);

						// Update closest distances
						if (dist < minDist1)
						{
							nPointX = currentCellX;
							nPointY = currentCellY;
							nPointI = i;

							minDist2 = minDist1;
							minDist1 = dist;
						}
						else if (dist < minDist2)
						{
							minDist2 = dist;
						}
					}
				}
			}

			int pointHash = 0;
			pointHash ^= nPointX * 0x7123a8c1;
			pointHash ^= nPointY * 0x4a5b9c2d;
			pointHash ^= nPointI * 0x5123a8c1;
			double featureValue = GetFeatureValue(minDist1, minDist2, pointHash);
			return featureValue * 2.0 - 1.0;
		}

		protected override double GenerateCore(double x, double y, double z)
		{
			int cellX = (int)Math.Floor(x);
			int cellY = (int)Math.Floor(y);
			int cellZ = (int)Math.Floor(z);

			double minDist1 = double.MaxValue;
			double minDist2 = double.MaxValue;

			int nPointX = 0, nPointY = 0, nPointZ = 0, nPointI = 0;

			// Check the cell and its neighbors
			for (int nx = -1; nx <= 1; nx++)
			{
				for (int ny = -1; ny <= 1; ny++)
				{
					for (int nz = -1; nz <= 1; nz++)
					{
						int currentCellX = cellX + nx;
						int currentCellY = cellY + ny;
						int currentCellZ = cellZ + nz;

						// Generate feature points for this cell
						for (int i = 0; i < PointsPerCell; i++)
						{
							// Get deterministic random offsets for this cell and point index
							double offsetX = GetRandomOffset(currentCellX, currentCellY, currentCellZ, i * 3);
							double offsetY = GetRandomOffset(currentCellX, currentCellY, currentCellZ, i * 3 + 1);
							double offsetZ = GetRandomOffset(currentCellX, currentCellY, currentCellZ, i * 3 + 2);

							double pointX = currentCellX + offsetX;
							double pointY = currentCellY + offsetY;
							double pointZ = currentCellZ + offsetZ;

							// Calculate distance
							double dx = pointX - x;
							double dy = pointY - y;
							double dz = pointZ - z;
							double dist = CalculateDistance(dx, dy, dz, 0, 3);

							// Update closest distances
							if (dist < minDist1)
							{
								nPointX = currentCellX;
								nPointY = currentCellY;
								nPointZ = currentCellZ;
								nPointI = i;

								minDist2 = minDist1;
								minDist1 = dist;
							}
							else if (dist < minDist2)
							{
								minDist2 = dist;
							}
						}
					}
				}
			}

			int pointHash = 0;
			pointHash ^= nPointX * 0x7123a8c1;
			pointHash ^= nPointY * 0x4a5b9c2d;
			pointHash ^= nPointZ * 0x1e3f4a5b;
			pointHash ^= nPointI * 0x5123a8c1;
			double featureValue = GetFeatureValue(minDist1, minDist2, pointHash);
			return featureValue * 2.0 - 1.0;
		}

		protected override double GenerateCore(double x, double y, double z, double w)
		{
			int cellX = (int)Math.Floor(x);
			int cellY = (int)Math.Floor(y);
			int cellZ = (int)Math.Floor(z);
			int cellW = (int)Math.Floor(w);

			double minDist1 = double.MaxValue;
			double minDist2 = double.MaxValue;

			int nPointX = 0, nPointY = 0, nPointZ = 0, nPointW = 0, nPointI = 0;

			// Check the cell and its neighbors
			for (int nx = -1; nx <= 1; nx++)
			{
				for (int ny = -1; ny <= 1; ny++)
				{
					for (int nz = -1; nz <= 1; nz++)
					{
						for (int nw = -1; nw <= 1; nw++)
						{
							int currentCellX = cellX + nx;
							int currentCellY = cellY + ny;
							int currentCellZ = cellZ + nz;
							int currentCellW = cellW + nw;

							// Generate feature points for this cell
							for (int i = 0; i < PointsPerCell; i++)
							{
								// Get deterministic random offsets for this cell and point index
								double offsetX = GetRandomOffset(currentCellX, currentCellY, currentCellZ, currentCellW, i * 4);
								double offsetY = GetRandomOffset(currentCellX, currentCellY, currentCellZ, currentCellW, i * 4 + 1);
								double offsetZ = GetRandomOffset(currentCellX, currentCellY, currentCellZ, currentCellW, i * 4 + 2);
								double offsetW = GetRandomOffset(currentCellX, currentCellY, currentCellZ, currentCellW, i * 4 + 3);

								double pointX = currentCellX + offsetX;
								double pointY = currentCellY + offsetY;
								double pointZ = currentCellZ + offsetZ;
								double pointW = currentCellW + offsetW;

								// Calculate distance
								double dx = pointX - x;
								double dy = pointY - y;
								double dz = pointZ - z;
								double dw = pointW - w;
								double dist = CalculateDistance(dx, dy, dz, dw, 4);

								// Update closest distances
								if (dist < minDist1)
								{
									nPointX = currentCellX;
									nPointY = currentCellY;
									nPointZ = currentCellZ;
									nPointW = currentCellW;
									nPointI = i;

									minDist2 = minDist1;
									minDist1 = dist;
								}
								else if (dist < minDist2)
								{
									minDist2 = dist;
								}
							}
						}
					}
				}
			}

			int pointHash = 0;
			pointHash ^= nPointX * 0x7123a8c1;
			pointHash ^= nPointY * 0x4a5b9c2d;
			pointHash ^= nPointZ * 0x1e3f4a5b;
			pointHash ^= nPointW * 0x164f432a;
			pointHash ^= nPointI * 0x5123a8c1;
			double featureValue = GetFeatureValue(minDist1, minDist2, pointHash);
			return featureValue * 2.0 - 1.0;
		}

		/// <summary>
		/// Calculates the value based on the selected Voronoi feature.
		/// </summary>
		private double GetFeatureValue(double f1, double f2, int pointHash)
		{
			double result;
			switch (Feature)
			{
				default:
				case VoronoiFeature.F1:
					result = f1;
					break;

				case VoronoiFeature.F2:
					result = f2;
					break;

				case VoronoiFeature.F2MinusF1:
					result = f2 - f1;
					break;

				case VoronoiFeature.Average:
					result = (f1 + f2) * 0.5;
					break;

				case VoronoiFeature.Product:
					result = f1 * f2;
					break;

				case VoronoiFeature.RandomValue:
					// Generate a deterministic random value based on the closest cell
					result = (double)(pointHash & 0x7FFFFFFF) / int.MaxValue; // Normalize to 0-1
					break;
			}
			return result;
		}

		/// <summary>
		/// Calculates distance based on the selected metric.
		/// </summary>
		private double CalculateDistance(double dx, double dy, double dz, double dw, double dimensions)
		{
			switch (Metric)
			{
				default:
				case VoronoiDistanceMetric.Euclidean:
					double sum = dx * dx + dy * dy + dz * dz + dw * dw;
					return Math.Sqrt(sum) / Math.Sqrt(dimensions / 2.0);

				case VoronoiDistanceMetric.Manhattan:
					double mandist = Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dz) + Math.Abs(dw);
					return mandist / Math.Sqrt(dimensions);

				case VoronoiDistanceMetric.Chebyshev:
					double max1 = Math.Max(Math.Abs(dx), Math.Abs(dy));
					double max2 = Math.Max(Math.Abs(dz), Math.Abs(dw));
					return Math.Max(max1, max2);

				case VoronoiDistanceMetric.Quadratic:
					sum = dx * dx + dy * dy + dz * dz + dw * dw;
					return sum / Math.Sqrt(dimensions / 2.0);
			}
		}

		/// <summary>
		/// Gets a deterministic random offset for a 1D cell.
		/// </summary>
		private double GetRandomOffset(int cellX, int index)
		{
			int hash = Hash(cellX, index);
			return (hash & 0xFFFF) / 65535.0; // Normalize to 0-1
		}

		/// <summary>
		/// Gets a deterministic random offset for a 2D cell.
		/// </summary>
		private double GetRandomOffset(int cellX, int cellY, int index)
		{
			int hash = Hash(cellX, cellY, index);
			return (hash & 0xFFFF) / 65535.0; // Normalize to 0-1
		}

		/// <summary>
		/// Gets a deterministic random offset for a 3D cell.
		/// </summary>
		private double GetRandomOffset(int cellX, int cellY, int cellZ, int index)
		{
			int hash = Hash(cellX, cellY, cellZ, index);
			return (hash & 0xFFFF) / 65535.0; // Normalize to 0-1
		}

		/// <summary>
		/// Gets a deterministic random offset for a 4D cell.
		/// </summary>
		private double GetRandomOffset(int cellX, int cellY, int cellZ, int cellW, int index)
		{
			int hash = Hash(cellX, cellY, cellZ, cellW, index);
			return (hash & 0xFFFF) / 65535.0; // Normalize to 0-1
		}

		private int Hash(int x, int index)
		{
			unchecked
			{
				int hash = Seed;
				hash = hash * 16777619 ^ x;
				hash = hash * 16777619 ^ index;

				hash ^= hash >> 16;
				hash *= 0x75ebca6b;
				hash ^= hash >> 13;
				hash *= 0x52b2ae35;
				hash ^= hash >> 16;

				return hash;
			}
		}

		private int Hash(int x, int y, int index)
		{
			unchecked
			{
				int hash = Seed;
				hash = hash * 16777619 ^ x;
				hash = hash * 16777619 ^ y;
				hash = hash * 16777619 ^ index;

				hash ^= hash >> 16;
				hash *= 0x75ebca6b;
				hash ^= hash >> 13;
				hash *= 0x52b2ae35;
				hash ^= hash >> 16;

				return hash;
			}
		}

		private int Hash(int x, int y, int z, int index)
		{
			unchecked
			{
				int hash = Seed;
				hash = hash * 16777619 ^ x;
				hash = hash * 16777619 ^ y;
				hash = hash * 16777619 ^ z;
				hash = hash * 16777619 ^ index;

				hash ^= hash >> 16;
				hash *= 0x75ebca6b;
				hash ^= hash >> 13;
				hash *= 0x52b2ae35;
				hash ^= hash >> 16;

				return hash;
			}
		}

		private int Hash(int x, int y, int z, int w, int index)
		{
			unchecked
			{
				int hash = Seed;
				hash = hash * 16777619 ^ x;
				hash = hash * 16777619 ^ y;
				hash = hash * 16777619 ^ z;
				hash = hash * 16777619 ^ w;
				hash = hash * 16777619 ^ index;

				hash ^= hash >> 16;
				hash *= 0x75ebca6b;
				hash ^= hash >> 13;
				hash *= 0x52b2ae35;
				hash ^= hash >> 16;

				return hash;
			}
		}
	}
}