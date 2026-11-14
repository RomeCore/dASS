using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace LLMDesktopAssistant.LLM.MVVM.Additional.Chat
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
		private AdditionalChatDataOverlayItem? _draggedItem;
		private Control? _draggedWindow;
		private TranslateTransform? _dragTransform;
		private Point _pointerOrigin;
		private Point _windowOrigin;
		private Size _area;

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

			var window = source
				.GetSelfAndVisualAncestors()
				.OfType<Border>()
				.FirstOrDefault(border => border.Classes.Contains("overlayWindow"));

			if (window?.DataContext is not AdditionalChatDataOverlayItem item)
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

		private void OnPointerMoved(object? sender, PointerEventArgs e)
		{
			if (_dragTransform is null || _draggedItem is null)
				return;

			var position = e.GetPosition(this);
			_dragTransform.X = position.X - _pointerOrigin.X;
			_dragTransform.Y = position.Y - _pointerOrigin.Y;
		}

		private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (_draggedItem is null || _draggedWindow is null || _dragTransform is null)
				return;

			// A press that did not move is a click: writing the same place back would persist nothing useful.
			if (Math.Abs(_dragTransform.X) > 0.5 || Math.Abs(_dragTransform.Y) > 0.5)
			{
				_draggedItem.Left = Fraction(_windowOrigin.X + _dragTransform.X, _area.Width);
				_draggedItem.Top = Fraction(_windowOrigin.Y + _dragTransform.Y, _area.Height);
			}

			ResetDrag(e.Pointer);
		}

		private void ResetDrag(IPointer pointer)
		{
			if (_draggedWindow is not null)
				_draggedWindow.RenderTransform = null;

			pointer.Capture(null);
			_draggedItem = null;
			_draggedWindow = null;
			_dragTransform = null;
		}

		/// <summary>
		/// Converts a pixel offset into the fraction of the area the item stores, keeping it inside the area.
		/// </summary>
		private static double Fraction(double offset, double size)
			=> size > 0 ? Math.Clamp(offset / size, 0, 1) : 0;
	}
}
