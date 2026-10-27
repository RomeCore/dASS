using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LLMDesktopAssistant.ApiKeys;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Controls
{
	/// <summary>
	/// Wrapper for a selectable API key item in the dropdown.
	/// </summary>
	public class ApiKeyWrapperItemModel
	{
		public static readonly ApiKeyWrapperItemModel Empty = new()
		{
			Item = new ApiKeysConfigurationItem { Name = "None" }
		};

		public required ApiKeysConfigurationItem Item { get; init; }
		public bool IsEmpty => Item.Id == Guid.Empty;
		public Guid ApiKeyId => Item.Id;
	}

	/// <summary>
	/// A control for selecting an API key: ComboBox + Add/Manage buttons.
	/// </summary>
	public partial class ApiKeySelectorControl : UserControl
	{
		public static readonly StyledProperty<Guid> ApiKeyIdProperty =
			AvaloniaProperty.Register<ApiKeySelectorControl, Guid>(nameof(ApiKeyId));

		public static readonly StyledProperty<bool> IsApiKeyValidProperty =
			AvaloniaProperty.Register<ApiKeySelectorControl, bool>(nameof(IsApiKeyValid));

		public Guid ApiKeyId
		{
			get => GetValue(ApiKeyIdProperty);
			set => SetValue(ApiKeyIdProperty, value);
		}

		public bool IsApiKeyValid
		{
			get => GetValue(IsApiKeyValidProperty);
			set => SetValue(IsApiKeyValidProperty, value);
		}

		public event Action<Guid>? ApiKeyIdChanged;
		public event Action<bool>? IsApiKeyValidChanged;

		static ApiKeySelectorControl()
		{
			ApiKeyIdProperty.Changed.AddClassHandler<ApiKeySelectorControl>(
				(o, e) => o.OnApiKeyIdChanged((Guid)e.NewValue!));

			IsApiKeyValidProperty.Changed.AddClassHandler<ApiKeySelectorControl>(
				(o, e) => o.IsApiKeyValidChanged?.Invoke((bool)e.NewValue!));
		}

		public ApiKeySelectorControl()
		{
			InitializeComponent();

			KeyComboBox.SelectionChanged += KeyComboBox_SelectionChanged;
			KeyComboBox.DropDownOpened += (_, _) => Rebuild();

			Rebuild();
		}

		private void OnApiKeyIdChanged(Guid newId)
		{
			IsApiKeyValid = newId != Guid.Empty;

			var item = KeyComboBox.Items
				.OfType<ApiKeyWrapperItemModel>()
				.FirstOrDefault(k => k.ApiKeyId == newId);

			if (item != null)
			{
				KeyComboBox.SelectedItem = item;
			}
			else if (newId == Guid.Empty)
			{
				KeyComboBox.SelectedIndex = 0;
			}
			else
			{
				// Key not in list - add temporarily
				var apiKeys = ServiceRegistry.Provider.GetRequiredService<IApiKeyManagerService>();
				var key = apiKeys.GetKey(newId);
				if (key != null)
				{
					var wrapper = new ApiKeyWrapperItemModel { Item = key };
					KeyComboBox.Items.Insert(1, wrapper);
					KeyComboBox.SelectedItem = wrapper;
				}
			}

			ApiKeyIdChanged?.Invoke(newId);
		}

		private void KeyComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (KeyComboBox.SelectedItem is ApiKeyWrapperItemModel wrapper)
				ApiKeyId = wrapper.ApiKeyId;
		}

		private void AddButton_Click(object? sender, RoutedEventArgs e)
		{
			UIDispatcher.Post(async () =>
			{
				var vm = new AddApiKeyDialogViewModel();
				var result = await DialogManager.ShowDialogAsync(vm);

				if (result is true && vm.CreatedKeyId != null)
				{
					ApiKeyId = vm.CreatedKeyId.Value;
					Rebuild();
				}
			});
		}

		private void ManageButton_Click(object? sender, RoutedEventArgs e)
		{
			UIDispatcher.Post(async () =>
			{
				var vm = new ManageApiKeysDialogViewModel();
				await DialogManager.ShowDialogAsync(vm);
				Rebuild();
			});
		}

		private void Rebuild()
		{
			var apiKeys = ServiceRegistry.Provider.GetRequiredService<IApiKeyManagerService>();
			var allKeys = apiKeys.GetAllKeys().ToList();
			var prevSelected = ApiKeyId;

			ApiKeyWrapperItemModel.Empty.Item.Name =
				LLMDesktopAssistant.Localization.LocalizationManager.LocalizeStatic("settings.api.none");

			KeyComboBox.Items.Clear();
			KeyComboBox.Items.Add(ApiKeyWrapperItemModel.Empty);

			ApiKeyWrapperItemModel? toSelect = null;

			foreach (var key in allKeys.OrderBy(k => k.Name))
			{
				var wrapper = new ApiKeyWrapperItemModel { Item = key };
				KeyComboBox.Items.Add(wrapper);

				if (key.Id == prevSelected)
					toSelect = wrapper;
			}

			KeyComboBox.SelectedItem = toSelect ?? ApiKeyWrapperItemModel.Empty;
		}
	}
}
