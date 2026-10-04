using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace LLMDesktopAssistant.Controls.Behaviours
{
	/// <summary>
	/// Adds interpolated ("smooth") mouse wheel scrolling to a <see cref="ScrollViewer"/> and,
	/// optionally, makes it follow the growing end of its content.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Avalonia scrolls instantly on a wheel notch: <c>ScrollContentPresenter</c> handles
	/// <see cref="InputElement.PointerWheelChangedEvent"/> in the bubbling phase and jumps the offset.
	/// This behaviour intercepts the event in the tunneling phase (before the presenter) and instead
	/// drives <see cref="ScrollViewer.OffsetProperty"/> frame by frame.
	/// </para>
	/// <para>
	/// The model is "target + chase": every scroll request accumulates an intended <c>Target</c>, and the
	/// visible offset exponentially approaches that target (time to settle ≈ <see cref="DurationProperty"/>).
	/// This way any amount of scrolling is possible in a single gesture - fast spinning travels far
	/// (a far target moves faster), while a single notch stays gentle. Notches that arrive faster than
	/// the render loop cannot get lost, because they accumulate into the target instead of
	/// restarting/overwriting an animation.
	/// </para>
	/// <para>
	/// With <see cref="FollowEndEnabledProperty"/> the same chase is continuously retargeted to
	/// <see cref="ScrollViewer.ScrollBarMaximum"/>, so growing (streaming) content is followed smoothly
	/// instead of snapping on every new token. The follow is "sticky": it is engaged by <see cref="AnimateTo"/>
	/// requests that end at the bottom, and it is dropped only by an explicit user scroll
	/// (wheel notch, scroll bar, touch gesture). It is deliberately <b>not</b> inferred from offset deltas:
	/// layout can move the offset on its own (anchoring, coercion when a collapsible block collapses),
	/// which must never break the follow.
	/// </para>
	/// </remarks>
	public class SmoothScrollBehavior
	{
		public static readonly AttachedProperty<bool> SmoothScrollEnabledProperty =
			AvaloniaProperty.RegisterAttached<ScrollViewer, bool>(
				"SmoothScrollEnabled",
				typeof(SmoothScrollBehavior),
				false);

		/// <summary>
		/// When enabled, the ScrollViewer sticks to the end of its content: while the content grows
		/// (streaming output) the offset smoothly follows <see cref="ScrollViewer.ScrollBarMaximum"/>.
		/// The follow engages once the end is reached and stops as soon as the user scrolls away from it.
		/// </summary>
		public static readonly AttachedProperty<bool> FollowEndEnabledProperty =
			AvaloniaProperty.RegisterAttached<ScrollViewer, bool>(
				"FollowEndEnabled",
				typeof(SmoothScrollBehavior),
				false);

		/// <summary>
		/// The size of one wheel notch, in pixels. Defaults to the value Avalonia uses for instant
		/// wheel scrolling (<c>ScrollContentPresenter</c> advances the offset by 50px per notch).
		/// </summary>
		public static readonly AttachedProperty<double> StepProperty =
			AvaloniaProperty.RegisterAttached<ScrollViewer, double>(
				"Step",
				typeof(SmoothScrollBehavior),
				50d);

		/// <summary>Approximate time for the content to settle on the target.</summary>
		public static readonly AttachedProperty<TimeSpan> DurationProperty =
			AvaloniaProperty.RegisterAttached<ScrollViewer, TimeSpan>(
				"Duration",
				typeof(SmoothScrollBehavior),
				TimeSpan.FromMilliseconds(180));

		/// <summary>Upper bound of the scroll speed, in pixels per second. Zero or less means unlimited.</summary>
		public static readonly AttachedProperty<double> MaxSpeedProperty =
			AvaloniaProperty.RegisterAttached<ScrollViewer, double>(
				"MaxSpeed",
				typeof(SmoothScrollBehavior),
				10000d);

		/// <summary>ln(100), i.e. the time in "tau" units needed to cover ~99% of the remaining distance.</summary>
		private const double SettleFactor = 4.6d;

		private const double StopThreshold = 0.5d;

		/// <summary>How far the offset may drift on its own (coercion, rounding) before it counts as external.</summary>
		private const double ResyncThreshold = 1d;

		private const double EndTolerance = 1d;

		private static readonly Dictionary<ScrollViewer, ScrollState> s_states = new();

		public static void SetSmoothScrollEnabled(ScrollViewer element, bool value)
		{
			element.SetValue(SmoothScrollEnabledProperty, value);
		}

		public static bool GetSmoothScrollEnabled(ScrollViewer element)
		{
			return element.GetValue(SmoothScrollEnabledProperty);
		}

		public static void SetFollowEndEnabled(ScrollViewer element, bool value)
		{
			element.SetValue(FollowEndEnabledProperty, value);
		}

		public static bool GetFollowEndEnabled(ScrollViewer element)
		{
			return element.GetValue(FollowEndEnabledProperty);
		}

		public static void SetStep(ScrollViewer element, double value)
		{
			element.SetValue(StepProperty, value);
		}

		public static double GetStep(ScrollViewer element)
		{
			return element.GetValue(StepProperty);
		}

		public static void SetDuration(ScrollViewer element, TimeSpan value)
		{
			element.SetValue(DurationProperty, value);
		}

		public static TimeSpan GetDuration(ScrollViewer element)
		{
			return element.GetValue(DurationProperty);
		}

		public static void SetMaxSpeed(ScrollViewer element, double value)
		{
			element.SetValue(MaxSpeedProperty, value);
		}

		public static double GetMaxSpeed(ScrollViewer element)
		{
			return element.GetValue(MaxSpeedProperty);
		}

		/// <summary>
		/// Tells whether the end of the content is currently being followed
		/// (see <see cref="FollowEndEnabledProperty"/>).
		/// </summary>
		public static bool GetIsFollowingEnd(ScrollViewer element)
		{
			return s_states.TryGetValue(element, out var state) && state.Sticky;
		}

		static SmoothScrollBehavior()
		{
			SmoothScrollEnabledProperty.Changed.AddClassHandler<ScrollViewer, bool>(
				(sv, _) => UpdateSubscriptions(sv));

			FollowEndEnabledProperty.Changed.AddClassHandler<ScrollViewer, bool>(
				(sv, _) => UpdateSubscriptions(sv));
		}

		private static void UpdateSubscriptions(ScrollViewer scrollViewer)
		{
			var smoothScroll = GetSmoothScrollEnabled(scrollViewer);
			var followEnd = GetFollowEndEnabled(scrollViewer);

			// Tunneling is required: ScrollContentPresenter handles the wheel while bubbling,
			// so a bubbling handler here would never be reached.
			if (smoothScroll)
				scrollViewer.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
			else
				scrollViewer.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);

			if (followEnd)
			{
				GetState(scrollViewer);

				scrollViewer.ScrollChanged += OnScrollChanged;
				scrollViewer.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);

				// Touch/pen dragging: the presenter marks the event as handled, so listen anyway.
				scrollViewer.AddHandler(InputElement.ScrollGestureEvent, OnScrollGesture,
					RoutingStrategies.Bubble, handledEventsToo: true);
			}
			else
			{
				scrollViewer.ScrollChanged -= OnScrollChanged;
				scrollViewer.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
				scrollViewer.RemoveHandler(InputElement.ScrollGestureEvent, OnScrollGesture);

				if (s_states.TryGetValue(scrollViewer, out var state))
					state.Sticky = false;
			}

			// Never subscribe twice, but keep tracking while either feature is on.
			scrollViewer.DetachedFromVisualTree -= OnDetachedFromVisualTree;

			if (smoothScroll || followEnd)
				scrollViewer.DetachedFromVisualTree += OnDetachedFromVisualTree;
			else
				RemoveState(scrollViewer);
		}

		private static void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
		{
			if (sender is not ScrollViewer scrollViewer)
				return;

			scrollViewer.ScrollChanged -= OnScrollChanged;
			scrollViewer.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
			scrollViewer.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
			scrollViewer.RemoveHandler(InputElement.ScrollGestureEvent, OnScrollGesture);

			RemoveState(scrollViewer);
		}

		private static void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
		{
			if (sender is not ScrollViewer scrollViewer || scrollViewer.ScrollBarMaximum.Y <= 0)
				return;

			// A nested, independently scrollable ScrollViewer under the pointer must win.
			if (FindInnerScrollable(e.Source, scrollViewer) != null)
				return;

			var state = GetState(scrollViewer);

			// Accumulate on top of the pending target, not on top of the already moved offset:
			// several wheel notches may arrive before the next rendered frame.
			var origin = state.HasTarget ? state.Target : scrollViewer.Offset.Y;

			var target = Math.Clamp(
				origin - (e.Delta.Y * GetStep(scrollViewer)),
				0d,
				scrollViewer.ScrollBarMaximum.Y);

			// Nothing left in this direction (we are already at the limit): leave the event unhandled
			// so the wheel chains to an outer ScrollViewer, like Avalonia does on its own.
			if (Math.Abs(target - origin) < 0.001d)
				return;

			// Scrolling to the end re-engages the follow, scrolling away from it disengages.
			SetSticky(state, target >= scrollViewer.ScrollBarMaximum.Y - EndTolerance);

			e.Handled = true;
			Pursue(scrollViewer, state, target);
		}

		private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
		{
			if (sender is not ScrollViewer scrollViewer || !s_states.TryGetValue(scrollViewer, out var state))
				return;

			// Taking hold of a scroll bar is a manual scroll - stop following the end.
			if (e.Source is Visual visual &&
				visual.FindAncestorOfType<ScrollBar>(includeSelf: true) != null)
				SetSticky(state, false);
		}

		private static void OnScrollGesture(object? sender, ScrollGestureEventArgs e)
		{
			// Touch/pen dragging is a manual scroll - stop following the end.
			if (sender is ScrollViewer scrollViewer && s_states.TryGetValue(scrollViewer, out var state))
				SetSticky(state, false);
		}

		private static void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
		{
			if (sender is not ScrollViewer scrollViewer || !s_states.TryGetValue(scrollViewer, out var state))
				return;

			var maximum = scrollViewer.ScrollBarMaximum.Y;

			if (scrollViewer.Offset.Y >= maximum - EndTolerance)
			{
				// Re-engage only when the end is actually (re)reached - not right after a manual detach,
				// while the offset is still sitting at the end waiting for the first chase frame.
				if (state.LeftEnd)
					SetSticky(state, true);
			}
			else
			{
				state.LeftEnd = true;
			}

			if (state.Sticky)
				Pursue(scrollViewer, state, maximum);
		}

		/// <summary>
		/// Starts (or retargets) a smooth scroll to <paramref name="targetY"/>, clamped to the scrollable range.
		/// A smooth scroll already in progress is not restarted - it just gets a new target.
		/// A target at the end of the content engages the follow (see <see cref="FollowEndEnabledProperty"/>).
		/// </summary>
		public static void AnimateTo(ScrollViewer scrollViewer, double targetY)
		{
			var state = GetState(scrollViewer);
			var maximum = scrollViewer.ScrollBarMaximum.Y;
			var target = Math.Clamp(targetY, 0d, Math.Max(0d, maximum));

			if (target >= maximum - EndTolerance)
				SetSticky(state, true);

			Pursue(scrollViewer, state, target);
		}

		/// <summary>
		/// Stops a smooth scroll in progress on the given <see cref="ScrollViewer"/>,
		/// leaving the offset where it currently is.
		/// </summary>
		public static void CancelAnimation(ScrollViewer scrollViewer)
		{
			if (s_states.TryGetValue(scrollViewer, out var state) && state.IsRunning)
			{
				state.IsRunning = false;
				state.HasTarget = false;
			}
		}

		private static void Pursue(ScrollViewer scrollViewer, ScrollState state, double target)
		{
			state.Target = target;
			state.HasTarget = true;
			StartChase(scrollViewer, state);
		}

		private static void SetSticky(ScrollState state, bool sticky)
		{
			state.Sticky = sticky;

			if (sticky)
				state.LeftEnd = false;
		}

		private static void StartChase(ScrollViewer scrollViewer, ScrollState state)
		{
			if (state.IsRunning)
				return;

			var topLevel = TopLevel.GetTopLevel(scrollViewer);

			if (topLevel == null)
			{
				SetOffsetY(scrollViewer, state.Target);
				state.HasTarget = false;
				return;
			}

			var settleMs = Math.Max(1d, GetDuration(scrollViewer).TotalMilliseconds);
			var tauSeconds = settleMs / SettleFactor / 1000d;

			state.IsRunning = true;
			state.LastWritten = scrollViewer.Offset.Y;

			var stopwatch = Stopwatch.StartNew();
			var lastFrame = TimeSpan.Zero;

			void Frame(TimeSpan _)
			{
				if (!state.IsRunning)
					return;

				var now = stopwatch.Elapsed;
				var deltaSeconds = (now - lastFrame).TotalSeconds;
				lastFrame = now;

				if (deltaSeconds <= 0d)
					deltaSeconds = 1d / 60d;

				var position = scrollViewer.Offset.Y;

				// Somebody else moved the offset (scrollbar drag, programmatic scroll, layout anchoring) -
				// drop the stale target instead of fighting them. The sticky flag is untouched on purpose:
				// layout may shift the offset while following a collapsing block.
				if (Math.Abs(position - state.LastWritten) > ResyncThreshold)
				{
					state.HasTarget = false;
					Finish(scrollViewer, state);
					return;
				}

				var remaining = state.Target - position;

				if (!state.HasTarget || Math.Abs(remaining) <= StopThreshold)
				{
					SetOffsetY(scrollViewer, state.Target);
					Finish(scrollViewer, state);
					return;
				}

				var step = remaining * (1d - Math.Exp(-deltaSeconds / tauSeconds));
				var maxSpeed = GetMaxSpeed(scrollViewer);

				if (maxSpeed > 0d)
				{
					var maxStep = maxSpeed * deltaSeconds;

					if (Math.Abs(step) > maxStep)
						step = Math.Sign(step) * maxStep;
				}

				position += step;
				state.LastWritten = position;
				SetOffsetY(scrollViewer, position);

				topLevel.RequestAnimationFrame(Frame);
			}

			topLevel.RequestAnimationFrame(Frame);
		}

		private static void Finish(ScrollViewer scrollViewer, ScrollState state)
		{
			state.IsRunning = false;
			state.HasTarget = false;
			state.LastWritten = scrollViewer.Offset.Y;
		}

		private static void RemoveState(ScrollViewer scrollViewer)
		{
			if (s_states.Remove(scrollViewer, out var state))
			{
				state.IsRunning = false;
				state.HasTarget = false;
				state.Sticky = false;
			}
		}

		private static ScrollState GetState(ScrollViewer scrollViewer)
		{
			if (!s_states.TryGetValue(scrollViewer, out var state))
			{
				state = new ScrollState();
				s_states[scrollViewer] = state;
			}

			return state;
		}

		private static void SetOffsetY(ScrollViewer scrollViewer, double y)
		{
			scrollViewer.SetCurrentValue(
				ScrollViewer.OffsetProperty,
				new Vector(scrollViewer.Offset.X, y));
		}

		private static ScrollViewer? FindInnerScrollable(object? source, ScrollViewer outer)
		{
			if (source is not Visual visual)
				return null;

			for (Visual? current = visual; current != null && current != outer; current = current.GetVisualParent())
			{
				if (current is ScrollViewer inner && inner.ScrollBarMaximum.Y > 0)
					return inner;
			}

			return null;
		}

		private sealed class ScrollState
		{
			public bool IsRunning;

			public bool HasTarget;

			/// <summary>The end of the content is being followed (sticky bottom).</summary>
			public bool Sticky;

			/// <summary>
			/// The offset has been away from the end since the follow was last engaged (starts as true,
			/// so that an already-bottomed content engages the follow right away).
			/// This is what allows the user to detach: while a manual detach is still pending the first
			/// chase frame, the offset is technically still "at the end" and must not re-engage.
			/// </summary>
			public bool LeftEnd = true;

			public double Target;

			public double LastWritten;
		}
	}
}
