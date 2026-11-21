using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using LLMDesktopAssistant.Settings.Application;

namespace LLMDesktopAssistant.Styles.Theming
{
	/// <summary>
	/// Derives the Markdown type scale from the interface one by the Markdown factor the user chose.
	/// </summary>
	/// <remarks>
	/// An instance lives in <c>MarkdownControl.Resources</c>, so its keys shadow the application ones for the
	/// Markdown subtree only and the rest of the window keeps the ordinary scale. Sizes are read back from the
	/// application resources instead of being recomputed, so the profile and the accessibility multiplier reach
	/// Markdown without a second copy of the formula.
	/// </remarks>
	public class MarkdownTypographyProvider : ResourceProvider
	{
		private readonly Dictionary<string, object> _resources = [];

		/// <summary>
		/// Initializes a new instance of the <see cref="MarkdownTypographyProvider"/> class.
		/// </summary>
		public MarkdownTypographyProvider()
		{
			ThemeManager.AttachMarkdownTypography(this);
			Recompute();
		}

		/// <inheritdoc/>
		public override bool HasResources => _resources.Count > 0;

		/// <inheritdoc/>
		public override bool TryGetResource(object key, ThemeVariant? theme, out object? value)
		{
			value = null;
			return key is string name && _resources.TryGetValue(name, out value);
		}

		/// <summary>
		/// Rebuilds the Markdown scale from the current interface one and the Markdown multiplier.
		/// </summary>
		public void Recompute()
		{
			_resources.Clear();

			var factor = ApplicationStartupSettingsAccessor.Current.MarkdownFontScale;
			if (factor <= 0 || double.IsNaN(factor))
				factor = 1;

			if (Application.Current is { } application)
			{
				foreach (var name in TypographyProvider.StepNames)
				{
					Copy(application, "FontSize" + name, factor);
					Copy(application, "LineHeight" + name, factor);
				}
			}

			RaiseResourcesChanged();
		}

		/// <summary>
		/// Scales one interface resource into its Markdown counterpart, leaving the key absent when the
		/// interface has none, so the ordinary value is used instead of a made-up one.
		/// </summary>
		private void Copy(IResourceHost source, string key, double factor)
		{
			if (source.TryFindResource(key, null, out var value) && value is double size)
				_resources[key] = TypographyProvider.RoundHalfPixel(size * factor);
		}
	}
}
