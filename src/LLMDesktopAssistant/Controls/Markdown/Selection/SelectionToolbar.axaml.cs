using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using LiveMarkdown.Avalonia;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;
using Serilog;

namespace LLMDesktopAssistant.Controls.Markdown.Selection
{
	/// <summary>
	/// The floating toolbar that appears above the text the user selected inside a markdown renderer.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The toolbar is fed by <see cref="ProcessorsProvider"/> and does nothing on its own: it tracks the selection,
	/// snapshots it, asks the provider which actions apply and shows their buttons.
	/// </para>
	/// <para>
	/// It is shown when the pointer is released after a drag, and on a keyboard selection (the renderer's
	/// <see cref="MarkdownRenderer.CanCopy"/> turning on while no pointer is down). It hides on scroll, on a
	/// document change (streaming rebuilds the blocks the selection points at), on detach, on Escape and when the
	/// selection goes away. Running an action closes the toolbar and clears the selection first, then hands the
	/// immutable snapshot to the processor.
	/// </para>
	/// </remarks>
	public partial class SelectionToolbar : UserControl
	{
		/// <summary>Defines the <see cref="Renderer"/> property.</summary>
		public static readonly StyledProperty<MarkdownRenderer?> RendererProperty =
			AvaloniaProperty.Register<SelectionToolbar, MarkdownRenderer?>(nameof(Renderer));

		/// <summary>Defines the <see cref="ProcessorsProvider"/> property.</summary>
		public static readonly StyledProperty<IMarkdownSelectionProcessorsProvider?> ProcessorsProviderProperty =
			AvaloniaProperty.Register<SelectionToolbar, IMarkdownSelectionProcessorsProvider?>(nameof(ProcessorsProvider));

		private readonly SelectionToolbarViewModel _viewModel = new();

		private MarkdownSelectionContext? _context;
		private bool _pointerDown;
		private bool _suppressDismiss;
		
		private ScrollViewer? _scrollViewer;
		private TopLevel? _topLevel;

		/// <summary>
		/// Initializes a new instance of the <see cref="SelectionToolbar"/> class.
		/// </summary>
		public SelectionToolbar()
		{
			InitializeComponent();

			DataContext = _viewModel;
			_viewModel.ProcessCommand = new AsyncRelayCommand<IMarkdownSelectionProcessor>(ExecuteAsync);
			ToolbarPopup.Closed += OnPopupClosed;
		}

		/// <summary>
		/// Gets or sets the renderer whose selection this toolbar serves.
		/// </summary>
		public MarkdownRenderer? Renderer
		{
			get => GetValue(RendererProperty);
			set => SetValue(RendererProperty, value);
		}

		/// <summary>
		/// Gets or sets the provider of the actions offered for a selection. A <see langword="null"/> provider
		/// disables the toolbar entirely.
		/// </summary>
		public IMarkdownSelectionProcessorsProvider? ProcessorsProvider
		{
			get => GetValue(ProcessorsProviderProperty);
			set => SetValue(ProcessorsProviderProperty, value);
		}

		/// <summary>
		/// Hides the toolbar because the document changed under it (a streaming update rebuilds the blocks the
		/// selection pointed at).
		/// </summary>
		public void NotifyDocumentChanged() => Hide(clearSelection: false);

		/// <inheritdoc />
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);

