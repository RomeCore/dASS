using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Material.Icons;

namespace LLMDesktopAssistant.Controls.Icons
{
	/// <summary>
	/// Renders a <see cref="VisualIconKind"/> as a tintable monochrome glyph.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="Kind"/> is the single source of truth for rendering. <see cref="Material"/>
	/// and <see cref="Path"/> are explicit, symmetric shortcuts: assigning one of them writes
	/// the corresponding <see cref="Kind"/>, and assigning <see cref="Kind"/> directly clears them.
	/// </para>
	/// <para>
	/// The control derives from <see cref="TemplatedControl"/> so that it carries the usual
	/// background, border, corner-radius and padding properties, and inherits the text
	/// <c>Foreground</c>/<c>FontSize</c>. The background is painted explicitly so the whole box
	/// participates in hit-testing (tooltips, icon buttons) even when the glyph is thin.
	/// </para>
	/// <para>
	/// Size comes from <c>FontSize</c> (or an explicit <c>Width</c>/<c>Height</c>). Invalid icons
	/// are rendered in red using the <see cref="VisualIconDataHandler"/> fallback glyph.
	/// </para>
	/// </remarks>
	public class VisualIcon : TemplatedControl
	{
		/// <summary>Defines the <see cref="Kind"/> property.</summary>
		public static readonly StyledProperty<VisualIconKind?> KindProperty =
			AvaloniaProperty.Register<VisualIcon, VisualIconKind?>(nameof(Kind));

		/// <summary>Defines the <see cref="Material"/> property.</summary>
		public static readonly StyledProperty<MaterialIconKind?> MaterialProperty =
			AvaloniaProperty.Register<VisualIcon, MaterialIconKind?>(nameof(Material));

		/// <summary>Defines the <see cref="Path"/> property.</summary>
		public static readonly StyledProperty<string?> PathProperty =
			AvaloniaProperty.Register<VisualIcon, string?>(nameof(Path));

		private const double DefaultSize = 16d;

		/// <summary>The canonical material design grid; every material glyph lives inside it.</summary>
		private static readonly Rect DefaultDesignBox = new(0, 0, 24, 24);

		private static readonly IBrush ErrorBrush = new SolidColorBrush(Colors.Red);

		private Geometry? _geometry;
		private Rect _designBox = DefaultDesignBox;
		private VisualIconStatus _status = VisualIconStatus.None;
		private bool _internal;
		private bool _ownsTooltip;

		/// <summary>The icon to render. <see langword="null"/> or <see cref="VisualIconKind.None"/> renders nothing.</summary>
		public VisualIconKind? Kind
		{
			get => GetValue(KindProperty);
			set => SetValue(KindProperty, value);
		}

		/// <summary>Explicit shortcut for a material icon; equivalent to <c>Kind="material:&lt;kind&gt;"</c>.</summary>
		public MaterialIconKind? Material
		{
			get => GetValue(MaterialProperty);
			set => SetValue(MaterialProperty, value);
		}

		/// <summary>Explicit shortcut for raw SVG path data; equivalent to <c>Kind="path:&lt;path&gt;"</c>.</summary>
		public string? Path
		{
			get => GetValue(PathProperty);
			set => SetValue(PathProperty, value);
		}

		/// <inheritdoc />
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);

			var property = change.Property;

