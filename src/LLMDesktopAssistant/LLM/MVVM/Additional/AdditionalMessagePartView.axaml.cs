using Avalonia.Controls;
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
		Cursor = DataContext is AttachmentMessagePart attachment && HasOpenTarget(attachment)
			? new Cursor(StandardCursorType.Hand)
			: Cursor.Default;
	}

	private static bool HasOpenTarget(AttachmentMessagePart attachment)
		=> !string.IsNullOrWhiteSpace(attachment.SourceUrl) || !string.IsNullOrWhiteSpace(attachment.LocalPath);

	private async void OnPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (DataContext is not AttachmentMessagePart attachment)
			return;

		var url = attachment.SourceUrl;
		if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
			return;

		var launcher = TopLevel.GetTopLevel(this)?.Launcher;
		if (launcher == null)
			return;

		if (uri.IsFile && File.Exists(uri.LocalPath))
			await launcher.LaunchFileInfoAsync(new FileInfo(uri.LocalPath));
		else
			await launcher.LaunchUriAsync(uri);
	}
}
