namespace LLMDesktopAssistant.Utils.Noise
{
	public class SineNoise : NoiseBase
	{
		protected override double GenerateCore(double x)
		{
			return Math.Sin(x * Math.PI * 2);
		}

		protected override double GenerateCore(double x, double y)
		{
			return Math.Sin(x * Math.PI * 2) * Math.Sin(y * Math.PI * 2);
		}

		protected override double GenerateCore(double x, double y, double z)
		{
			return Math.Sin(x * Math.PI * 2) * Math.Sin(y * Math.PI * 2) * Math.Sin(z * Math.PI * 2);
		}

		protected override double GenerateCore(double x, double y, double z, double w)
		{
			return Math.Sin(x * Math.PI * 2) *
				Math.Sin(y * Math.PI * 2) *
				Math.Sin(z * Math.PI * 2) *
				Math.Sin(w * Math.PI * 2);
		}
	}
}