			if (property == KindProperty)
			{
				UpdateGeometry();

				// When Kind is assigned directly (not through a shortcut), the shortcuts are stale.
				if (!_internal)
					ClearShortcuts();
			}
			else if (property == MaterialProperty)
			{
				if (_internal)
					return;

				SetKindFromShortcut(Material.HasValue ? (VisualIconKind)Material.Value : null);
			}
			else if (property == PathProperty)
			{
				if (_internal)
					return;

				SetKindFromShortcut(Path is { } path ? VisualIconKind.FromPath(path) : null);
			}
			else if (property == BackgroundProperty
				|| property == ForegroundProperty
				|| property == BorderBrushProperty
				|| property == BorderThicknessProperty
				|| property == CornerRadiusProperty
				|| property == PaddingProperty
				|| property == FontSizeProperty)
			{
				InvalidateVisual();
			}
		}

		/// <inheritdoc />
		protected override Size MeasureOverride(Size availableSize)
		{
			var size = FontSize;
			if (double.IsNaN(size) || size <= 0)
				size = DefaultSize;

			return new Size(size + Padding.Left + Padding.Right, size + Padding.Top + Padding.Bottom);
		}

		/// <inheritdoc />
		public override void Render(DrawingContext context)
		{
			base.Render(context);

			var bounds = new Rect(Bounds.Size);
			if (bounds.Width <= 0 || bounds.Height <= 0)
				return;

			// Paint the background so the entire box is hit-testable (tooltips, icon buttons),
			// not just the thin glyph outline.
			if (Background is { } background)
				context.DrawRectangle(background, null, new RoundedRect(bounds, CornerRadius));

			if (BorderBrush is { } borderBrush && BorderThickness != default)
				context.DrawRectangle(null, new Pen(borderBrush, BorderThickness.Top), new RoundedRect(bounds.Deflate(BorderThickness), CornerRadius));

			if (_geometry is not { } geometry)
				return;

			var content = bounds.Deflate(Padding).Deflate(BorderThickness);
			if (content.Width <= 0 || content.Height <= 0)
				return;

			var brush = _status == VisualIconStatus.Ok ? Foreground ?? Brushes.Black : ErrorBrush;
			if (brush is null)
				return;

			var box = _designBox;
			if (box.Width <= 0 || box.Height <= 0)
				return;

			var scale = Math.Min(content.Width / box.Width, content.Height / box.Height);
			var offsetX = content.X + (content.Width - box.Width * scale) / 2d - box.X * scale;
			var offsetY = content.Y + (content.Height - box.Height * scale) / 2d - box.Y * scale;

			using (context.PushTransform(new Matrix(scale, 0, 0, scale, offsetX, offsetY)))
			{
				context.DrawGeometry(brush, null, geometry);
			}
		}

		private void SetKindFromShortcut(VisualIconKind? kind)
		{
			var wasInternal = _internal;
			_internal = true;
			try
			{
				SetCurrentValue(KindProperty, kind);
			}
			finally
			{
				_internal = wasInternal;
			}
		}

		private void ClearShortcuts()
		{
			var wasInternal = _internal;
			_internal = true;
			try
			{
				if (Material is not null)
					SetCurrentValue(MaterialProperty, null);
				if (Path is not null)
					SetCurrentValue(PathProperty, null);
			}
			finally
			{
				_internal = wasInternal;
			}
		}

		private void UpdateGeometry()
		{
			var data = VisualIconDataHandler.Resolve(Kind);
			_geometry = TryParseGeometry(data.SvgPath);

			if (_geometry is null && data.Status == VisualIconStatus.Ok)
			{
				// This control is the authoritative parser: data that passes the handler's cheap
				// syntax check can still fail to parse here (malformed commands).
				data = new VisualIconData(VisualIconStatus.InvalidData, VisualIconDataHandler.FallbackPath);
				_geometry = TryParseGeometry(data.SvgPath);
			}

			_status = data.Status;

			// Arbitrary path data has no known design box, so it is scaled by its own bounds.
			// Material glyphs (and the error fallback, which is a material glyph) use the 24x24 grid.
			_designBox = data.Status == VisualIconStatus.Ok && Kind?.Pack == IconPackKind.Path && _geometry is not null
				? _geometry.Bounds
				: DefaultDesignBox;

			UpdateErrorTooltip(data);
			InvalidateVisual();
		}

		private static Geometry? TryParseGeometry(string? path)
		{
			if (string.IsNullOrEmpty(path))
				return null;

			try
			{
				return Geometry.Parse(path);
			}
			catch
			{
				return null;
			}
		}

		private void UpdateErrorTooltip(VisualIconData data)
		{
			if (data.IsError)
			{
				// Never clobber a tooltip set by the host.
				if (!_ownsTooltip && ToolTip.GetTip(this) is not null)
					return;

				var raw = Kind?.ToString() ?? string.Empty;
				var message = data.Status == VisualIconStatus.InvalidPack
					? $"Unknown icon pack in \"{raw}\"."
					: $"Invalid icon data in \"{raw}\".";

				ToolTip.SetTip(this, message);
				_ownsTooltip = true;
			}
			else if (_ownsTooltip)
			{
				ToolTip.SetTip(this, null);
				_ownsTooltip = false;
			}
		}
	}
}
