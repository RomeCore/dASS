using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace LLMDesktopAssistant.Controls.Scrolling
{
	/// <summary>
	/// A <see cref="ScrollViewer"/> whose wheel scrolling is interpolated ("smooth") instead of instant,
	/// and which can follow the growing end of its content.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The model is "target + chase": a wheel notch only moves an intended <c>Target</c>, and the visible
	/// offset exponentially approaches it (time to settle ≈ <see cref="Duration"/>). Any number of notches
	/// may arrive in one frame - they accumulate into the target instead of restarting an animation, so
	/// fast spinning travels far while a single notch stays gentle.
	/// </para>
	/// <para>
	/// The wheel itself is taken over by <see cref="SmoothScrollContentPresenter"/> (this control needs it in
	/// its template, see <c>SmoothScrollViewerTheme.axaml</c>). Intercepting there, in the bubbling phase,
	/// is what lets nested consumers win: a terminal, a combo box or a nested <see cref="ScrollViewer"/>
	/// below the pointer handles the notch first, and only when nobody does - the nested one is already at
	/// its limit and chains the event outward - does the notch reach this viewer and get animated.
	/// </para>
	/// <para>
	/// With <see cref="FollowEnd"/> the chase is continuously retargeted to the end of the content, so
	/// growing (streaming) content is followed instead of snapping on every new token. Following is
	/// engaged and dropped by explicit intent only: a wheel notch landing on the end, the scroll bar or a
	/// touch gesture ending on the end, or <see cref="AnimateToEnd"/>. It is never inferred from "the offset
	/// happens to be at the bottom", which is what makes grabbing the scroll bar together with a running
	/// auto-scroll work.
	/// </para>
	/// </remarks>
	public class SmoothScrollViewer : ScrollViewer
	{
		/// <summary>
		/// The size of one wheel notch, in pixels. Defaults to the value stock Avalonia scrolling uses.
		/// </summary>
		public static readonly StyledProperty<double> StepProperty =
			AvaloniaProperty.Register<SmoothScrollViewer, double>(nameof(Step), 50d);

		/// <summary>Approximate time for the content to settle on the target.</summary>
		public static readonly StyledProperty<TimeSpan> DurationProperty =
			AvaloniaProperty.Register<SmoothScrollViewer, TimeSpan>(nameof(Duration), TimeSpan.FromMilliseconds(180));

		/// <summary>Upper bound of the scroll speed, in pixels per second. Zero or less means unlimited.</summary>
		public static readonly StyledProperty<double> MaxSpeedProperty =
			AvaloniaProperty.Register<SmoothScrollViewer, double>(nameof(MaxSpeed), 10000d);

		/// <summary>
		/// When enabled, the viewer sticks to the end of its content: as the content grows the target is
		/// continuously pulled to the bottom, so streaming output is followed instead of jumping.
		/// The follow is dropped as soon as the user scrolls away from the end.
		/// </summary>
		public static readonly StyledProperty<bool> FollowEndProperty =
			AvaloniaProperty.Register<SmoothScrollViewer, bool>(nameof(FollowEnd));

		/// <summary>Whether the end of the content is currently being followed (see <see cref="FollowEnd"/>).</summary>
		private static readonly DirectProperty<SmoothScrollViewer, bool> IsFollowingEndProperty =
			AvaloniaProperty.RegisterDirect<SmoothScrollViewer, bool>(
				nameof(IsFollowingEnd),
				o => o.IsFollowingEnd);

		/// <summary>ln(100), i.e. the time in "tau" units needed to cover ~99% of the remaining distance.</summary>
		private const double SettleFactor = 4.6d;

		/// <summary>Closer than this to the target counts as arrived (and snaps to it).</summary>
		private const double StopThreshold = 0.25d;

		/// <summary>How close to the end of the content counts as "at the end".</summary>
		private const double EndTolerance = 1d;

		/// <summary>How far the offset may differ from what we wrote before it counts as a foreign change.</summary>
		private const double OwnWriteTolerance = 0.5d;

		private Vector _target;
		private Vector _lastWritten;
		private bool _hasTarget;
		private bool _isAnimating;
		private bool _isFollowingEnd;

		/// <summary>A scroll bar is being dragged or a touch gesture is in progress - the user drives.</summary>
		private bool _manualScroll;

		/// <summary>
		/// Identifies the one animation loop that is allowed to exist: a frame callback from an older loop
		/// sees a stale generation and dies, so two loops can never fight over the same target.
		/// </summary>
		private int _generation;

		public SmoothScrollViewer()
		{
			ScrollChanged += OnScrollChanged;

			AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
			AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
			AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);

			// Touch/pen dragging: the presenter scrolls the offset itself and marks the event as handled.
			AddHandler(ScrollGestureEvent, OnScrollGesture, RoutingStrategies.Bubble, handledEventsToo: true);
			AddHandler(ScrollGestureEndedEvent, OnScrollGestureEnded, RoutingStrategies.Bubble, handledEventsToo: true);
		}

		public double Step
		{
			get => GetValue(StepProperty);
			set => SetValue(StepProperty, value);
		}

		public TimeSpan Duration
		{
			get => GetValue(DurationProperty);
			set => SetValue(DurationProperty, value);
		}

		public double MaxSpeed
		{
			get => GetValue(MaxSpeedProperty);
			set => SetValue(MaxSpeedProperty, value);
		}

		public bool FollowEnd
		{
			get => GetValue(FollowEndProperty);
			set => SetValue(FollowEndProperty, value);
		}

		public bool IsFollowingEnd => _isFollowingEnd;

		/// <summary>
		/// Starts (or retargets) a smooth scroll to <paramref name="targetY"/>, clamped to the scrollable
		/// range. A scroll already in progress is not restarted - it just gets a new target.
		/// </summary>
		public void AnimateTo(double targetY)
		{
			var maximum = MaximumY;
			var target = new Vector(Offset.X, Math.Clamp(targetY, 0d, maximum));

			SetFollowingEnd(target.Y >= maximum - EndTolerance);
			Pursue(target);
		}

		/// <summary>
		/// Scrolls smoothly to the end of the content and, when <see cref="FollowEnd"/> is set,
		/// sticks to it.
		/// </summary>
		/// <remarks>Named to not hide the instant <see cref="ScrollViewer.ScrollToEnd"/>.</remarks>
		public void AnimateToEnd()
		{
			SetFollowingEnd(true);
			Pursue(new Vector(Offset.X, MaximumY));
		}

		/// <summary>
		/// Stops a smooth scroll in progress, leaving the offset where it currently is,
		/// and stops following the end of the content.
		/// </summary>
		public void CancelAnimation()
		{
			_hasTarget = false;
			SetFollowingEnd(false);
		}

		/// <summary>
		/// Applies one wheel notch: the target moves by <paramref name="delta"/> notches, the offset follows.
		/// Returns <c>false</c> when the scroll is already at its limit in the requested direction, so the
		/// caller can leave the event unhandled and let it chain to an outer scroll viewer.
		/// </summary>
		internal bool HandlePointerWheel(Vector delta)
		{
			var maximum = ScrollBarMaximum;

			if (maximum.X <= 0d && maximum.Y <= 0d)
				return false;

			// Accumulate on top of the pending target, not on top of the already moved offset:
			// several notches may arrive before the next rendered frame.
			var origin = _hasTarget ? _target : Offset;

			var target = new Vector(
				Clamp(origin.X - (delta.X * Step), maximum.X),
				Clamp(origin.Y - (delta.Y * Step), maximum.Y));

			if (AreSame(target, origin))
				return false;

			// A notch is an explicit manual scroll: it ends a scroll bar drag mode, and it keeps following
			// the end only when the notch actually lands on it (a notch up always drops the follow).
			_manualScroll = false;
			SetFollowingEnd(target.Y >= MaximumY - EndTolerance);
			Pursue(target);

			return true;
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);

			if (change.Property != FollowEndProperty)
				return;

			if (!FollowEnd)
			{
				SetFollowingEnd(false);
			}
			else if (IsAtEnd)
			{
				// Turning the follow on while already at the end should take effect immediately.
				SetFollowingEnd(true);
				PursueEnd();
			}
		}

		private double MaximumY => Math.Max(0d, ScrollBarMaximum.Y);

		private bool IsAtEnd => Offset.Y >= MaximumY - EndTolerance;

		private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
		{
			if (_manualScroll)
			{
				// The user is driving (scroll bar drag, touch gesture): the follow is decided by where they
				// put the content, never fought by a chase.
				SetFollowingEnd(IsAtEnd);
				return;
			}

			// A layout change (streamed content grew, viewport resized, the offset got coerced) is what the
			// follow exists for; a pure offset change is somebody scrolling.
			var layoutChanged = !AreSame(e.ExtentDelta, default) || !AreSame(e.ViewportDelta, default);

			if (_isFollowingEnd)
			{
				// A pure offset change that we did not write means somebody else scrolled the content away from
				// the end (keyboard, application code): stop following instead of fighting them.
				if (!IsAtEnd && !layoutChanged && !AreSame(Offset, _lastWritten))
				{
					SetFollowingEnd(false);
					return;
				}

				// The content grew, the viewport changed, or our own chase moved the offset: stay glued to the end.
				PursueEnd();
				return;
			}

			// Being at the end because of a layout change engages the follow: an empty viewer that then receives
			// streamed content, or content that shrank/clamped under the offset. A *scrolled* end is handled by
			// the wheel, the scroll bar and the touch gesture themselves.
			if (layoutChanged && IsAtEnd)
				SetFollowingEnd(true);
		}

		private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
		{
			// Taking hold of a scroll bar is a manual scroll: the chase stops, and the follow will be decided
			// by where the drag ends (dragging down to the end re-engages it).
			if (e.Source is not Visual visual || visual.FindAncestorOfType<ScrollBar>(includeSelf: true) == null)
				return;

			_manualScroll = true;
			_hasTarget = false;
			SetFollowingEnd(false);
		}

		private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (_manualScroll)
				EndManualScroll();
		}

		private void OnPointerMoved(object? sender, PointerEventArgs e)
		{
			// The release can happen outside the window (or on another control): noticing that the button is
			// no longer held is what ends a scroll bar drag then.
			if (_manualScroll && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
				EndManualScroll();
		}

		private void OnScrollGesture(object? sender, ScrollGestureEventArgs e)
		{
			_manualScroll = true;
			_hasTarget = false;
			SetFollowingEnd(IsAtEnd);
		}

		private void OnScrollGestureEnded(object? sender, ScrollGestureEndedEventArgs e)
		{
			EndManualScroll();
		}

		private void EndManualScroll()
		{
			_manualScroll = false;
			SetFollowingEnd(IsAtEnd);

			if (_isFollowingEnd)
				PursueEnd();
		}

		private void SetFollowingEnd(bool value)
		{
			// Following is meaningless when it is not enabled for this viewer.
			value &= FollowEnd;
			SetAndRaise(IsFollowingEndProperty, ref _isFollowingEnd, value);
		}

		private void PursueEnd()
		{
			Pursue(new Vector((_hasTarget ? _target : Offset).X, MaximumY));
		}

		private void Pursue(Vector target)
		{
			_target = ClampToRange(target);
			_hasTarget = true;

			if ((_target - Offset).Length <= StopThreshold)
			{
				SetOffset(_target);
				Finish();
				return;
			}

			StartChase();
		}

		private void StartChase()
		{
			if (_isAnimating)
				return;

			var topLevel = TopLevel.GetTopLevel(this);

			if (topLevel == null)
			{
				// There is no frame loop to interpolate on (not rendered yet): land on the target directly.
				SetOffset(_target);
				Finish();
				return;
			}

			_isAnimating = true;
			var generation = ++_generation;
			_lastWritten = Offset;

			var stopwatch = Stopwatch.StartNew();
			var previous = TimeSpan.Zero;

			void Frame(TimeSpan _)
			{
				// A newer chase (or a cancel) supersedes this frame: only one loop may ever run.
				if (!_isAnimating || generation != _generation)
					return;

				var now = stopwatch.Elapsed;
				var elapsedSeconds = (now - previous).TotalSeconds;
				previous = now;

				if (elapsedSeconds <= 0d)
					elapsedSeconds = 1d / 60d;

				if (Advance(elapsedSeconds))
					topLevel.RequestAnimationFrame(Frame);
			}

			topLevel.RequestAnimationFrame(Frame);
		}

		/// <summary>Moves the offset one frame closer to the target; returns <c>false</c> when the chase is over.</summary>
		private bool Advance(double elapsedSeconds)
		{
			var position = Offset;

			if (!AreSame(position, _lastWritten))
			{
				// Somebody else moved the offset: layout coercion (the extent or viewport changed),
				// a scroll bar drag, or application code scrolling on purpose.
				_lastWritten = position;

				// While following the end the target is the end, whatever moved the offset - keep pulling.
				// Otherwise the chase gives way to whoever took over.
				if (!_isFollowingEnd)
					_hasTarget = false;
			}

			if (_isFollowingEnd)
			{
				// Growing content keeps moving the end - the target keeps chasing it.
				_target = ClampToRange(new Vector(_target.X, MaximumY));
				_hasTarget = true;
			}
			else
			{
				_target = ClampToRange(_target);
			}

			if (!_hasTarget)
			{
				Finish();
				return false;
			}

			var remaining = _target - position;

			if (remaining.Length <= StopThreshold)
			{
				SetOffset(_target);
				Finish();
				return false;
			}

			var settleSeconds = Math.Max(1d, Duration.TotalMilliseconds) / SettleFactor / 1000d;
			var step = remaining * (1d - Math.Exp(-elapsedSeconds / settleSeconds));
			var maxSpeed = MaxSpeed;

			if (maxSpeed > 0d)
			{
				var maxStep = maxSpeed * elapsedSeconds;

				if (step.Length > maxStep)
					step = step * (maxStep / step.Length);
			}

			SetOffset(position + step);

			return true;
		}

		private void Finish()
		{
			_isAnimating = false;
			_hasTarget = false;
		}

		private void SetOffset(Vector offset)
		{
			SetCurrentValue(OffsetProperty, offset);

			// The viewer may coerce the value (the extent has not been measured yet, the content shrank).
			// Remember what it actually accepted, so the coercion is not mistaken for a foreign scroll.
			_lastWritten = Offset;
		}

		private Vector ClampToRange(Vector value)
		{
			var maximum = ScrollBarMaximum;
			return new Vector(Clamp(value.X, maximum.X), Clamp(value.Y, maximum.Y));
		}

		private static double Clamp(double value, double maximum)
		{
			if (double.IsNaN(value))
				return 0d;

			maximum = Math.Max(0d, maximum);
			return value < 0d ? 0d : value > maximum ? maximum : value;
		}

		private static bool AreSame(Vector left, Vector right)
		{
			return (left - right).Length <= OwnWriteTolerance;
		}
	}
}
