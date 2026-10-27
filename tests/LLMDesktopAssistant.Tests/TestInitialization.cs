using System.Runtime.CompilerServices;
using LLMDesktopAssistant.Settings.Application;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Tests;

internal static class TestInitialization
{
	[ModuleInitializer]
	internal static void Initialize()
	{
		ReflectionUtility.Initialize(AppDomain.CurrentDomain);
		ApplicationSettingsAccessor.SetApplicationSettings(new());
		// No dispatcher loop runs here, so every UI-thread call must execute inline.
		UIDispatcher.Delegating = false;
	}
}
