using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace LLMDesktopAssistant.Controls.Behaviours
{
	/// <summary>
	/// Adds interpolated ("smooth") mouse wheel scrolling to a <see cref="ScrollViewer"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Avalonia scrolls instantly on a wheel notch: <c>ScrollContentPresenter</c> handles
	/// <see cref="InputElement.PointerWheelChangedEvent"/> in the bubbling phase and jumps the offset.
	/// This behaviour intercepts the event in the tunneling phase (before the presenter) and instead
	/// drives <see cref="ScrollViewer.OffsetProperty"/> frame by frame.
	/// </para>
	/// <para>
	/// The model is "target + chase": every wheel notch accumulates an intended <c>Target</c>, and the
	/// visible offset exponentially approaches that target (time to settle ≈ <see cref="DurationProperty"/>).
	/// This way any amount of scrolling is possible in a single gesture - fast spinning travels far
	/// (a far target moves faster), while a single notch stays gentle. Notches that arrive faster than
	/// the render loop cannot get lost, because they accumulate into the target instead of
	/// restarting/overwriting an animation.
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

		private const double ResyncThreshold = 1d;

		private static readonly Dictionary<ScrollViewer, ScrollState> s_states = new();

		public static void SetSmoothScrollEnabled(ScrollViewer element, bool value)
		{
			element.SetValue(SmoothScrollEnabledProperty, value);
		}

		public static bool GetSmoothScrollEnabled(ScrollViewer element)
		{
			return element.GetValue(SmoothScrollEnabledProperty);
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

		static SmoothScrollBehavior()
		{
			SmoothScrollEnabledProperty.Changed.AddClassHandler<ScrollViewer, bool>(
				(sv, e) => OnSmoothScrollEnabledChanged(sv, e.GetNewValue<bool>()));
		}

		private static void OnSmoothScrollEnabledChanged(ScrollViewer scrollViewer, bool enabled)
		{
			// Tunneling is required: ScrollContentPresenter handles the wheel while bubbling,
			// so a bubbling handler here would never be reached.
			if (enabled)
			{
				scrollViewer.AddHandler(
					InputElement.PointerWheelChangedEvent,
					OnPointerWheelChanged,
					RoutingStrategies.Tunnel);
				scrollViewer.DetachedFromVisualTree += OnDetachedFromVisualTree;
			}
			else
			{
				scrollViewer.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
				scrollViewer.DetachedFromVisualTree -= OnDetachedFromVisualTree;
				RemoveState(scrollViewer);
			}
		}

		private static void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
		{
			if (sender is ScrollViewer scrollViewer)
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

			state.Target = Math.Clamp(
				origin - (e.Delta.Y * GetStep(scrollViewer)),
				0d,
				scrollViewer.ScrollBarMaximum.Y);
			state.HasTarget = true;

			e.Handled = true;
			StartChase(scrollViewer, state);
		}

		/// <summary>
		/// Starts (or retargets) a smooth scroll to <paramref name="targetY"/>, clamped to the scrollable range.
		/// A smooth scroll already in progress is not restarted - it just gets a new target.
		/// </summary>
		public static void AnimateTo(ScrollViewer scrollViewer, double targetY)
		{
			var state = GetState(scrollViewer);

			state.Target = Math.Clamp(targetY, 0d, Math.Max(0d, scrollViewer.ScrollBarMaximum.Y));
			state.HasTarget = true;

			StartChase(scrollViewer, state);
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

				// Somebody else moved the offset (auto-scroll pin, scrollbar drag, layout coercion) -
				// drop the stale target instead of fighting them.
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

			public double Target;

			public double LastWritten;
		}
	}
}
