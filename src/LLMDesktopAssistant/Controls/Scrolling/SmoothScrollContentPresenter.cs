using Avalonia;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace LLMDesktopAssistant.Controls.Scrolling
{
	/// <summary>
	/// The content presenter <see cref="SmoothScrollViewer"/> puts into its template.
	/// </summary>
	/// <remarks>
	/// <para>
	/// It exists for exactly one reason: the mouse wheel. The stock
	/// <see cref="ScrollContentPresenter.OnPointerWheelChanged"/> jumps the offset to its new value the
	/// instant a notch arrives; this presenter only retargets its owner's chase
	/// (<see cref="SmoothScrollViewer.HandlePointerWheel"/>), so the visible offset glides.
	/// </para>
	/// <para>
	/// Taking the wheel over here - instead of in a tunnel handler on the scroll viewer - is what makes
	/// nested scrolling work. <see cref="InputElement.OnPointerWheelChanged"/> runs in the bubbling phase
	/// and only while nothing deeper has handled the event yet, so a terminal, a combo box or a nested
	/// <see cref="Avalonia.Controls.ScrollViewer"/> under the pointer gets the notch first. If that nested
	/// viewer is already at its limit it leaves the event unhandled, the wheel chains outward, reaches this
	/// presenter and is animated smoothly by the outer viewer.
	/// </para>
	/// </remarks>
	public class SmoothScrollContentPresenter : ScrollContentPresenter
	{
		private SmoothScrollViewer? _owner;

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
	}
}
