using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
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

	static MarkdownControl()
	{
		MarkdownTextProperty.Changed.AddClassHandler<MarkdownControl>((o, e) => o.MarkdownTextChanged(e.NewValue as string, o.UsePlaintext));
		UsePlaintextProperty.Changed.AddClassHandler<MarkdownControl>((o, e) => o.MarkdownTextChanged(o.MarkdownText, (bool)e.NewValue!));
	}

	private readonly ObservableStringBuilder _markdownBuilder = new();

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