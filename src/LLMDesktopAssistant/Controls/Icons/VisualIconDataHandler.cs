using Avalonia.Media;
using Material.Icons;

namespace LLMDesktopAssistant.Controls.Icons
{
	/// <summary>
	/// Resolves a <see cref="VisualIconKind"/> into renderable SVG path data.
	/// </summary>
	/// <remarks>
	/// This is the single semantic authority for icons: parsing (<see cref="VisualIconKind.TryParse"/>)
	/// is syntax-only, while the decision "does this pack exist / are these data valid" lives here.
	/// Both the Avalonia control and the Blazor component consume this handler, which is why it
	/// returns plain path data rather than a <see cref="Geometry"/>.
	/// </remarks>
	public static class VisualIconDataHandler
	{
		private static readonly MaterialIconDataProvider _materialProvider = new();
		private static readonly Dictionary<string, MaterialIconKind> _materialNames = BuildMaterialNames();

		/// <summary>Fallback glyph (a material "alert circle") shown for invalid icons.</summary>
		private static readonly string? _fallbackPath = SafeProvide(MaterialIconKind.AlertCircleOutline);

		/// <summary>The fallback glyph path, or <see langword="null"/> when it is unavailable.</summary>
		public static string? FallbackPath => _fallbackPath;

		/// <summary>Resolves a nullable icon reference. <see langword="null"/> and <see cref="VisualIconKind.None"/> map to "no icon".</summary>
		public static VisualIconData Resolve(VisualIconKind? kind)
		{
			if (kind is not { } value || value.IsNone)
				return new VisualIconData(VisualIconStatus.None, null);

			return Resolve(value.Pack, value.Data);
		}

		/// <summary>Resolves an icon reference from a pack and its data.</summary>
		public static VisualIconData Resolve(IconPackKind pack, string? data)
		{
			switch (pack)
			{
				case IconPackKind.None:
					return new VisualIconData(VisualIconStatus.None, null);

				case IconPackKind.Material:
					return ResolveMaterial(data);

				case IconPackKind.Path:
					return ResolvePath(data);

				case IconPackKind.Unknown:
				default:
					return new VisualIconData(VisualIconStatus.InvalidPack, _fallbackPath);
			}
		}

		private static VisualIconData ResolveMaterial(string? data)
		{
			if (string.IsNullOrEmpty(data) || !_materialNames.TryGetValue(data, out var kind))
				return new VisualIconData(VisualIconStatus.InvalidData, _fallbackPath);

			var path = SafeProvide(kind);
			return path is null
				? new VisualIconData(VisualIconStatus.InvalidData, _fallbackPath)
				: new VisualIconData(VisualIconStatus.Ok, path);
		}

		private static VisualIconData ResolvePath(string? data)
		{
			if (string.IsNullOrEmpty(data))
				return new VisualIconData(VisualIconStatus.InvalidData, _fallbackPath);

			try
			{
				// Path data has no known design box, so validation is "can it be parsed at all".
				_ = Geometry.Parse(data);
				return new VisualIconData(VisualIconStatus.Ok, data);
			}
			catch
			{
				return new VisualIconData(VisualIconStatus.InvalidData, _fallbackPath);
			}
		}

		private static Dictionary<string, MaterialIconKind> BuildMaterialNames()
		{
			var result = new Dictionary<string, MaterialIconKind>(StringComparer.OrdinalIgnoreCase);
			foreach (var kind in Enum.GetValues<MaterialIconKind>())
				result.TryAdd(kind.ToString(), kind);

			return result;
		}

		private static string? SafeProvide(MaterialIconKind kind)
		{
			try
			{
				return _materialProvider.ProvideData(kind);
			}
			catch
			{
				return null;
			}
		}
	}
}
