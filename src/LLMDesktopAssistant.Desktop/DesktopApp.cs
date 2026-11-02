using Avalonia.Markup.Xaml.Styling;
using LLMDesktopAssistant.Cli;
using LLMDesktopAssistant.Settings.Application;

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

		protected override object? CreateSplashContent()
		{
			if (CommandLineExecutor.Parameters.NoSplash)
				return null;

			if (!ApplicationStartupSettingsAccessor.Current.ShowSplash)
				return null;

			return new SplashWindow();
		}
	}
}
