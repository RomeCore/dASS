using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Users;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace LLMDesktopAssistant.LLM.Settings
{
	/// <summary>
	/// ViewModel for the "Add / Edit User" dialog. Edits a working copy of the user
	/// and applies the changes only when the user confirms the dialog.
	/// </summary>
	[ViewModelFor(typeof(EditUserDialogView))]
	public class EditUserDialogViewModel : ViewModelBase
	{
		private readonly UserInformation? _target;

		/// <summary>
		/// Gets the user created or updated by the dialog, or <see langword="null"/> when cancelled.
		/// </summary>
		public UserInformation? Result { get; private set; }

		/// <summary>
		/// Gets a value indicating whether the dialog edits an existing user.
		/// </summary>
		public bool IsEditMode => _target != null;

		/// <summary>
		/// Gets the localized dialog title.
		/// </summary>
		public string TitleText => LocalizationManager.LocalizeStatic(IsEditMode ? "settings.user.edit.title" : "settings.user.add.title");

		/// <summary>
		/// Gets the localized confirm button text.
		/// </summary>
		public string ConfirmButtonText => LocalizationManager.LocalizeStatic(IsEditMode ? "common.save" : "common.add");

		private string _login = string.Empty;
		/// <summary>
		/// Gets or sets the login of the user being edited.
		/// </summary>
		public string Login
		{
			get => _login;
			set => SetProperty(ref _login, value);
		}

		private string _name = string.Empty;
		/// <summary>
		/// Gets or sets the name of the user being edited.
		/// </summary>
		public string Name
		{
			get => _name;
			set => SetProperty(ref _name, value);
		}

		private string _description = string.Empty;
		/// <summary>
		/// Gets or sets the description of the user being edited.
		/// </summary>
		public string Description
		{
			get => _description;
			set => SetProperty(ref _description, value);
		}

		private bool _isAvailable = true;
		/// <summary>
		/// Gets or sets a value indicating whether the user may send messages in the chat.
		/// </summary>
		public bool IsAvailable
		{
			get => _isAvailable;
			set => SetProperty(ref _isAvailable, value);
		}

		private string _base64ProfileImage = string.Empty;
		/// <summary>
		/// Gets or sets the base64 encoded profile image of the user being edited.
		/// </summary>
		public string Base64ProfileImage
		{
			get => _base64ProfileImage;
			set => SetProperty(ref _base64ProfileImage, value);
		}

		private Bitmap? _profileImage;
		/// <summary>
		/// Gets the preview bitmap of the profile image.
		/// </summary>
		public Bitmap? ProfileImage
		{
			get => _profileImage;
			private set => SetProperty(ref _profileImage, value);
		}

		private string? _errorMessage;
		/// <summary>
		/// Gets or sets the validation error message shown in the dialog.
		/// </summary>
		public string? ErrorMessage
		{
			get => _errorMessage;
			set => SetProperty(ref _errorMessage, value);
		}

		/// <summary>
		/// Gets the command that validates the input and closes the dialog with the result.
		/// </summary>
		public ICommand SaveCommand { get; }

		/// <summary>
		/// Gets the command that closes the dialog without applying changes.
		/// </summary>
		public ICommand CancelCommand { get; }

		/// <summary>
		/// Gets the command that picks a profile image from disk.
		/// </summary>
		public IAsyncRelayCommand SelectImageCommand { get; }

		/// <summary>
		/// Gets the command that clears the profile image.
		/// </summary>
		public ICommand ClearImageCommand { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="EditUserDialogViewModel"/> class.
		/// </summary>
		/// <param name="target">The user to edit, or <see langword="null"/> to create a new user.</param>
		public EditUserDialogViewModel(UserInformation? target)
		{
			_target = target;

			Login = target?.Login ?? string.Empty;
			Name = target?.Name ?? string.Empty;
			Description = target?.Description ?? string.Empty;
			Base64ProfileImage = target?.Base64ProfileImage ?? string.Empty;
			IsAvailable = target?.IsAvailable ?? true;

			SaveCommand = new RelayCommand(Save);
			CancelCommand = new RelayCommand(Cancel);
			SelectImageCommand = new AsyncRelayCommand(SelectImageAsync);
			ClearImageCommand = new RelayCommand(ClearImage);

			LoadProfileImage();
		}

		private void Save()
		{
			ErrorMessage = null;

			if (string.IsNullOrWhiteSpace(Login))
			{
				ErrorMessage = LocalizationManager.LocalizeStatic("settings.user.login_required.error");
				return;
			}

			if (_target is { } target)
			{
				target.Login = Login.Trim();
				target.Name = Name;
				target.Description = Description;
				target.Base64ProfileImage = Base64ProfileImage;
				target.IsAvailable = IsAvailable;
				Result = target;
			}
			else
			{
				Result = new UserInformation
				{
					Login = Login.Trim(),
					Name = Name,
					Description = Description,
					Base64ProfileImage = Base64ProfileImage,
					IsAvailable = IsAvailable
				};
			}

			DialogManager.CloseDialog(true);
		}

		private void Cancel()
		{
			DialogManager.CloseDialog(false);
		}

		private void LoadProfileImage()
		{
			try
			{
				if (string.IsNullOrWhiteSpace(Base64ProfileImage))
				{
					ProfileImage = null;
					return;
				}

				var bytes = Convert.FromBase64String(Base64ProfileImage);
				using var ms = new MemoryStream(bytes);
				ProfileImage = new Bitmap(ms);
			}
			catch
			{
				ProfileImage = null;
			}
		}

		private async Task SelectImageAsync()
		{
			var files = await App.MainTopLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = LocalizationManager.LocalizeStatic("settings.user.select_image.title"),
				FileTypeFilter =
				[
					new("Image files")
					{
						Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp"]
					}
				],
				AllowMultiple = false
			});

			var file = files?.FirstOrDefault();
			if (file == null)
				return;

			try
			{
				await using var stream = await file.OpenReadAsync();
				using var image = await Image.LoadAsync(stream);
				image.Mutate(x => x.Resize(new ResizeOptions
				{
					Size = new Size(128, 128),
					Mode = ResizeMode.Crop
				}));

				using var ms = new MemoryStream();
				await image.SaveAsync(ms, PngFormat.Instance);
				Base64ProfileImage = Convert.ToBase64String(ms.ToArray());
				LoadProfileImage();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to load image: {ex.Message}");
			}
		}

		private void ClearImage()
		{
			Base64ProfileImage = string.Empty;
			ProfileImage = null;
		}
	}
}
