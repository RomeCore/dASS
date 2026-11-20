using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.ApiKeys
{
	/// <summary>
	/// ViewModel for the "Manage API Keys" dialog.
	/// </summary>
	[ViewModelFor(typeof(ManageApiKeysDialogView))]
	public class ManageApiKeysDialogViewModel : ViewModelBase
	{
		private readonly IApiKeyManagerService _apiKeys;

		public RangeObservableCollection<ApiKeyDisplayItem> Keys { get; } = [];

		private ApiKeyDisplayItem? _selectedKey;
		public ApiKeyDisplayItem? SelectedKey
		{
			get => _selectedKey;
			set
			{
				if (SetProperty(ref _selectedKey, value))
				{
					NotifyCanExecuteChanged();
					RaisePropertyChanged(nameof(IsKeySelected));
				}
			}
		}

		public bool IsKeySelected => SelectedKey != null;

		public IRelayCommand AddCommand { get; }
		public IRelayCommand DeleteCommand { get; }
		public IRelayCommand EditCommand { get; }
		public IRelayCommand CloseCommand { get; }

		public ManageApiKeysDialogViewModel()
		{
			_apiKeys = ServiceRegistry.Provider.GetRequiredService<IApiKeyManagerService>();

			AddCommand = new RelayCommand(Add);
			DeleteCommand = new RelayCommand(Delete, () => IsKeySelected);
			EditCommand = new RelayCommand(Edit, () => IsKeySelected);
			CloseCommand = new RelayCommand(Close);

			ReloadKeys();
		}

		private void NotifyCanExecuteChanged()
		{
			DeleteCommand.NotifyCanExecuteChanged();
			EditCommand.NotifyCanExecuteChanged();
		}

		private void ReloadKeys()
		{
			Keys.Clear();
			foreach (var key in _apiKeys.GetAllKeys().OrderBy(k => k.Name))
				Keys.Add(new ApiKeyDisplayItem(key));
		}

		private async void Add()
		{
			var vm = new AddApiKeyDialogViewModel();
			var result = await DialogManager.ShowDialogAsync(vm);

			if (result is true)
				ReloadKeys();
		}

		private void Delete()
		{
			if (SelectedKey == null)
				return;

			_apiKeys.RemoveKey(SelectedKey.Id);
			ReloadKeys();
			SelectedKey = null;
		}

		private async void Edit()
		{
			if (SelectedKey == null)
				return;

			var editVm = new AddApiKeyDialogViewModel
			{
				EditingKeyId = SelectedKey.Id,
				Name = SelectedKey.Name,
				Value = GetEditableValue(SelectedKey),
				Scheme = SelectedKey.StorageScheme
			};

			var result = await DialogManager.ShowDialogAsync(editVm);

			if (result is true)
				ReloadKeys();
		}

		/// <summary>
		/// Returns the value that should be pre-filled into the edit dialog.
		/// For environment-variable keys this is the env var NAME, because resolving it
		/// would replace the stored name with the actual secret on save.
		/// For encrypted/raw keys the actual value is resolved so it can be viewed and edited.
		/// </summary>
		private string GetEditableValue(ApiKeyDisplayItem key)
		{
			if (key.StorageScheme == ApiKeyStorageScheme.EnvironmentVariable)
				return key.Source.StoredValue ?? string.Empty;

			return _apiKeys.ResolveKey(key.Id) ?? string.Empty;
		}

		private void Close()
		{
			DialogManager.CloseDialog(null);
		}
	}

	/// <summary>
	/// Wrapper around <see cref="ApiKeysConfigurationItem"/> for display in the manager,
	/// providing localized scheme text.
	/// </summary>
	public class ApiKeyDisplayItem
	{
		public Guid Id => Source.Id;
		public string Name => Source.Name;
		public ApiKeyStorageScheme StorageScheme => Source.StorageScheme;
		public string SchemeText => StorageScheme switch
		{
			ApiKeyStorageScheme.Encrypted => LocalizationManager.LocalizeStatic("settings.api.scheme.encrypted"),
			ApiKeyStorageScheme.Raw => LocalizationManager.LocalizeStatic("settings.api.scheme.raw"),
			ApiKeyStorageScheme.EnvironmentVariable => LocalizationManager.LocalizeStatic("settings.api.scheme.env"),
			_ => StorageScheme.ToString()
		};

		public ApiKeysConfigurationItem Source { get; }

		public ApiKeyDisplayItem(ApiKeysConfigurationItem source)
		{
			Source = source;
		}
	}
}
