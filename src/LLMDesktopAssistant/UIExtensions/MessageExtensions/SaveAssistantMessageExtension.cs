using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.LLM.MVVM.Messages;
using Material.Icons;

namespace LLMDesktopAssistant.UIExtensions.MessageExtensions
{
	[MessageExtension(Targets = MessageExtensionTargets.Assistant)]
	public class SaveAssistantMessageExtension : MessageExtension
	{
		public override int Order => 1;

		public SaveAssistantMessageExtension(MessageViewModelBase viewModel)
		{
			Icon = MaterialIconKind.ContentSave;

			Tooltip = "message.save_markdown";

			Command = new AsyncRelayCommand(async () =>
			{
				var result = await App.MainTopLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
				{
					FileTypeChoices = [
						new FilePickerFileType("Markdown files") { Patterns = ["*.md"] },
						new FilePickerFileType("Any files") { Patterns = ["*.*"] }
					],
					ShowOverwritePrompt = true
				});

				if (result != null)
				{
					var path = result.Path.LocalPath;
					File.WriteAllText(path, viewModel.Message.Content);
				}
			});
		}
	}
}