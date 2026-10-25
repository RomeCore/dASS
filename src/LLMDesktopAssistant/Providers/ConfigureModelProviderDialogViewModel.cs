using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.ApiKeys;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Providers
{
	/// <summary>
	/// Represents a model with a selection checkbox for UI display.
	/// </summary>
	public class ModelCheckboxItem : NotifyPropertyChanged
	{
		private bool _isSelected;
		/// <summary>
		/// Gets or sets whether this model is selected.
		/// </summary>
		public bool IsSelected
		{
			get => _isSelected;
			set => SetProperty(ref _isSelected, value);
		}

		/// <summary>
		/// The model descriptor.
		/// </summary>
		public required ModelDescriptor Model { get; init; }

		/// <summary>
		/// Gets the display name of the model.
		/// </summary>
		public string DisplayName => !string.IsNullOrEmpty(Model.DisplayName) ? Model.DisplayName : Model.Name;

		/// <summary>
		/// Gets the context size as a human-readable string.
		/// </summary>
		public string ContextText => Model.ContextSize > 0 ? $"{Model.ContextSize:N0}" : "-";

		/// <summary>
		/// Gets the input modality icons with tooltips.
		/// </summary>
		public List<ModelModalityFlagInfo> InputModalityFlags => ModelModalityFlagInfo.FromModalities(Model.InputModalities);

		/// <summary>
		/// Gets the output modality icons with tooltips.
		/// </summary>
		public List<ModelModalityFlagInfo> OutputModalityFlags => ModelModalityFlagInfo.FromModalities(Model.OutputModalities);

		/// <summary>
		/// Gets the capability icons with tooltips.
		/// </summary>
		public List<ModelCapabilityFlagInfo> CapabilityFlags => ModelCapabilityFlagInfo.FromCapabilities(Model.Capabilities);
	}

	/// <summary>
	/// ViewModel for adding or editing a model provider.
	/// </summary>
	[ViewModelFor(typeof(ConfigureModelProviderDialogView))]
	public class ConfigureModelProviderDialogViewModel : ViewModelBase
	{
		private readonly IApiKeyManagerService _apiKeys;
		private readonly IModelManager _modelManager;
		private readonly List<ModelProviderType> _providerTypes;

		/// <summary>
		/// Gets the list of available provider type IDs.
		/// </summary>
		public List<ProviderTypeItem> ProviderTypes { get; }

		/// <summary>
		/// If set, we are editing an existing provider.
		/// </summary>
		public ModelProviderConfiguration EditingProvider { get; }

		private ProviderTypeItem? _selectedProviderType;
		/// <summary>
		/// Gets or sets the selected provider type.
		/// </summary>
		public ProviderTypeItem? SelectedProviderType
		{
			get => _selectedProviderType;
			set
			{
				var prevProvider = _selectedProviderType;
				if (SetProperty(ref _selectedProviderType, value))
				{
					EditingProvider.Type = _selectedProviderType?.Id ?? string.Empty;
					if (_selectedProviderType != null)
					{
						var prevProviderImpl = prevProvider != null ? _providerTypes.FirstOrDefault(t => t.Id == prevProvider?.Id) : null;
						var providerTypeImpl = _providerTypes.FirstOrDefault(t => t.Id == _selectedProviderType.Id);
						if (providerTypeImpl != null)
						{
							var prevDefaultEndpoint = prevProviderImpl?.DefaultEndpoint;
							var defaultEndpoint = providerTypeImpl.DefaultEndpoint;
							if (string.IsNullOrEmpty(EditingProvider.EndpointUri) || prevDefaultEndpoint == EditingProvider.EndpointUri)
								EditingProvider.EndpointUri = defaultEndpoint;
						}
					}
				}
			}
		}

		private bool _isTestingConnection;
		/// <summary>
		/// Gets or sets whether a connection test is in progress.
		/// </summary>
		public bool IsTestingConnection
		{
			get => _isTestingConnection;
			set => SetProperty(ref _isTestingConnection, value);
		}

		/// <summary>
		/// Gets or sets the connection test result message.
		/// </summary>
		public string? ConnectionTestResult { get; set; }

		/// <summary>
		/// Gets the list of available models with selection checkboxes.
		/// </summary>
		public RangeObservableCollection<ModelCheckboxItem> AvailableModels { get; } = [];

		private bool _isLoadingModels;
		/// <summary>
		/// Gets or sets whether models are being loaded.
		/// </summary>
		public bool IsLoadingModels
		{
			get => _isLoadingModels;
			set => SetProperty(ref _isLoadingModels, value);
		}

		/// <summary>
		/// Gets the collection of custom models added by the user.
		/// These models are always available in the model selector and are not
		/// part of the default set of models offered by the provider.
		/// </summary>
		public RangeObservableCollection<ModelDescriptor> CustomModels => EditingProvider.CustomModels;

		private string _customModelName = string.Empty;
		/// <summary>
		/// Gets or sets the name (ID) of the custom model being added.
		/// </summary>
		public string CustomModelName
		{
			get => _customModelName;
			set
			{
				if (SetProperty(ref _customModelName, value))
				{
					CustomModelError = null;
				}
			}
		}

		private string _customModelDisplayName = string.Empty;
		/// <summary>
		/// Gets or sets the optional display name of the custom model being added.
		/// </summary>
		public string CustomModelDisplayName
		{
			get => _customModelDisplayName;
			set => SetProperty(ref _customModelDisplayName, value);
		}

		private string? _customModelError;
		/// <summary>
		/// Gets or sets the validation error message for the custom model form.
		/// </summary>
		public string? CustomModelError
		{
			get => _customModelError;
			set => SetProperty(ref _customModelError, value);
		}

		/// <summary>
		/// Gets whether the provider has any custom models.
		/// </summary>
		public bool HasCustomModels => CustomModels.Count > 0;

		public IRelayCommand LoadModelsCommand { get; }
		public IRelayCommand SelectAllCommand { get; }
		public IRelayCommand DeselectAllCommand { get; }
		public IRelayCommand TestConnectionCommand { get; }
		public IRelayCommand AddCustomModelCommand { get; }
		public IRelayCommand<ModelDescriptor> RemoveCustomModelCommand { get; }
		public IRelayCommand CloseCommand { get; }

		public ConfigureModelProviderDialogViewModel(ModelProviderConfiguration editingProvider)
		{
			_apiKeys = ServiceRegistry.Provider.GetRequiredService<IApiKeyManagerService>();
			_modelManager = ServiceRegistry.Provider.GetRequiredService<IModelManager>();
			EditingProvider = editingProvider;

			_providerTypes = ServiceRegistry.Provider.GetServices<ModelProviderType>().ToList();
			ProviderTypes = _providerTypes.Select(t => new ProviderTypeItem
			{
				Id = t.Id,
				DisplayName = t.DisplayName,
				IsApiKeyRequired = t.IsApiKeyRequired(new ModelProviderConfiguration())
			}).ToList();

			LoadModelsCommand = new AsyncRelayCommand(LoadModelsAsync);
			SelectAllCommand = new RelayCommand(() => SetAllModelsSelected(true));
			DeselectAllCommand = new RelayCommand(() => SetAllModelsSelected(false));
			TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
			AddCustomModelCommand = new RelayCommand(AddCustomModel);
			RemoveCustomModelCommand = new RelayCommand<ModelDescriptor>(RemoveCustomModel);
			CloseCommand = new RelayCommand(Close);

			CustomModels.CollectionChanged += OnCustomModelsChanged;

			SelectedProviderType = ProviderTypes.FirstOrDefault(p => p.Id == editingProvider.Type) ?? ProviderTypes.First();
			RefreshModelList();
		}

		private void OnCustomModelsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			RaisePropertyChanged(nameof(HasCustomModels));
		}

		private void AddCustomModel()
		{
			var name = CustomModelName.Trim();
			if (string.IsNullOrEmpty(name))
			{
				CustomModelError = LocalizationManager.LocalizeStatic("settings.provider.custom.name.required.error");
				return;
			}
			if (name.Contains('$'))
			{
				CustomModelError = LocalizationManager.LocalizeStatic("settings.provider.custom.name.invalid.error");
				return;
			}
			if (CustomModels.Any(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)))
			{
				CustomModelError = LocalizationManager.LocalizeStatic("settings.provider.custom.name.duplicate.error");
				return;
			}

			var displayName = CustomModelDisplayName.Trim();
			CustomModels.Add(new ModelDescriptor
			{
				Name = name,
				DisplayName = displayName
			});

			CustomModelName = string.Empty;
			CustomModelDisplayName = string.Empty;
			CustomModelError = null;
		}

		private void RemoveCustomModel(ModelDescriptor? model)
		{
			if (model == null)
				return;
			CustomModels.Remove(model);
		}

		private void SetAllModelsSelected(bool selected)
		{
			foreach (var item in AvailableModels)
				item.IsSelected = selected;
		}

		private void RefreshModelList()
		{
			AvailableModels.Clear();
			var selectedModels = EditingProvider.SelectedModelNames.ToHashSet();
			foreach (var model in EditingProvider.Models.OrderBy(m => m.DisplayName ?? m.Name))
			{
				AvailableModels.Add(new ModelCheckboxItem
				{
					Model = model,
					IsSelected = selectedModels.Contains(model.Name)
				});
			}
		}

		private async Task LoadModelsAsync()
		{
			IsLoadingModels = true;
			try
			{
				var result = await _modelManager.CheckConnectionAsync(EditingProvider);
				if (result)
				{
					await _modelManager.RefreshModelsAsync(EditingProvider);
					RefreshModelList();
					ConnectionTestResult = LocalizationManager.LocalizeStatic("settings.provider.connection.success");
				}
				else
				{
					ConnectionTestResult = LocalizationManager.LocalizeStatic("settings.provider.connection.failure");
				}
			}
			catch (Exception ex)
			{
				ConnectionTestResult = LocalizationManager.LocalizeStaticFormat("settings.provider.connection.error", ex.Message);
			}
			finally
			{
				IsLoadingModels = false;
				RaisePropertyChanged(nameof(ConnectionTestResult));
			}
		}

		private async Task TestConnectionAsync()
		{
			IsTestingConnection = true;
			ConnectionTestResult = null;
			RaisePropertyChanged(nameof(ConnectionTestResult));

			try
			{
				var result = await _modelManager.CheckConnectionAsync(EditingProvider);
				ConnectionTestResult = result
					? LocalizationManager.LocalizeStatic("settings.provider.connection.success")
					: LocalizationManager.LocalizeStatic("settings.provider.connection.failure");
			}
			catch (Exception ex)
			{
				ConnectionTestResult = LocalizationManager.LocalizeStaticFormat("settings.provider.connection.error", ex.Message);
			}
			finally
			{
				IsTestingConnection = false;
				RaisePropertyChanged(nameof(ConnectionTestResult));
			}
		}

		private void Close()
		{
			// Save selected model names
			EditingProvider.SelectedModelNames = [.. AvailableModels.Where(m => m.IsSelected).Select(m => m.Model.Name)];

			var providersConfig = LLMDesktopAssistant.Settings.SettingsManager.Get<ModelProvidersConfiguration>();
			if (!providersConfig.ModelProviders.Contains(EditingProvider))
				providersConfig.ModelProviders.Add(EditingProvider);

			DialogManager.CloseDialog();
		}
	}

	/// <summary>
	/// Represents a provider type item in the selection dropdown.
	/// </summary>
	public class ProviderTypeItem
	{
		public required string Id { get; init; }
		public required string DisplayName { get; init; }
		public bool? IsApiKeyRequired { get; init; }
	}
}
