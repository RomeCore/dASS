using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LiveMarkdown.Avalonia;
using LLMDesktopAssistant.Controls.Markdown.CodeBlocks;
using LLMDesktopAssistant.Controls.Markdown.Selection;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Controls;

public partial class MarkdownControl : UserControl
{
	public static readonly StyledProperty<string> MarkdownTextProperty =
		AvaloniaProperty.Register<MarkdownControl, string>(
			nameof(MarkdownText));

	public string MarkdownText
	{
		get => GetValue(MarkdownTextProperty);
		set => SetValue(MarkdownTextProperty, value);
	}

	public static readonly StyledProperty<bool> UsePlaintextProperty =
		AvaloniaProperty.Register<MarkdownControl, bool>(
			nameof(UsePlaintext));

	public bool UsePlaintext
	{
		get => GetValue(UsePlaintextProperty);
		set => SetValue(UsePlaintextProperty, value);
	}

	public static readonly StyledProperty<Func<Uri, bool>?> OpenLinkProperty =
		AvaloniaProperty.Register<MarkdownControl, Func<Uri, bool>?>(
			nameof(OpenLink));

	public Func<Uri, bool>? OpenLink
	{
		get => GetValue(OpenLinkProperty);
		set => SetValue(OpenLinkProperty, value);
	}

	public static readonly StyledProperty<string> ImageBasePathProperty =
		AvaloniaProperty.Register<MarkdownControl, string>(
			nameof(ImageBasePath));

