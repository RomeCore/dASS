using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LiveMarkdown.Avalonia;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;

namespace LLMDesktopAssistant.Controls;

public partial class MarkdownControl : UserControl
{
	public static readonly StyledProperty<string> MarkdownTextProperty =
		AvaloniaProperty.Register<MarkdownControl, string>(
			nameof(MarkdownText));

	public static readonly StyledProperty<bool> UsePlaintextProperty =
		AvaloniaProperty.Register<MarkdownControl, bool>(
			nameof(UsePlaintext));

	public static readonly StyledProperty<Func<Uri, bool>?> OpenLinkProperty =
		AvaloniaProperty.Register<MarkdownControl, Func<Uri, bool>?>(
			nameof(OpenLink));

	public string MarkdownText
	{
		get => GetValue(MarkdownTextProperty);
		set => SetValue(MarkdownTextProperty, value);
	}

	public bool UsePlaintext
	{
		get => GetValue(UsePlaintextProperty);
		set => SetValue(UsePlaintextProperty, value);
	}

	public Func<Uri, bool>? OpenLink
	{
		get => GetValue(OpenLinkProperty);
		set => SetValue(OpenLinkProperty, value);
	}

	public static readonly StyledProperty<bool> AnimateBlocksProperty =
		AvaloniaProperty.Register<MarkdownControl, bool>(
			nameof(AnimateBlocks));

	/// <summary>
	/// Gets or sets a value indicating whether newly rendered Markdown blocks fade in
	/// instead of appearing instantly. Intended to be enabled while a message is streaming.
	/// </summary>
	public bool AnimateBlocks
	{
		get => GetValue(AnimateBlocksProperty);
		set => SetValue(AnimateBlocksProperty, value);
	}

	public static readonly StyledProperty<bool> ShowCaretProperty =
		AvaloniaProperty.Register<MarkdownControl, bool>(
			nameof(ShowCaret));

	/// <summary>
	/// Gets or sets a value indicating whether a blinking caret is shown at the end of the last
	/// rendered Markdown block. Intended to be enabled while a message is streaming.
	/// </summary>
	public bool ShowCaret
	{
		get => GetValue(ShowCaretProperty);
		set => SetValue(ShowCaretProperty, value);
	}

	static MarkdownControl()
	{
		MarkdownTextProperty.Changed.AddClassHandler<MarkdownControl>((o, e) => o.MarkdownTextChanged(e.NewValue as string, o.UsePlaintext));
		UsePlaintextProperty.Changed.AddClassHandler<MarkdownControl>((o, e) => o.MarkdownTextChanged(o.MarkdownText, (bool)e.NewValue!));
	}

	private readonly ObservableStringBuilder _markdownBuilder = new();

	/// <summary>
	/// Duration of the fade-in applied to a newly rendered Markdown block.
	/// </summary>
	private static readonly TimeSpan BlockFadeInDuration = TimeSpan.FromMilliseconds(450);

	/// <summary>
	/// The block containers whose children are observed for newly added blocks.
	/// </summary>
	private readonly HashSet<Panel> _trackedBlockPanels = [];

	public MarkdownControl()
	{
		InitializeComponent();

		var thisRef = new WeakReference<MarkdownControl>(this);
		void MarkdownRenderer_LinkClick(object? sender, LinkClickedEventArgs e)
		{
			if (thisRef.TryGetTarget(out var markdownControl))
			{
				if (e.HRef != null)
				{
					if (markdownControl.OpenLink?.Invoke(e.HRef) is true)
						return;
					ServiceRegistry.Provider.GetService<ILinkOpener>()?.OpenLink(e.HRef);
				}
			}
		}
		MarkdownRenderer.LinkClick += MarkdownRenderer_LinkClick;

		MarkdownRenderer.ImageBasePath = null;
		MarkdownRenderer.CodeBlockColorTheme = TextMateSharp.Grammars.ThemeName.Monokai;
		MarkdownRenderer.MarkdownBuilder = _markdownBuilder;

		// The root block container ("MarkdownDocument") already exists at this point.
		foreach (var panel in MarkdownRenderer.GetVisualChildren().OfType<Panel>())
			TrackBlockPanel(panel);
	}

	/// <summary>
	/// Starts observing the children of a Markdown block container so that
	/// newly added blocks can be animated.
	/// </summary>
	private void TrackBlockPanel(Panel panel)
	{
		if (!_trackedBlockPanels.Add(panel))
			return;

		((INotifyCollectionChanged)panel.Children).CollectionChanged += OnBlockPanelChildrenChanged;
	}

	private void OnBlockPanelChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action is NotifyCollectionChangedAction.Move || e.NewItems is null)
			return;

		foreach (var child in e.NewItems.OfType<Control>())
		{
			// Nested containers (a list inside the document) host their own animated blocks.
			if (child is Panel nested && IsBlockContainer(nested))
				TrackBlockPanel(nested);

			if (AnimateBlocks)
				FadeInBlock(child);
		}
	}

	/// <summary>
	/// Determines whether the panel is a Markdown block container
	/// (the document root or a list) rather than an internal part of a single block.
	/// </summary>
	private static bool IsBlockContainer(Panel panel) =>
		panel.Classes.Contains("MarkdownDocument") || panel.Classes.Contains("ListBlock");

	/// <summary>
	/// Makes the control appear with a short opacity transition instead of popping in instantly.
	/// </summary>
	private static void FadeInBlock(Control control)
	{
		// The initial opacity must be applied instantly, so it is set before the transition exists.
		control.Transitions = null;
		control.Opacity = 0;
		control.Transitions = new Transitions
		{
			new DoubleTransition
			{
				Property = Visual.OpacityProperty,
				Duration = BlockFadeInDuration,
				Easing = new CubicEaseOut()
			}
		};

		// Deferred, so the control is already attached and laid out when the transition starts.
		Dispatcher.UIThread.Post(() => control.Opacity = 1, DispatcherPriority.Render);
	}

	private void MarkdownTextChanged(string? newText, bool usePlaintext)
	{
		newText ??= string.Empty;

		if (usePlaintext)
		{
			_markdownBuilder.Clear();
			MarkdownTextBlock.IsVisible = true;
			MarkdownTextBlock.Inlines = [new Run(newText)];
		}
		else
		{
			MarkdownTextBlock.IsVisible = false;
			MarkdownTextBlock.Inlines = null;
			var oldText = _markdownBuilder.ToString();
			if (!newText.StartsWith(oldText))
				_markdownBuilder.Clear();
			string delta = newText[_markdownBuilder.Length..];
			if (!string.IsNullOrEmpty(delta))
				_markdownBuilder.Append(delta);
		}
	}
}