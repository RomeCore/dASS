using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.LLM.MVVM.Messages;
using Material.Icons;

namespace LLMDesktopAssistant.UIExtensions.MessageExtensions
{
	[MessageExtension(Targets = MessageExtensionTargets.User)]
	public class SaveUserMessageExtension : MessageExtension
	{
		public override int Order => 1;

		public SaveUserMessageExtension(MessageViewModelBase viewModel)
		{
			Icon = MaterialIconKind.ContentSave;

			Tooltip = "save";

			Command = new AsyncRelayCommand(async () =>
			{
				var result = await App.MainTopLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
				{
					FileTypeChoices = [
						new FilePickerFileType("Text files") { Patterns = ["*.txt"] },
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