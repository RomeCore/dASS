using System.Runtime.CompilerServices;
using LLMDesktopAssistant.LLM.MVVM.Additional;
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
		// Disable raising events in the UI thread for additional chat data collections
		// otherwise is causes constant deadlocks when running tests concurrently.
		AdditionalChatDataCollection.RaiseInUIThreadGlobal = false;
	}
}
