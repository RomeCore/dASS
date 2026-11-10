using Avalonia.Markup.Xaml.Styling;
using LLMDesktopAssistant.Cli;
using LLMDesktopAssistant.Settings.Application;
using LLMDesktopAssistant.Startup;

namespace LLMDesktopAssistant.Desktop
{
	public class DesktopApp : App
	{
		public override void Initialize()
		{
			// Plugin loading and the Blazor assembly registration are the PluginsStage, so that they
			// appear in the startup progress like every other expensive step.
			Styles.Add(new StyleInclude(new Uri("avares://LiveMarkdown.Avalonia.Mermaid/Styles.axaml"))
			{
				Source = new Uri("avares://LiveMarkdown.Avalonia.Mermaid/Styles.axaml")
			});

			base.Initialize();
		}

		protected override IStartupSplashHandle? CreateSplashContent()
		{
			return new SplashWindow();
		}
	}
}