			if (change.Property == RendererProperty)
				AttachRenderer(change.GetNewValue<MarkdownRenderer?>());
		}

		/// <inheritdoc />
		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);

			_scrollViewer = this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
			if (_scrollViewer is not null)
				_scrollViewer.ScrollChanged += OnScrollChanged;

			_topLevel = TopLevel.GetTopLevel(this);
			_topLevel?.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
		}

		/// <inheritdoc />
		protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
		{
			Hide(clearSelection: false);

			if (_scrollViewer is not null)
			{
				_scrollViewer.ScrollChanged -= OnScrollChanged;
				_scrollViewer = null;
			}

			_topLevel?.RemoveHandler(KeyDownEvent, OnKeyDown);
			_topLevel = null;

			base.OnDetachedFromVisualTree(e);
		}

		private void AttachRenderer(MarkdownRenderer? renderer)
		{
			if (renderer is null)
				return;

			renderer.AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
			renderer.AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
			renderer.PropertyChanged += OnRendererPropertyChanged;
		}

		private void OnRendererPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == MarkdownRenderer.CanCopyProperty)
				OnCanCopyChanged(e.GetNewValue<bool>());
		}

		private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
		{
			// A new press starts a new selection: the old toolbar has nothing left to act on.
			_pointerDown = true;
			Hide(clearSelection: false);
		}

		private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			_pointerDown = false;

			// Deferred: the renderer finishes the drag in its own handler, so the selection is only final
			// once the current event has been processed.
			Dispatcher.UIThread.Post(TryShow);
		}

		private void OnCanCopyChanged(bool canCopy)
		{
			if (!canCopy)
			{
				Hide(clearSelection: false);
				return;
			}

			// Keyboard selections have no pointer release to wait for. During a drag the release decides.
			if (!_pointerDown)
				Dispatcher.UIThread.Post(TryShow);
		}

		private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => Hide(clearSelection: false);

		private void OnKeyDown(object? sender, KeyEventArgs e)
		{
			if (e.Key != Key.Escape || !ToolbarPopup.IsOpen)
				return;

			Hide(clearSelection: true);
			e.Handled = true;
		}

		private void OnPopupClosed(object? sender, EventArgs e)
		{
			if (_suppressDismiss)
				return;

			// Dismissed by a click elsewhere or by light dismiss. The text block keeps its selection (the popup
			// never took the focus), so drop it explicitly - a highlight with no toolbar is a dead end.
			_viewModel.Clear();
			_context = null;
			Renderer?.ClearSelection();
		}

		private void TryShow()
		{
			if (ToolbarPopup.IsOpen)
				return;

			if (Renderer is not { } renderer || ProcessorsProvider is not { } provider)
				return;

			if (CreateContext(renderer) is not { } context)
				return;

			if (!_viewModel.Populate(context, provider))
			{
				_viewModel.Clear();
				return;
			}

			if (GetAnchorRect(context) is not { } anchor)
			{
				_viewModel.Clear();
				return;
			}

			_context = context;
			ToolbarPopup.PlacementTarget = context.Renderer;
			ToolbarPopup.PlacementRect = anchor;
			ToolbarPopup.IsOpen = true;
		}

		private async Task ExecuteAsync(IMarkdownSelectionProcessor? processor)
		{
			if (processor is null || _context is not { } context)
				return;

			Hide(clearSelection: true);

			try
			{
				await processor.ExecuteAsync(context, CancellationToken.None);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Markdown selection processor failed: {Error}", ex.Message);
				ServiceRegistry.Provider.GetService<IToastService>()?
					.ShowError(Locale.Get("message.selection.failed.title"), ex.Message);
			}
		}

		private void Hide(bool clearSelection)
		{
			if (ToolbarPopup.IsOpen)
			{
				_suppressDismiss = true;
				try
				{
					ToolbarPopup.IsOpen = false;
				}
				finally
				{
					_suppressDismiss = false;
				}
			}

			_viewModel.Clear();
			_context = null;

			if (clearSelection)
				Renderer?.ClearSelection();
		}

		/// <summary>
		/// Snapshots the selection of every visible block of the renderer. Blocks are visited in document order,
		/// which is the order the renderer joins them in for the clipboard as well.
		/// </summary>
		private static MarkdownSelectionContext? CreateContext(MarkdownRenderer renderer)
		{
			var ranges = new List<MarkdownSelectedRange>();

			foreach (var block in renderer.GetVisualDescendants().OfType<MarkdownTextBlock>())
			{
				if (!block.IsEffectivelyVisible)
					continue;

				var start = Math.Min(block.SelectionStart, block.SelectionEnd);
				var length = Math.Abs(block.SelectionEnd - block.SelectionStart);
				if (length <= 0)
					continue;

				ranges.Add(new MarkdownSelectedRange(block, start, length, block.ActualSelectedText ?? string.Empty));
			}

			if (ranges.Count == 0)
				return null;

			var text = renderer.SelectedText;
			if (string.IsNullOrEmpty(text))
				text = string.Join("\n\n", ranges.Select(static range => range.Text));

			return new MarkdownSelectionContext
			{
				Text = text,
				Ranges = ranges,
				Renderer = renderer,
			};
		}

		/// <summary>
		/// Gets the rectangle the toolbar is anchored to, expressed in the coordinate space of the first selected
		/// block.
		/// </summary>
		/// <remarks>
		/// The vertical anchor is the topmost selected line (the toolbar sits above the selection, not above a
		/// bounding box that reaches half a paragraph up), while the horizontal one is the middle of the selected
		/// area as a whole - across every selected block, not just the first one. Centring over the first block alone
		/// pins the toolbar to the left edge when the selection starts with a two-character stub in one block and
		/// continues over a full line in the next.
		/// </remarks>
		private static Rect? GetAnchorRect(MarkdownSelectionContext context)
		{
			var anchorCtrl = context.Renderer;

			var left = double.MaxValue;
			var right = double.MinValue;
			var top = double.MaxValue;
			var bottom = double.MinValue;

			foreach (var range in context.Ranges)
			{
				foreach (var rect in range.Block.GetTextRangeBoundsInControl(range.Start, range.Length))
				{
					var origin = range.Block.TranslatePoint(rect.TopLeft, anchorCtrl);

					if (origin is not { } point)
						continue;

					left = Math.Min(left, point.X);
					right = Math.Max(right, point.X + rect.Width);
					top = Math.Min(top, point.Y);
					bottom = Math.Max(bottom, point.Y + rect.Height);
				}
			}

			if (left > right || top > bottom)
				return null;

			return new Rect(left, top, Math.Max(0d, right - left), Math.Max(0d, bottom - top));
		}
	}
}
