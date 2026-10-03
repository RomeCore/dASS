using Avalonia.Collections;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM;
using LLMDesktopAssistant.LLM.Services.Attachments;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.MVVM.Attachments
{
	public class AttachmentDraftViewModel : ViewModelBase
	{
		public AttachmentsManagerViewModel Manager { get; }
		public Uri SourceUri { get; }
		public string? Title { get; }
		public AttachmentApplicationParameters Parameters { get; }

		public bool CopyToWorkingDirectory
		{
			get => Parameters.CopyToWorkingDirectory;
			set
			{
				Parameters.CopyToWorkingDirectory = value;
				RaisePropertyChanged();
			}
		}

		public bool ApplyNative
		{
			get => Parameters.ApplyNative;
			set
			{
				Parameters.ApplyNative = value;
				RaisePropertyChanged();
			}
		}

		public ICommand ApplyCommand { get; }
		public ICommand RemoveCommand { get; }

		public AttachmentDraftViewModel(AttachmentsManagerViewModel parent,
			Uri uri)
		{
			Manager = parent;
			SourceUri = uri;
			Title = Path.GetFileName(uri.LocalPath);
			Parameters = new AttachmentApplicationParameters
			{
				SourceUri = uri,
				CopyToWorkingDirectory = true,
				ApplyNative = false
			};

			ApplyCommand = new AsyncRelayCommand(ApplyAsync);
			RemoveCommand = new RelayCommand(Remove);
		}

		private async Task ApplyAsync()
		{
			var attachment = await Manager.ApplicationService.ApplyAttachmentAsync(Parameters);
			Manager.UserInput.AddPart(attachment);
			Manager.Drafts.Remove(this);
		}

		private void Remove()
		{
			Manager.Drafts.Remove(this);
		}
	}

	[ViewModelFor(typeof(AttachmentsManagerView))]
	public class AttachmentsManagerViewModel : ViewModelBase
	{
		public UserInputViewModel UserInput { get; }
		public IAttachmentApplicationService ApplicationService { get; }

		private readonly AvaloniaList<AttachmentDraftViewModel> _drafts = [];
		public ICollection<AttachmentDraftViewModel> Drafts
		{
			get => _drafts;
			set
			{
				_drafts.Clear();
				_drafts.AddRange(value);
			}
		}

		private string _inputUrl = string.Empty;
		public string InputUrl
		{
			get => _inputUrl;
			set => SetProperty(ref _inputUrl, value);
		}

		public ICommand AddUrlCommand { get; }
		public ICommand AttachFilesCommand { get; }

		public bool OpenedInDialog { get; }
		public ICommand CloseDialogCommand { get; }

		public AttachmentsManagerViewModel(UserInputViewModel parent)
		{
			UserInput = parent;
			ApplicationService = parent.Chat.Services.GetRequiredService<IAttachmentApplicationService>();

			AddUrlCommand = new RelayCommand(AddUrl);
			AttachFilesCommand = new AsyncRelayCommand(AttachFiles);

			_drafts.CollectionChanged += (s, e) =>
			{
				// Close the dialog when there are no drafts left.
				if (Drafts.Count == 0)
				{
					DialogManager.CloseDialog();
				}
			};

			OpenedInDialog = true;
			CloseDialogCommand = new RelayCommand(() =>
			{
				DialogManager.CloseDialog();
			});
		}

		private void AddUrl()
		{
			var url = InputUrl?.Trim();
			if (string.IsNullOrEmpty(url))
				return;

			var draft = new AttachmentDraftViewModel(this, new Uri(url));
			_drafts.Add(draft);

			InputUrl = string.Empty;
		}

		private async Task AttachFiles()
		{
			var result = await App.MainTopLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = Locale.Get("env.select_working_directory"),
				FileTypeFilter = [
					new FilePickerFileType("All files (*.*)")
				],
				AllowMultiple = false
			});

			if (result.Count > 0)
			{
				foreach (var file in result)
				{
					var draft = new AttachmentDraftViewModel(this, file.Path);
					_drafts.Add(draft);
				}
			}
		}

		public async void AcceptDrop(DragEventArgs args)
		{
			if (args.DataTransfer.TryGetFiles() is IStorageItem[] files)
			{
				foreach (var file in files)
				{
					var draft = new AttachmentDraftViewModel(this, file.Path);
					_drafts.Add(draft);
				}
			}

			if (args.DataTransfer.TryGetText() is string text)
			{
				var draft = new AttachmentDraftViewModel(this, new Uri(text));
				_drafts.Add(draft);
			}
		}

		public async void AcceptImage(Bitmap image)
		{
			var pathToSave = Path.Combine(Directories.TempFiles, $"cp_{Guid.NewGuid()}.png");
			image.Save(pathToSave, new PngBitmapEncoderOptions());

			var draft = new AttachmentDraftViewModel(this, new Uri("file:///" + pathToSave, UriKind.Absolute));
			_drafts.Add(draft);
		}

		public async void AcceptFiles(IStorageItem[] files)
		{
			foreach (var file in files)
			{
				var draft = new AttachmentDraftViewModel(this, file.Path);
				_drafts.Add(draft);
			}
		}
	}
}