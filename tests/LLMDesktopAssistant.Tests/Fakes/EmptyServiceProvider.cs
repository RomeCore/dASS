namespace LLMDesktopAssistant.Tests.Fakes
{
	/// <summary>
	/// A service provider that resolves nothing: every lookup answers <see langword="null"/>.
	/// </summary>
	/// <remarks>
	/// Shared rather than re-declared per test file. A private copy per namespace looks harmless until an API grows a
	/// parameter that needs a provider, and then every test file has to grow the same stub at once.
	/// </remarks>
	public sealed class EmptyServiceProvider : IServiceProvider
	{
		public object? GetService(Type serviceType) => null;
	}
}
