using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using LLMDesktopAssistant.Settings.Application;

namespace LLMDesktopAssistant.Styles.Theming
{
	/// <summary>
	/// Generates the corner radius vocabulary from a single base: the base is <c>M</c>, and every other
	/// size, direction and inner radius is derived from it.
	/// </summary>
	/// <remarks>
	/// Directional and inner radii exist so that authors never hand-write <c>CornerRadius="12,12,0,0"</c>
	/// or compute an inner radius from the padding: both are the usual source of corners that look wrong
	/// only in one place.
	/// </remarks>
	public class RadiusProvider : ResourceProvider
	{
		private const double innerPadding = 4;
		private const double pill = 9999;

		private static readonly (string Name, double Factor)[] sizes =
		[
			("S", 0.5), ("M", 1.0), ("L", 1.5), ("Xl", 2.0),
		];

		private readonly Dictionary<string, object> _resources = [];

		/// <summary>
		/// Initializes a new instance of the <see cref="RadiusProvider"/> class from the startup settings.
		/// </summary>
		public RadiusProvider()
		{
			ThemeManager.AttachRadius(this);
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
		/// Rebuilds the radius vocabulary from the current base.
		/// </summary>
		public void Recompute()
		{
			var settings = ApplicationStartupSettingsAccessor.Current;
			var baseRadius = settings.RadiusBase;

			_resources["RadiusNone"] = new CornerRadius(0);
			_resources["RadiusPill"] = new CornerRadius(pill);

			foreach (var (name, factor) in sizes)
			{
				var radius = baseRadius * factor;

				_resources["Radius" + name] = new CornerRadius(radius);
				_resources["RadiusTop" + name] = new CornerRadius(radius, radius, 0, 0);
				_resources["RadiusRight" + name] = new CornerRadius(0, radius, radius, 0);
				_resources["RadiusBottom" + name] = new CornerRadius(0, 0, radius, radius);
				_resources["RadiusLeft" + name] = new CornerRadius(radius, 0, 0, radius);
				_resources["RadiusInner" + name] = new CornerRadius(Math.Max(0, baseRadius * factor - innerPadding));
			}

			RaiseResourcesChanged();
		}
	}
}
