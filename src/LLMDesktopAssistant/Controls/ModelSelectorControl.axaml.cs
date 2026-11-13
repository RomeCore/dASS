using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Providers;
using LLMDesktopAssistant.Services;
using Serilog;

namespace LLMDesktopAssistant.Controls
{
	/// <summary>
	/// Wrapper for a model item in the dropdown list.
	/// </summary>
	public class ModelItemWrapper
	{
		/// <summary>
		/// Gets or sets the full name of the model (e.g. "OpenAI$gpt-4o").
		/// </summary>
		public required string FullName { get; set; }

		/// <summary>
		/// Gets or sets the display text for the model.
		/// </summary>
		public required LocaleKeyBase DisplayText { get; set; }

		/// <summary>
		/// Value indicating whether this model item is invalid or not.
		/// </summary>
		public bool IsInvalid { get; init; } = false;

		public static ModelItemWrapper None { get; } = new ModelItemWrapper
		{
			FullName = string.Empty,
			DisplayText = Locale.GetKey("model.selector.none")
		};
	}

	/// <summary>
	/// Wrapper for a modifier item in the dropdown list.
	/// </summary>
	public class ModifierSelectorItem
	{
		/// <summary>
		/// Gets or sets the name of the modifier, or <see langword="null"/> for the "none" item.
		/// </summary>
		public required string? Name { get; init; }

		/// <summary>
		/// Gets or sets the hint for the modifier, or <see langword="null"/> if no hint is available.
		/// </summary>
		public required string? Hint { get; init; }

		/// <summary>
		/// Gets or sets the display text for the modifier.
		/// </summary>
		public required LocaleKeyBase DisplayText { get; init; }

		/// <summary>
		/// Value indicating whether this model item is invalid or not.
		/// </summary>
		public bool IsInvalid { get; init; } = false;

		/// <summary>
		/// Gets a value indicating whether the hint is visible or not.
		/// </summary>
		public bool IsHintVisible => !IsInvalid && !string.IsNullOrWhiteSpace(Hint);

		public static ModifierSelectorItem None { get; } = new ModifierSelectorItem
		{
			Name = null,
			Hint = null,
			DisplayText = Locale.GetKey("model.selector.modifier.none")
		};
	}

	/// <summary>
	/// Wrapper for a header item in the dropdown list (provider group header).
	/// </summary>
	public class ComboBoxHeaderItem
	{
		public required string Title { get; init; }
	}

	/// <summary>
	/// Wrapper for an empty item in the dropdown list.
	/// </summary>
	public class ComboBoxEmptyItem
	{
		public string Title { get; init; } = LocalizationManager.LocalizeStatic("model.selector.no_models");
	}

	/// <summary>
	/// A control for selecting a model and an optional modifier from available providers.
	/// Works with model full names in format "ProviderName$ModelName" or "ProviderName$ModelName$Modifier".
	/// </summary>
	public partial class ModelSelectorControl : UserControl
	{
		/// <summary>
		/// Defines the <see cref="SelectedModel"/> property.
		/// </summary>
		public static readonly StyledProperty<string> SelectedModelProperty =
			AvaloniaProperty.Register<ModelSelectorControl, string>(
				nameof(SelectedModel));

		/// <summary>
		/// Gets or sets the selected model full name.
		/// </summary>
		public string SelectedModel
		{
			get => GetValue(SelectedModelProperty);
			set => SetValue(SelectedModelProperty, value);
		}

		/// <summary>
		/// Occurs when the selected model changes.
		/// </summary>
		public event Action<string>? SelectedModelChanged;

		private bool _isSyncing;

		static ModelSelectorControl()
		{
			SelectedModelProperty.Changed.AddClassHandler<ModelSelectorControl>(
				(o, e) => o.OnSelectedModelChanged((string)e.NewValue!));
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ModelSelectorControl"/> class.
		/// </summary>
		public ModelSelectorControl()
		{
			InitializeComponent();

			ModelSelector.SelectionChanged += ModelSelector_SelectionChanged;
			ModelSelector.DropDownOpened += (_, _) => Rebuild();
			ModifierSelector.SelectionChanged += ModifierSelector_SelectionChanged;

			Rebuild();
		}

		private void OnSelectedModelChanged(string newValue)
		{
			SelectedModelChanged?.Invoke(newValue);

			if (_isSyncing)
				return;

			ApplyValueToSelectors(newValue);
		}

		private void ModelSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (_isSyncing)
				return;

			UpdateSelectedModel();
		}

		private void ModifierSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (_isSyncing)
				return;

			UpdateSelectedModel();
		}

		private void UpdateSelectedModel()
		{
			var model = ModelSelector.SelectedItem as ModelItemWrapper;
			var modifier = ModifierSelector.SelectedItem as ModifierSelectorItem;

			if (model == null || string.IsNullOrEmpty(model.FullName))
			{
				SelectedModel = string.Empty;
				return;
			}

			SelectedModel = modifier is { Name: not null }
				? $"{model.FullName}${modifier.Name}"
				: model.FullName;
		}

