using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Controls.Presenters;

namespace LLMDesktopAssistant.Controls.Layout
{
	/// <summary>
	/// Lays its children out by fractions of its own size: the offset from the top-left corner and the size are
	/// attached properties in the range 0..1, and they follow every resize of the panel.
	/// </summary>
	/// <remarks>
	/// Positions are kept in fractions rather than pixels so a child placed in a corner stays in that corner when
	/// the area around it grows or shrinks, and so a layout that has to survive a restart can store it in the model
	/// without knowing any pixel size. The panel always takes all the space it is offered: its children are
	/// positioned relative to that space, so a smaller size would change what the fractions mean. A child is clamped
	/// so it can never leave the panel completely - a widget dragged off the edge and then resized away would be
	/// lost with no way to reach it.
	/// </remarks>
	public class FractionPanel : Panel
	{
		/// <summary>
		/// The offset of a child from the left edge, as a fraction of the panel's width.
		/// </summary>
		public static readonly AttachedProperty<double?> RelativeLeftProperty =
			AvaloniaProperty.RegisterAttached<FractionPanel, Control, double?>("RelativeLeft");

		/// <summary>
		/// The offset of a child from the top edge, as a fraction of the panel's height.
		/// </summary>
		public static readonly AttachedProperty<double?> RelativeTopProperty =
			AvaloniaProperty.RegisterAttached<FractionPanel, Control, double?>("RelativeTop");

		/// <summary>
		/// The width of a child, as a fraction of the panel's width; null lets the child's content decide.
		/// </summary>
		public static readonly AttachedProperty<double?> RelativeWidthProperty =
			AvaloniaProperty.RegisterAttached<FractionPanel, Control, double?>("RelativeWidth");

		/// <summary>
		/// The height of a child, as a fraction of the panel's height; null lets the child's content decide.
		/// </summary>
		public static readonly AttachedProperty<double?> RelativeHeightProperty =
			AvaloniaProperty.RegisterAttached<FractionPanel, Control, double?>("RelativeHeight");

		static FractionPanel()
		{
			RelativeLeftProperty.Changed.AddClassHandler<Control>((child, _) => InvalidateOwner(child));
			RelativeTopProperty.Changed.AddClassHandler<Control>((child, _) => InvalidateOwner(child));
			RelativeWidthProperty.Changed.AddClassHandler<Control>((child, _) => InvalidateOwner(child));
			RelativeHeightProperty.Changed.AddClassHandler<Control>((child, _) => InvalidateOwner(child));

			// The stacking order is the item's own ZIndex, copied onto the element the panel arranges.
			Visual.ZIndexProperty.Changed.AddClassHandler<Control>((child, _) => InvalidateOwner(child));
		}

		/// <summary>
		/// Gets the left fraction of a child.
		/// </summary>
		public static double? GetRelativeLeft(Control child) => child.GetValue(RelativeLeftProperty);

		/// <summary>
		/// Sets the left fraction of a child.
		/// </summary>
		public static void SetRelativeLeft(Control child, double? value) => child.SetValue(RelativeLeftProperty, value);

		/// <summary>
		/// Gets the top fraction of a child.
		/// </summary>
		public static double? GetRelativeTop(Control child) => child.GetValue(RelativeTopProperty);

		/// <summary>
		/// Sets the top fraction of a child.
		/// </summary>
		public static void SetRelativeTop(Control child, double? value) => child.SetValue(RelativeTopProperty, value);

		/// <summary>
		/// Gets the width fraction of a child.
		/// </summary>
		public static double? GetRelativeWidth(Control child) => child.GetValue(RelativeWidthProperty);

		/// <summary>
		/// Sets the width fraction of a child.
		/// </summary>
		public static void SetRelativeWidth(Control child, double? value) => child.SetValue(RelativeWidthProperty, value);

		/// <summary>
		/// Gets the height fraction of a child.
		/// </summary>
		public static double? GetRelativeHeight(Control child) => child.GetValue(RelativeHeightProperty);

		/// <summary>
		/// Sets the height fraction of a child.
		/// </summary>
		public static void SetRelativeHeight(Control child, double? value) => child.SetValue(RelativeHeightProperty, value);

