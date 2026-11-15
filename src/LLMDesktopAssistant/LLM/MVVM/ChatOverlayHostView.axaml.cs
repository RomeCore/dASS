using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using LLMDesktopAssistant.Controls.Layout;
using LLMDesktopAssistant.LLM.MVVM.Additional.Chat;

namespace LLMDesktopAssistant.LLM.MVVM
{
	/// <summary>
	/// The overlay layer over the message sequence: the windows themselves and the tray button.
	/// </summary>
	/// <remarks>
	/// Dragging has to live in the view because it is a pointer gesture: the window is translated while the pointer
	/// moves and only the drop is written to the item, so a drag costs one change instead of one per pointer event -
	/// every change of the item is persisted.
	/// </remarks>
	public partial class ChatOverlayHostView : UserControl
	{
		/// <summary>
		/// The narrowest a window may be dragged to, in pixels: the header has to keep room for its buttons.
		/// </summary>
		private const double minimumWindowWidth = 160;

		/// <summary>
		/// The shortest a window may be dragged to, in pixels: a frame with nothing in it is a trap.
		/// </summary>
		private const double minimumWindowHeight = 64;

		private AdditionalChatDataOverlayItem? _draggedItem;
		private Control? _draggedWindow;
		private TranslateTransform? _dragTransform;
		private Point _pointerOrigin;
		private Point _windowOrigin;
		private Size _area;

		private AdditionalChatDataOverlayItem? _resizedItem;
		private Control? _resizedWindow;
		private Point _resizePointerOrigin;
		private Point _resizeWindowOrigin;
		private Size _resizeStartSize;
		private Size _resizeSize;
		private Size _resizeArea;

		/// <summary>
		/// Initializes a new instance of the <see cref="ChatOverlayHostView"/> class.
		/// </summary>
		public ChatOverlayHostView()
		{
			InitializeComponent();

			// The captured header routes the rest of the gesture here, even when a payload under the pointer
			// would otherwise mark the event as handled.
			AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
			AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);

			// Using a window - anywhere in it, not only its header - is what raises it above its neighbours.
			AddHandler(PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
		}

		/// <summary>
		/// Raises the window the press landed in, so the one being used is the one on top.
		/// </summary>
		private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
		{
			if (e.Source is not Visual source)
				return;

			if (FindWindow(source) is not { } window || window.DataContext is not AdditionalChatDataOverlayItem item)
				return;

			if (DataContext is ChatOverlayHostViewModel host)
				host.BringToFront(item);
		}

		private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
		{
			if (sender is not Control header || header.DataContext is not AdditionalChatDataOverlayItem item)
				return;

			// The buttons in the header act on themselves: a press that starts on one is not a drag.
			if (e.Source is Visual source && source.FindAncestorOfType<Button>(includeSelf: true) is not null)
				return;

			if (!e.GetCurrentPoint(header).Properties.IsLeftButtonPressed)
				return;

			// The header sits in a grid inside the frame, which is the window the panel lays out.
			if (header.Parent is not Control grid || grid.Parent is not Control window)
				return;

			_draggedItem = item;
			_draggedWindow = window;
			_area = Bounds.Size;
			_pointerOrigin = e.GetPosition(this);
			_windowOrigin = window.TranslatePoint(default, this) ?? default;
			_dragTransform = new TranslateTransform();
			window.RenderTransform = _dragTransform;

			e.Pointer.Capture(header);
			e.Handled = true;
		}

		/// <summary>
		/// Starts a resize: the window follows the pointer while it moves and the size is sealed on the drop.
		/// </summary>
		private void OnResizeGripPointerPressed(object? sender, PointerPressedEventArgs e)
		{
			if (sender is not Control grip || grip.DataContext is not AdditionalChatDataOverlayItem item)
				return;

			if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)
				return;

			if (FindWindow(grip) is not { } window)
				return;

			_resizedItem = item;
			_resizedWindow = window;
			_resizeArea = Bounds.Size;
			_resizeStartSize = window.Bounds.Size;
			_resizeSize = _resizeStartSize;
			_resizeWindowOrigin = window.TranslatePoint(default, this) ?? default;
			_resizePointerOrigin = e.GetPosition(this);

