using System.Reflection;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Startup.Stages
{
	/// <summary>
	/// The stage that loads the plugins and registers the additional assemblies. It must run before
	/// <see cref="ReflectionScanStage"/>, otherwise the plugins never reach the type scan.
	/// </summary>
	/// <param name="additionalAssemblies">The assemblies the host wants observed on top of the plugins.</param>
	public sealed class PluginsStage(params Assembly[] additionalAssemblies) : IStartupStage
	{
		/// <inheritdoc />
		public LocaleKeyBase Title => Locale.GetKey("splash.stage.plugins");

		/// <inheritdoc />
		public Task RunAsync(CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();

			PluginManager.LoadPluginsInto(AppDomain.CurrentDomain);

			foreach (var assembly in additionalAssemblies)
				ReflectionUtility.AddAdditionalAssembly(assembly, observe: true);

			return Task.CompletedTask;
		}
	}
}
