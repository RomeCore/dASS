using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace LLMDesktopAssistant.LLM.MVVM.Additional;

public partial class AdditionalMessagePartView : UserControl
{
	public AdditionalMessagePartView()
	{
		InitializeComponent();

		DataContextChanged += OnDataContextChanged;
		PointerPressed += OnPointerPressed;
	}

	private void OnDataContextChanged(object? sender, EventArgs e)
	{
		Cursor = DataContext switch
		{
			AttachmentMessagePart attachment when HasOpenTarget(attachment) => new Cursor(StandardCursorType.Hand),
			AdditionalMessageContentPart content when !string.IsNullOrWhiteSpace(content.Content) => new Cursor(StandardCursorType.Hand),
			_ => Cursor.Default
		};
	}

	private static bool HasOpenTarget(AttachmentMessagePart attachment)
		=> !string.IsNullOrWhiteSpace(attachment.SourceUrl) || !string.IsNullOrWhiteSpace(attachment.LocalPath);

	private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			return;

		// A content chip hides text behind it: the click opens the whole thing where the cursor already is.
		if (DataContext is AdditionalMessageContentPart content && !string.IsNullOrWhiteSpace(content.Content))
		{
			FlyoutBase.ShowAttachedFlyout(ChipBorder);
			e.Handled = true;
			return;
		}

		if (DataContext is not AttachmentMessagePart attachment)
			return;

		var url = attachment.SourceUrl;
		if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
			return;

		var launcher = TopLevel.GetTopLevel(this)?.Launcher;
		if (launcher == null)
			return;

		if (uri.IsFile && File.Exists(uri.LocalPath))
			launcher.LaunchFileInfoAsync(new FileInfo(uri.LocalPath));
		else
			launcher.LaunchUriAsync(uri);
	}
}
