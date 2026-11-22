namespace LLMDesktopAssistant.Utils.Noise
{
	/// <summary>
	/// A window of noise space to rasterize: a <see cref="Width"/> by <see cref="Height"/> rectangle at (<see cref="X"/>, <see cref="Y"/>).
	/// </summary>
	public readonly record struct NoiseRenderRegion(double X, double Y, double Width, double Height);
}