			e.Pointer.Capture(grip);
			e.Handled = true;
		}

		private void OnPointerMoved(object? sender, PointerEventArgs e)
		{
			if (_dragTransform is not null && _draggedItem is not null)
			{
				var position = e.GetPosition(this);
				_dragTransform.X = position.X - _pointerOrigin.X;
				_dragTransform.Y = position.Y - _pointerOrigin.Y;
				return;
			}

			if (_resizedWindow is not null)
				ResizeTo(e.GetPosition(this));
		}

		private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			DropMove(e.Pointer);
			DropResize(e.Pointer);
		}

		/// <summary>
		/// Sizes the window under the pointer.
		/// </summary>
		/// <remarks>
		/// The attached fractions are written, not the item: the panel arranges from them, so the payload lays itself
		/// out live - a terminal resizes its own grid on the way - while the drag still costs one persisted change
		/// instead of one per pointer event.
		/// </remarks>
		private void ResizeTo(Point position)
		{
			if (_resizedWindow is null || _resizeArea.Width <= 0 || _resizeArea.Height <= 0)
				return;

			// The window may grow only up to the edge of the area: past it the panel would move it instead.
			double maxWidth = Math.Max(0, _resizeArea.Width - _resizeWindowOrigin.X);
			double maxHeight = Math.Max(0, _resizeArea.Height - _resizeWindowOrigin.Y);

			double width = Math.Clamp(
				_resizeStartSize.Width + (position.X - _resizePointerOrigin.X),
				Math.Min(minimumWindowWidth, maxWidth),
				maxWidth);

			double height = Math.Clamp(
				_resizeStartSize.Height + (position.Y - _resizePointerOrigin.Y),
				Math.Min(minimumWindowHeight, maxHeight),
				maxHeight);

			_resizeSize = new Size(width, height);

			FractionPanel.SetRelativeWidth(_resizedWindow, width / _resizeArea.Width);
			FractionPanel.SetRelativeHeight(_resizedWindow, height / _resizeArea.Height);
		}

		private void DropMove(IPointer pointer)
		{
			if (_draggedItem is null || _draggedWindow is null || _dragTransform is null)
				return;

			// A press that did not move is a click: writing the same place back would persist nothing useful.
			if (Math.Abs(_dragTransform.X) > 0.5 || Math.Abs(_dragTransform.Y) > 0.5)
			{
				_draggedItem.Left = Fraction(_windowOrigin.X + _dragTransform.X, _area.Width);
				_draggedItem.Top = Fraction(_windowOrigin.Y + _dragTransform.Y, _area.Height);
			}

			_draggedWindow.RenderTransform = null;
			pointer.Capture(null);
			_draggedItem = null;
			_draggedWindow = null;
			_dragTransform = null;
		}

		/// <summary>
		/// Seals the size the user dragged as the window's own, exactly as an explicit width and height would.
		/// </summary>
		private void DropResize(IPointer pointer)
		{
			if (_resizedItem is null || _resizedWindow is null)
				return;

			// A click on the grip says nothing about the size: sealing it would freeze a window that hugged its content.
			if (Math.Abs(_resizeSize.Width - _resizeStartSize.Width) > 0.5
				|| Math.Abs(_resizeSize.Height - _resizeStartSize.Height) > 0.5)
			{
				_resizedItem.Width = Fraction(_resizeSize.Width, _resizeArea.Width);
				_resizedItem.Height = Fraction(_resizeSize.Height, _resizeArea.Height);
			}

			pointer.Capture(null);
			_resizedItem = null;
			_resizedWindow = null;
		}

		/// <summary>
		/// The frame of an overlay window a visual sits in, found by walking up: the payload's own borders are in the way.
		/// </summary>
		private static Border? FindWindow(Visual visual)
			=> visual
				.GetSelfAndVisualAncestors()
				.OfType<Border>()
				.FirstOrDefault(border => border.Classes.Contains("overlayWindow"));

		/// <summary>
		/// Converts a pixel offset into the fraction of the area the item stores, keeping it inside the area.
		/// </summary>
		private static double Fraction(double offset, double size)
			=> size > 0 ? Math.Clamp(offset / size, 0, 1) : 0;
	}
}
