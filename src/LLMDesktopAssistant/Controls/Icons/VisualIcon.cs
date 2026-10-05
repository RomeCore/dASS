using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
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
	/// Size comes from <see cref="FontSize"/> (or an explicit <c>Width</c>/<c>Height</c>); the
	/// brush comes from the inherited <c>Foreground</c>. Invalid icons are rendered in red using
	/// the <see cref="VisualIconDataHandler"/> fallback glyph.
	/// </para>
	/// </remarks>
	public class VisualIcon : Control
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

		/// <summary>Defines the <see cref="Foreground"/> property (the inherited text foreground).</summary>
		public static readonly StyledProperty<IBrush?> ForegroundProperty =
			TextElement.ForegroundProperty.AddOwner<VisualIcon>();

		/// <summary>Defines the <see cref="FontSize"/> property, which is the default icon size.</summary>
		public static readonly StyledProperty<double> FontSizeProperty =
			AvaloniaProperty.Register<VisualIcon, double>(nameof(FontSize), 16d, inherits: true);

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

		/// <summary>The brush used to paint a valid glyph. Invalid glyphs are always painted red.</summary>
		public IBrush? Foreground
		{
			get => GetValue(ForegroundProperty);
			set => SetValue(ForegroundProperty, value);
		}

		/// <summary>The default glyph size when no explicit <c>Width</c>/<c>Height</c> is set.</summary>
		public double FontSize
		{
			get => GetValue(FontSizeProperty);
			set => SetValue(FontSizeProperty, value);
		}

		/// <inheritdoc />
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);

			if (change.Property == KindProperty)
			{
				UpdateGeometry();

				// When Kind is assigned directly (not through a shortcut), the shortcuts are stale.
				if (!_internal)
					ClearShortcuts();
			}
			else if (change.Property == MaterialProperty)
			{
				if (_internal)
					return;

				SetKindFromShortcut(Material.HasValue ? (VisualIconKind)Material.Value : null);
			}
			else if (change.Property == PathProperty)
			{
				if (_internal)
					return;

				SetKindFromShortcut(Path is { } path ? VisualIconKind.FromPath(path) : null);
			}
			else if (change.Property == ForegroundProperty || change.Property == FontSizeProperty)
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

			return new Size(size, size);
		}

		/// <inheritdoc />
		public override void Render(DrawingContext context)
		{
			base.Render(context);

			if (_geometry is not { } geometry)
				return;

			var bounds = new Rect(Bounds.Size);
			if (bounds.Width <= 0 || bounds.Height <= 0)
				return;

			var brush = _status == VisualIconStatus.Ok ? Foreground ?? Brushes.Black : ErrorBrush;
			if (brush is null)
				return;

			var box = _designBox;
			if (box.Width <= 0 || box.Height <= 0)
				return;

			var scale = Math.Min(bounds.Width / box.Width, bounds.Height / box.Height);
			var offsetX = (bounds.Width - box.Width * scale) / 2d - box.X * scale;
			var offsetY = (bounds.Height - box.Height * scale) / 2d - box.Y * scale;

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
			_status = data.Status;

			_geometry = null;
			_designBox = DefaultDesignBox;

			if (!string.IsNullOrEmpty(data.SvgPath))
			{
				try
				{
					_geometry = Geometry.Parse(data.SvgPath!);
				}
				catch
				{
					_geometry = null;
				}
			}

			if (_geometry is not null)
			{
				// Arbitrary path data has no known design box, so it is scaled by its own bounds.
				// Material glyphs (and the error fallback, which is a material glyph) use the 24x24 grid.
				_designBox = data.Status == VisualIconStatus.Ok && Kind?.Pack == IconPackKind.Path
					? _geometry.Bounds
					: DefaultDesignBox;
			}

			UpdateErrorTooltip(data);
			InvalidateVisual();
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
