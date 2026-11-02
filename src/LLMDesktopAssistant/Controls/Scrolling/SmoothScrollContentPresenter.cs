using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace LLMDesktopAssistant.Controls.Scrolling
{
	/// <summary>
	/// The content presenter <see cref="SmoothScrollViewer"/> puts into its template.
	/// </summary>
	/// <remarks>
	/// <para>
	/// It exists for exactly two reasons: the mouse wheel and <c>BringIntoView</c>. Both are answered by the
	/// stock presenter with an instantaneous jump of the offset, and both are turned here into a retargeting
	/// of the owner's chase (<see cref="SmoothScrollViewer.HandlePointerWheel"/>, <see cref="SmoothScrollViewer.AnimateTo(Vector)"/>)
	/// so the visible offset glides instead.
	/// </para>
	/// <para>
	/// Taking the wheel over here - instead of in a tunnel handler on the scroll viewer - is what makes
	/// nested scrolling work. <see cref="InputElement.OnPointerWheelChanged"/> runs in the bubbling phase
	/// and only while nothing deeper has handled the event yet, so a terminal, a combo box or a nested
	/// <see cref="Avalonia.Controls.ScrollViewer"/> under the pointer gets the notch first. If that nested
	/// viewer is already at its limit it leaves the event unhandled, the wheel chains outward, reaches this
	/// presenter and is animated smoothly by the outer viewer.
	/// </para>
	/// <para>
	/// <c>BringIntoView</c> is taken over the same way: the stock handler is allowed to compute the
	/// destination - which is the whole point, it owns the child padding, margin, content origin and the
	/// logical-scrollable path - and the jump it makes on the way is then undone and replayed by the chase.
	/// A request that moved nothing (already visible), or one a nested viewer answered for itself, is left
	/// alone, so the event chain keeps the stock semantics.
	/// </para>
	/// <para>
	/// The catch is that <see cref="Control.RequestBringIntoViewEvent"/> is registered as a bubbling-only
	/// event, so there is no tunnelling phase in which the offset could be read once more before the stock
	/// handler jumps it. The jump is an offset change like any other, so the value it replaced is tracked
	/// instead - see <see cref="OnPropertyChanged"/>.
	/// </para>
	/// </remarks>
	public class SmoothScrollContentPresenter : ScrollContentPresenter
	{
		/// <summary>Closer than this to the pre-request offset means the request scrolled nothing.</summary>
		private const double MovedTolerance = 0.5d;

		private SmoothScrollViewer? _owner;

		/// <summary>The offset that the current <see cref="ScrollContentPresenter.Offset"/> value replaced.</summary>
		private Vector _offsetBeforeLastChange;

		public SmoothScrollContentPresenter()
		{
			// Registered after the stock handler (the base constructor registers its own), so by the time this
			// runs the jump has landed and its destination can be read back. `handledEventsToo` is required:
			// the stock handler marks the event handled exactly when it did scroll.
			AddHandler(
				RequestBringIntoViewEvent,
				OnBringIntoViewCompleted,
				RoutingStrategies.Bubble,
				handledEventsToo: true);
		}

		protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
		{
			var owner = _owner ??= this.FindAncestorOfType<SmoothScrollViewer>();

			if (owner == null)
			{
				// Hosted outside a smooth viewer: keep the stock instant behaviour.
				base.OnPointerWheelChanged(e);
				return;
			}

			// Deliberately no base call: the stock implementation snaps the offset to the new value.
			var delta = e.Delta;

			// Shift + wheel scrolls horizontally, exactly like ScrollContentPresenter does.
			if (e.KeyModifiers == KeyModifiers.Shift && delta.X == 0d)
				delta = new Vector(delta.Y, delta.X);

			// Nothing moved means we are already at the limit in that direction: leave the event
			// unhandled so it chains to an outer scroll viewer.
			e.Handled = owner.HandlePointerWheel(delta) || !IsScrollChainingEnabled;
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);

			// The brought-into-view jump is an ordinary offset change, and its old value is where the request
			// started from - the only chance to learn it, since the event has no tunnel phase.
			if (change.Property == OffsetProperty)
				_offsetBeforeLastChange = change.GetOldValue<Vector>();
		}

		/// <summary>
		/// Replays the instantaneous jump the stock handler just made as a smooth scroll of the owner.
		/// </summary>
		private void OnBringIntoViewCompleted(object? sender, RequestBringIntoViewEventArgs e)
		{
			// Nothing needed scrolling: the target was already visible, or the request was raised before
			// layout - in which case Avalonia queues it and raises it again once the control is arranged.
			if (!e.Handled)
				return;

			var destination = Offset;
			var origin = _offsetBeforeLastChange;

			// A nested viewer answered the request for itself: it moved its own offset, not ours.
			if ((destination - origin).Length <= MovedTolerance)
				return;

			var owner = _owner ??= this.FindAncestorOfType<SmoothScrollViewer>();

			if (owner == null)
				return;

			// Undo the jump and let the chase replay it from where it started. Nothing can be rendered in
			// between: the whole event dispatch is synchronous and no layout pass runs inside it, so the jumped
			// destination is never seen. The owner keeps its own copy of the offset (the presenter pushes into
			// it on every change), and the chase starts from wherever the offset is once this returns.
			SetCurrentValue(OffsetProperty, origin);
			owner.AnimateTo(destination);
		}
	}
}
