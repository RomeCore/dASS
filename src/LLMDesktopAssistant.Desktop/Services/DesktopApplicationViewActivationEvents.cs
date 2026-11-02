using Avalonia.Controls;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;

namespace LLMDesktopAssistant.Desktop.Services
{
	[Service(typeof(IApplicationViewActivationEvents))]
	public class DesktopApplicationViewActivationEvents : IApplicationViewActivationEvents
	{
		public event Action? Activated;

		public DesktopApplicationViewActivationEvents()
		{
			// The window does not exist when the framework initialization completes: the startup
			// stages create it later, so the subscription waits for the main content.
			App.OnMainContentCreated(() =>
			{
				if (App.MainWindow is not { } window)
					return;

				window.Activated += (s, e) => Activated?.Invoke();
			});
		}
	}
}