		protected override Size MeasureOverride(Size availableSize)
		{
			bool finiteWidth = !double.IsInfinity(availableSize.Width);
			bool finiteHeight = !double.IsInfinity(availableSize.Height);
			double contentWidth = 0;
			double contentHeight = 0;

			foreach (var child in Children)
			{
				var source = GeometrySource(child);

				if (!child.IsVisible || !source.IsVisible)
					continue;

				child.Measure(MeasureConstraint(availableSize, source));

				// An items control builds the item's root inside the measure of its own presenter, and the fractions that
				// root carries arrive detached, where no change reaches this panel. The root exists now, so the
				// constraint has to be taken from it: a child measured for the whole area keeps that layout inside the
				// smaller frame it is arranged at, and whatever sits at its right and bottom edges - a header's buttons,
				// a resize grip - ends up outside the frame until something unrelated forces a second pass.
				if (GeometrySource(child) is { } settled && !ReferenceEquals(settled, source))
				{
					source = settled;
					child.Measure(MeasureConstraint(availableSize, source));
				}

				contentWidth = Math.Max(contentWidth, child.DesiredSize.Width + Offset(availableSize.Width, GetRelativeLeft(source)));
				contentHeight = Math.Max(contentHeight, child.DesiredSize.Height + Offset(availableSize.Height, GetRelativeTop(source)));
			}

			// The panel owns the whole area: a child sits somewhere in it, so a smaller panel would move it.
			return new Size(
				finiteWidth ? availableSize.Width : contentWidth,
				finiteHeight ? availableSize.Height : contentHeight);
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			foreach (var child in Children)
			{
				var source = GeometrySource(child);

				// The stacking order is the item's: promote it to the element the panel actually stacks.
				if (child.ZIndex != source.ZIndex)
					child.ZIndex = source.ZIndex;

				if (!child.IsVisible || !source.IsVisible)
					continue;

				double width = GetRelativeWidth(source) is { } widthFraction
					? finalSize.Width * widthFraction
					: child.DesiredSize.Width;

				double height = GetRelativeHeight(source) is { } heightFraction
					? finalSize.Height * heightFraction
					: child.DesiredSize.Height;

				width = Math.Clamp(width, 0, Math.Max(0, finalSize.Width));
				height = Math.Clamp(height, 0, Math.Max(0, finalSize.Height));

				double left = Math.Clamp(Offset(finalSize.Width, GetRelativeLeft(source)), 0, Math.Max(0, finalSize.Width - width));
				double top = Math.Clamp(Offset(finalSize.Height, GetRelativeTop(source)), 0, Math.Max(0, finalSize.Height - height));

				child.Arrange(new Rect(left, top, width, height));
			}

			return finalSize;
		}

		/// <summary>
		/// The element the fractions are read from.
		/// </summary>
		/// <remarks>
		/// An items control wraps every item in a content presenter, and the presenter is what this panel lays out - but
		/// the bindings that carry the geometry live on the item's own template root, whose data context is the item.
		/// Reading them there keeps the fractions out of the presenter's context, where they do not resolve.
		/// </remarks>
		private static Control GeometrySource(Control child)
			=> child is ContentPresenter { Child: Control inner } ? inner : child;

		/// <summary>
		/// The measure constraint a child gets: its fractions of the area, or the whole area on an axis it sizes itself.
		/// </summary>
		private static Size MeasureConstraint(Size available, Control source)
			=> new(
				Constraint(available.Width, GetRelativeWidth(source)),
				Constraint(available.Height, GetRelativeHeight(source)));

		/// <summary>
		/// The measure constraint an axis gets: its fraction of the area, or the whole area when it sizes itself.
		/// </summary>
		private static double Constraint(double available, double? fraction)
			=> fraction is { } value && !double.IsInfinity(available) ? available * value : available;

		/// <summary>
		/// A fraction of a size, with an unknown fraction read as zero.
		/// </summary>
		private static double Offset(double size, double? fraction)
			=> double.IsInfinity(size) ? 0 : size * (fraction ?? 0);

		/// <summary>
		/// A fraction changed after the child was laid out: the panel has to measure and arrange again.
		/// </summary>
		/// <remarks>
		/// The fraction usually sits on the item's template root, which is separated from the panel by the container
		/// the items control wrapped it in - so the panel is found by walking up, not by asking for the parent.
		/// </remarks>
		private static void InvalidateOwner(Control child)
		{
			// The element the panel arranges is written by the panel itself: re-arranging for its own write would loop.
			if (child.Parent is FractionPanel)
				return;

			if ((child as Visual)?.FindAncestorOfType<FractionPanel>() is not { } panel)
				return;

			panel.InvalidateMeasure();
			panel.InvalidateArrange();
		}
	}
}