	public string ImageBasePath
	{
		get => GetValue(ImageBasePathProperty);
		set => SetValue(ImageBasePathProperty, value);
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

	public static readonly StyledProperty<IMarkdownSelectionProcessorsProvider?> SelectionProcessorsProviderProperty =
		AvaloniaProperty.Register<MarkdownControl, IMarkdownSelectionProcessorsProvider?>(
			nameof(SelectionProcessorsProvider));

	/// <summary>
	/// Gets or sets the actions offered by the selection toolbar: the floating toolbar that appears above the
	/// text the user selected inside this control's Markdown content.
	/// </summary>
	/// <remarks>
	/// The provider is supplied by the consumer of this control and is never resolved from the container: the
	/// control knows nothing about chats or services, and a <see langword="null"/> provider disables the toolbar.
	/// </remarks>
	public IMarkdownSelectionProcessorsProvider? SelectionProcessorsProvider
	{
		get => GetValue(SelectionProcessorsProviderProperty);
		set => SetValue(SelectionProcessorsProviderProperty, value);
	}

	public static readonly StyledProperty<IReadOnlyList<ICodeBlockPreviewProvider>?> CodeBlockPreviewProvidersProperty =
		AvaloniaProperty.Register<MarkdownControl, IReadOnlyList<ICodeBlockPreviewProvider>?>(
			nameof(CodeBlockPreviewProviders), inherits: true);

	/// <summary>
	/// Gets or sets the preview tabs code blocks of this control offer (a rendered diagram, a page, a formula).
	/// Supplied by the consumer, never resolved from the container: the control knows nothing about chats.
	/// </summary>
	public IReadOnlyList<ICodeBlockPreviewProvider>? CodeBlockPreviewProviders
	{
		get => GetValue(CodeBlockPreviewProvidersProperty);
		set => SetValue(CodeBlockPreviewProvidersProperty, value);
	}

	public static readonly StyledProperty<IReadOnlyList<IMarkdownCodeBlockActionProvider>?> CodeBlockActionProvidersProperty =
		AvaloniaProperty.Register<MarkdownControl, IReadOnlyList<IMarkdownCodeBlockActionProvider>?>(
			nameof(CodeBlockActionProviders), inherits: true);

	/// <summary>
	/// Gets or sets the action providers behind the buttons of code blocks (copy, save, run).
	/// Supplied by the consumer, never resolved from the container.
	/// </summary>
	public IReadOnlyList<IMarkdownCodeBlockActionProvider>? CodeBlockActionProviders
	{
		get => GetValue(CodeBlockActionProvidersProperty);
		set => SetValue(CodeBlockActionProvidersProperty, value);
	}

	public static readonly StyledProperty<bool> IsStreamingProperty =
		AvaloniaProperty.Register<MarkdownControl, bool>(
			nameof(IsStreaming), inherits: true);

	/// <summary>
	/// Gets or sets a value indicating whether the markdown is still being generated. Code blocks use it to decide
	/// whether a fenced block is final (a closed fence or the end of the stream).
	/// </summary>
	public bool IsStreaming
	{
		get => GetValue(IsStreamingProperty);
		set => SetValue(IsStreamingProperty, value);
	}

	public static readonly StyledProperty<bool> CompactCodeBlocksProperty =
		AvaloniaProperty.Register<MarkdownControl, bool>(
			nameof(CompactCodeBlocks), inherits: true);

	/// <summary>
	/// Gets or sets a value indicating whether code blocks render as bare source: no top bar, no tabs, no actions.
	/// Intended for one-line surfaces such as tool statuses.
	/// </summary>
	public bool CompactCodeBlocks
	{
		get => GetValue(CompactCodeBlocksProperty);
		set => SetValue(CompactCodeBlocksProperty, value);
	}

	public static readonly StyledProperty<object?> OwnerProperty =
		AvaloniaProperty.Register<MarkdownControl, object?>(
			nameof(Owner), inherits: true);

	/// <summary>
	/// Gets or sets the owner domain object of this control. This can be a <see cref="ChatMessage"/>, a <see cref="ToolCall"/>, etc.
	/// </remarks>
	public object? Owner
	{
		get => GetValue(OwnerProperty);
		set => SetValue(OwnerProperty, value);
	}

	static MarkdownControl()
	{
		CodeBlockPreviewProvidersProperty.AddOwner<MarkdownRenderer>();
		CodeBlockActionProvidersProperty.AddOwner<MarkdownRenderer>();
		IsStreamingProperty.AddOwner<MarkdownRenderer>();
		CompactCodeBlocksProperty.AddOwner<MarkdownRenderer>();
		OwnerProperty.AddOwner<MarkdownRenderer>();

		MarkdownTextProperty.Changed.AddClassHandler<MarkdownControl>((o, e) => o.MarkdownTextChanged(e.NewValue as string, o.UsePlaintext));
		UsePlaintextProperty.Changed.AddClassHandler<MarkdownControl>((o, e) => o.MarkdownTextChanged(o.MarkdownText, (bool)e.NewValue!));
		ImageBasePathProperty.Changed.AddClassHandler<MarkdownControl>((o, e) => o.MarkdownRenderer.ImageBasePath = e.GetNewValue<string?>());
		SelectionProcessorsProviderProperty.Changed.AddClassHandler<MarkdownControl>((o, e) => o.SelectionToolbar.SelectionProcessorsProvider = e.GetNewValue<IMarkdownSelectionProcessorsProvider?>());
		
		OwnerProperty.Changed.AddClassHandler<MarkdownControl>((o, e) => o.SelectionToolbar.Owner = e.NewValue);
	}

	/// <summary>
	/// Reads the streaming flag from the control or from the document inside it.
	/// </summary>
	public static bool GetIsStreaming(AvaloniaObject target)
		=> target.GetValue(IsStreamingProperty);

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
		SelectionToolbar.Renderer = MarkdownRenderer;
		SelectionToolbar.SelectionProcessorsProvider = SelectionProcessorsProvider;
		SelectionToolbar.Owner = Owner;

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
		UIDispatcher.Post(() => control.Opacity = 1, DispatcherPriority.Render);
	}

	private void MarkdownTextChanged(string? newText, bool usePlaintext)
	{
		newText ??= string.Empty;

		// Streaming rebuilds the blocks the selection points at, so a floating toolbar would be left behind.
		SelectionToolbar.NotifyDocumentChanged();

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