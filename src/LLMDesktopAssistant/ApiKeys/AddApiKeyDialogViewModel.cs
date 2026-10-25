using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.ApiKeys
{
	/// <summary>
	/// ViewModel for the "Add / Edit API Key" dialog.
	/// </summary>
	[ViewModelFor(typeof(AddApiKeyDialogView))]
	public class AddApiKeyDialogViewModel : ViewModelBase
	{
		private readonly IApiKeyManagerService _apiKeys;

		private string _name = string.Empty;
		public string Name
		{
			get => _name;
			set => SetProperty(ref _name, value);
		}

		private string _value = string.Empty;
		public string Value
		{
			get => _value;
			set => SetProperty(ref _value, value);
		}

		private ApiKeyStorageScheme _scheme = ApiKeyStorageScheme.Encrypted;
		public ApiKeyStorageScheme Scheme
		{
			get => _scheme;
			set => SetProperty(ref _scheme, value);
		}

		private string? _errorMessage;
		public string? ErrorMessage
		{
			get => _errorMessage;
			set => SetProperty(ref _errorMessage, value);
		}

		/// <summary>
		/// If set, the dialog is in edit mode and will update this key instead of creating a new one.
		/// </summary>
		public Guid? EditingKeyId { get; set; }

		/// <summary>
		/// Whether the dialog is in edit mode.
		/// </summary>
		public bool IsEditMode => EditingKeyId != null;

		/// <summary>
		/// The title text for the dialog.
		/// </summary>
		public string TitleText => IsEditMode
			? LocalizationManager.LocalizeStatic("settings.api.edit.title")
			: LocalizationManager.LocalizeStatic("settings.api.add.title");

		/// <summary>
		/// The confirm button text.
		/// </summary>
		public string ConfirmButtonText => IsEditMode
			? LocalizationManager.LocalizeStatic("common.save")
			: LocalizationManager.LocalizeStatic("common.add");

		/// <summary>
		/// The ID of the created/updated key, set after successful operation.
		/// </summary>
		public Guid? CreatedKeyId { get; private set; }

		public ICommand AddCommand { get; }
		public ICommand CancelCommand { get; }

		public AddApiKeyDialogViewModel()
		{
			_apiKeys = ServiceRegistry.Provider.GetRequiredService<IApiKeyManagerService>();

			AddCommand = new RelayCommand(Add);
			CancelCommand = new RelayCommand(Cancel);
		}

		private void Add()
		{
			ErrorMessage = null;

			if (string.IsNullOrWhiteSpace(Name))
			{
				ErrorMessage = LocalizationManager.LocalizeStatic("settings.api.name.required.error");
				return;
			}

			if (string.IsNullOrEmpty(Value))
			{
				ErrorMessage = LocalizationManager.LocalizeStatic("settings.api.value.required.error");
				return;
			}

			try
			{
				if (EditingKeyId != null)
				{
					// Edit mode - update existing key
					_apiKeys.UpdateKey(EditingKeyId.Value, Name.Trim(), Value, Scheme);
					CreatedKeyId = EditingKeyId;
				}
				else
				{
					// Add mode - create new key
					var item = _apiKeys.AddKey(Name.Trim(), Value, Scheme);
					CreatedKeyId = item.Id;
				}

				DialogManager.CloseDialog(true);
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
			}
		}

		private void Cancel()
		{
			DialogManager.CloseDialog(false);
		}
	}
}
