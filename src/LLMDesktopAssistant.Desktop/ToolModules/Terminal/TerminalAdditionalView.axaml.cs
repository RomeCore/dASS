using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Desktop.Execution.Terminals;

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
		private bool _isRunning;

		public TerminalAdditionalView()
		{
			InitializeComponent();

			Loaded += OnLoaded;
			Unloaded += OnUnloaded;
			DataContextChanged += OnDataContextChanged;

			// The scrollback bar mirrors the terminal's viewport and drives it back when dragged.
			TerminalHost.PropertyChanged += OnTerminalViewPropertyChanged;
			TerminalScrollBar.PropertyChanged += OnScrollBarPropertyChanged;
		}

		private void OnDataContextChanged(object? sender, EventArgs e)
		{
			_viewModel = DataContext as TerminalAdditionalViewModel;
		}

		private void OnLoaded(object? sender, RoutedEventArgs e)
		{
			if (_viewModel == null || TerminalHost == null || _isRunning)
				return;

			var session = _viewModel.Descriptor?.TerminalSession;
			if (session == null)
			{
				_viewModel.Fail("No terminal session available.");
				return;
			}

			_isRunning = true;

			// Attach the session to the view. The launcher reads the PTY and writes to the
			// terminal; the view renders the buffer and forwards input back to the PTY.
			TerminalHost.Terminal = session.Terminal;
			TerminalHost.PtyConnection = session.Pty;

			// The launcher raises OutputUpdated when new data is written to the terminal
			// (XTerm.Terminal.BufferChanged only fires on buffer switches), so repaint on it.
			session.OutputUpdated += OnSessionOutputUpdated;

			UpdateScrollBar();
		}

		private void OnUnloaded(object? sender, RoutedEventArgs e)
		{
			_isRunning = false;

			if (_viewModel?.Descriptor?.TerminalSession is { } session)
			{
				session.OutputUpdated -= OnSessionOutputUpdated;
			}
		}

		private void OnSessionOutputUpdated(object? sender, EventArgs e)
		{
			Dispatcher.UIThread.Post(() =>
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
			if (e.Property != RangeBase.ValueProperty || !_isRunning)
				return;

			TerminalHost.ViewportY = (int)Math.Round(TerminalScrollBar.Value);
		}

		/// <summary>
		/// Sizes the scrollback bar from the terminal buffer: the range is the scrollable
		/// scrollback, the thumb covers the visible part of it.
		/// </summary>
		private void UpdateScrollBar()
		{
			if (TerminalHost == null || TerminalScrollBar == null)
				return;

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
			TerminalScrollBar.Value = TerminalHost.ViewportY;
			TerminalScrollBar.IsVisible = maximum > 0;
		}
	}
}