		private void ApplyValueToSelectors(string fullName)
		{
			_isSyncing = true;
			try
			{
				if (ModelReference.TryParse(fullName, out var reference))
				{
					var modelItem = FindModelItem(reference.Provider, reference.ModelId);
					if (modelItem == null)
					{
						modelItem = new ModelItemWrapper
						{
							FullName = $"{reference.Provider}${reference.ModelId}",
							DisplayText = Locale.GetConstKey($"{reference.Provider}${reference.ModelId}"),
							IsInvalid = true
						};
						ModelSelector.Items.Insert(0, modelItem);
					}
					if (ModelSelector.SelectedItem != modelItem)
					{
						ModelSelector.SelectedItem = modelItem;
						RebuildModifiers(reference.Modifiers?.FirstOrDefault());
					}

					var modifierItem = FindModifierItem(reference.Modifiers?.FirstOrDefault());
					if (modifierItem == null && reference.Modifiers?.FirstOrDefault() != null)
					{
						modifierItem = new ModifierSelectorItem
						{
							Name = reference.Modifiers.FirstOrDefault(),
							Hint = null,
							DisplayText = Locale.GetConstKey(reference.Modifiers.FirstOrDefault()!),
							IsInvalid = true
						};
						ModifierSelector.Items.Insert(0, modifierItem);
					}
					if (reference.Modifiers?.FirstOrDefault() == null)
					{
						ModifierSelector.SelectedIndex = 0;
					}
					else if (ModifierSelector.SelectedItem != modifierItem)
					{
						ModifierSelector.SelectedItem = modifierItem;
					}
				}
				else
				{
					ModelSelector.SelectedIndex = 0;
					ModifierSelector.SelectedIndex = 0;
				}
			}
			finally
			{
				_isSyncing = false;
			}
		}

		private ModelItemWrapper? FindModelItem(string provider, string modelId)
		{
			foreach (var item in ModelSelector.Items)
			{
				if (item is ModelItemWrapper wrapper &&
					wrapper.FullName == $"{provider}${modelId}")
				{
					return wrapper;
				}
			}
			return null;
		}

		private ModifierSelectorItem? FindModifierItem(string? name)
		{
			if (name == null)
				return null;

			foreach (var item in ModifierSelector.Items)
			{
				if (item is ModifierSelectorItem modifierItem &&
					modifierItem.Name == name)
				{
					return modifierItem;
				}
			}

			return null;
		}

		private async void SettingsButton_Click(object? sender, RoutedEventArgs e)
		{
			var vm = new ManageModelProvidersDialogViewModel();
			await DialogManager.ShowDialogAsync(vm);
			Rebuild();
		}

		private void Rebuild()
		{
			var prevSelected = SelectedModel;
			ModelReference.TryParse(prevSelected, out var prevReference);
			var prevBaseFullName = string.IsNullOrEmpty(prevSelected)
				? string.Empty
				: $"{prevReference.Provider}${prevReference.ModelId}";

			ModelSelector.Items.Clear();
			ModelSelector.Items.Add(ModelItemWrapper.None);

			try
			{
				var modelManager = ServiceRegistry.Provider.GetRequiredService<IModelManager>();

				string? lastProviderName = null;
				bool found = false;

				foreach (var model in modelManager.ListSelectedModels()
					.OrderBy(m => m.Provider.Name)
					.ThenBy(m => m.Descriptor.Name))
				{
					if (model.Provider.Name != lastProviderName)
					{
						lastProviderName = model.Provider.Name;
						ModelSelector.Items.Add(new ComboBoxHeaderItem
						{
							Title = model.Provider.Name
						});
					}

					var wrapper = new ModelItemWrapper
					{
						FullName = model.FullName,
						DisplayText = Locale.GetConstKey(!string.IsNullOrEmpty(model.Descriptor.DisplayName)
							? model.Descriptor.DisplayName : model.Descriptor.Name)
					};

					ModelSelector.Items.Add(wrapper);

					if (model.FullName == prevBaseFullName)
					{
						ModelSelector.SelectedItem = wrapper;
						found = true;
					}
				}

				if (!found && !string.IsNullOrEmpty(prevBaseFullName))
				{
					ModelSelector.Items.Add(new ModelItemWrapper
					{
						FullName = prevBaseFullName,
						DisplayText = Locale.GetConstKey(prevBaseFullName),
						IsInvalid = true
					});
					ModelSelector.SelectedIndex = ModelSelector.Items.Count - 1;
				}
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to load models for ModelSelectorControl");
				ModelSelector.Items.Add(new ComboBoxEmptyItem
				{
					Title = LocalizationManager.LocalizeStatic("model.selector.load.error")
				});
			}

			if (ModelSelector.SelectedItem == null)
				ModelSelector.SelectedIndex = 0;

			RebuildModifiers(prevReference.Modifiers?.FirstOrDefault());
		}

		private void RebuildModifiers(string? selectedModifierName)
		{
			ModifierSelector.Items.Clear();
			ModifierSelector.Items.Add(ModifierSelectorItem.None);

			ModifierSelectorItem? selectedItem = null;

			var modelManager = ServiceRegistry.Provider.GetRequiredService<IModelManager>();
			var modifiers = modelManager.ListModifiers().ToList();

			foreach (var modifier in modifiers)
			{
				var item = new ModifierSelectorItem
				{
					Name = modifier.Name,
					Hint = modifier.Hint,
					DisplayText = Locale.GetConstKey(modifier.Name)
				};
				ModifierSelector.Items.Add(item);
				if (modifier.Name == selectedModifierName)
					selectedItem = item;
			}

			if (selectedItem == null && !string.IsNullOrEmpty(selectedModifierName))
			{
				selectedItem = new ModifierSelectorItem
				{
					Name = selectedModifierName,
					Hint = null,
					DisplayText = Locale.GetConstKey(selectedModifierName),
					IsInvalid = true
				};
				ModifierSelector.Items.Add(selectedItem);
			}

			ModifierSelector.SelectedItem = selectedItem ?? ModifierSelector.Items[0];
			ModifierSelector.IsVisible = modifiers.Count > 0 || selectedItem != null;
		}
	}
}
