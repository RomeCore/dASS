using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Desktop.Execution.Terminals;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Desktop.ToolModules.Terminal
{
	/// <summary>
	/// View for displaying a terminal emulator inside a chat message.
	/// Renders the <see cref="ProcessTerminalSession"/> created by the launcher:
	/// the view only displays the terminal buffer and forwards user input to the PTY.
	/// </summary>
	public partial class TerminalAdditionalView : UserControl
	{
		private TerminalAdditionalViewModel? _viewModel;
		private bool _isAttached;

		// True while the bar is being written from the terminal. Without it, a programmatic write is
		// indistinguishable from a user drag.
		private bool _isSyncingScrollBar;

		private bool _isResizing;
		private double _resizeStartY;
		private double _resizeStartHeight;

		public TerminalAdditionalView()
		{
			InitializeComponent();

			Loaded += OnLoaded;
			Unloaded += OnUnloaded;
			DataContextChanged += OnDataContextChanged;

			// The scrollback bar mirrors the terminal's viewport and drives it back when dragged.
			TerminalHost.PropertyChanged += OnTerminalViewPropertyChanged;
			TerminalScrollBar.PropertyChanged += OnScrollBarPropertyChanged;

			// The focus ring means "typing goes here", so only the terminal raises it: a focused bar
			// button is a different thing and must not claim the card.
			TerminalHost.GotFocus += (_, _) => Card.Classes.Set("focused", true);
			TerminalHost.LostFocus += (_, _) => Card.Classes.Set("focused", false);

			ResizeGrip.PointerPressed += OnResizeGripPressed;
			ResizeGrip.PointerMoved += OnResizeGripMoved;
			ResizeGrip.PointerReleased += OnResizeGripReleased;
			ResizeGrip.PointerCaptureLost += OnResizeGripCaptureLost;
		}

		private void OnDataContextChanged(object? sender, EventArgs e)
		{
			_viewModel = DataContext as TerminalAdditionalViewModel;

			if (IsLoaded)
				AttachSession();
		}

		private void OnLoaded(object? sender, RoutedEventArgs e)
		{
			AttachSession();
		}

		private void OnUnloaded(object? sender, RoutedEventArgs e)
		{
			if (_viewModel?.Descriptor?.TerminalSession is { } session)
				session.OutputUpdated -= OnSessionOutputUpdated;

			_isAttached = false;
		}

		/// <summary>
		/// Binds this view to the launcher's session. The session outlives the view (it belongs to the
		/// descriptor), so unloading only drops the paint subscription and never the buffer.
		/// </summary>
		private void AttachSession()
		{
			if (_viewModel == null || _isAttached || TerminalHost == null)
				return;

			var session = _viewModel.Descriptor?.TerminalSession;
			if (session == null)
			{
				_viewModel.Fail(Locale.Get("message.terminal.error.no_session"));
				return;
			}

			_isAttached = true;

			// Attach the session to the view. The launcher reads the PTY and writes to the
			// terminal; the view renders the buffer and forwards input back to the PTY.
			TerminalHost.Terminal = session.Terminal;
			TerminalHost.PtyConnection = session.Pty;

			// The launcher raises OutputUpdated when new data is written to the terminal
			// (XTerm.Terminal.BufferChanged only fires on buffer switches), so repaint on it.
			session.OutputUpdated += OnSessionOutputUpdated;

			UpdateScrollBar();
		}

		private void OnSessionOutputUpdated(object? sender, EventArgs e)
		{
			UIDispatcher.Post(() =>
			{
				TerminalHost?.InvalidateVisual();
				UpdateScrollBar();
			});
		}

		private void OnTerminalViewPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == TerminalView.MaxScrollbackProperty ||
				e.Property == TerminalView.ViewportLinesProperty ||
				e.Property == TerminalView.ViewportYProperty ||
				e.Property == TerminalView.IsAlternateBufferProperty)
			{
				UpdateScrollBar();
			}
		}

		private void OnScrollBarPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			// Programmatic updates of the bar must not bounce back into the terminal.
			if (e.Property != RangeBase.ValueProperty || !_isAttached || _isSyncingScrollBar)
				return;

			TerminalHost.ViewportY = (int)Math.Round(TerminalScrollBar.Value);

			// The terminal owns the true maximum and may clamp the request. Show where the viewport
			// actually went instead of leaving the bar at a position the buffer never took.
			if (TerminalScrollBar.Value != TerminalHost.ViewportY)
				UpdateScrollBar();
		}

		/// <summary>
		/// Grabs the pointer for a height drag on the grip. Only the grip does this: the terminal needs
		/// the pointer for selection and mouse reporting.
		/// </summary>
		private void OnResizeGripPressed(object? sender, PointerPressedEventArgs e)
		{
			if (_viewModel == null || _viewModel.IsCollapsed || double.IsNaN(_viewModel.CardHeight))
				return;

			if (!e.GetCurrentPoint(ResizeGrip).Properties.IsLeftButtonPressed)
				return;

			_isResizing = true;
			_resizeStartY = e.GetPosition(this).Y;
			_resizeStartHeight = _viewModel.CardHeight;

			e.Pointer.Capture(ResizeGrip);
			e.Handled = true;
		}

		private void OnResizeGripMoved(object? sender, PointerEventArgs e)
		{
			if (!_isResizing || _viewModel == null)
				return;

			_viewModel.CardHeight = _resizeStartHeight + e.GetPosition(this).Y - _resizeStartY;
			e.Handled = true;
		}

		private void OnResizeGripReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (!_isResizing)
				return;

			EndResize();
			e.Pointer.Capture(null);
			e.Handled = true;
		}

		private void OnResizeGripCaptureLost(object? sender, PointerCaptureLostEventArgs e)
		{
			EndResize();
		}

		private void EndResize()
		{
			_isResizing = false;
		}

		/// <summary>
		/// Sizes the scrollback bar from the terminal buffer: the range is the scrollable
		/// scrollback, the thumb covers the visible part of it.
		/// </summary>
		private void UpdateScrollBar()
		{
			if (TerminalHost == null || TerminalScrollBar == null)
				return;

			// Everything below is the bar mirroring the terminal, and none of it is a user action. Avalonia
			// coerces Value down whenever Maximum shrinks - which happens on every layout pass that gives the
			// terminal more rows - and that coercion used to be read as a drag and written straight back into
			// ViewportY. That unpinned the viewport from the bottom: the launcher's auto-follow (it compares
			// the viewport against the end of the buffer) stopped matching, and the thumb settled short of the
			// real end because the bar's own maximum was still the honest one.
			_isSyncingScrollBar = true;
			try
			{
				// The alternate buffer has no scrollback of its own.
				if (TerminalHost.IsAlternateBuffer)
				{
					TerminalScrollBar.IsVisible = false;
					TerminalScrollBar.Value = 0;
					return;
				}

				var viewportLines = TerminalHost.ViewportLines;
				var maximum = TerminalHost.MaxScrollback;

				TerminalScrollBar.Minimum = 0;
				TerminalScrollBar.Maximum = maximum;
				TerminalScrollBar.ViewportSize = viewportLines;
				TerminalScrollBar.SmallChange = 1;
				TerminalScrollBar.LargeChange = Math.Max(1, viewportLines);
				TerminalScrollBar.Value = Math.Clamp(TerminalHost.ViewportY, 0, maximum);
				TerminalScrollBar.IsVisible = maximum > 0;
			}
			finally
			{
				_isSyncingScrollBar = false;
			}
		}
	}
}
