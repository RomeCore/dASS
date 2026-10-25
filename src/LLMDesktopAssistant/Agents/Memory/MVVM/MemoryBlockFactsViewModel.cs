using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Agents.Memory;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;
using LLMDesktopAssistant.Utils;
using ReverseMarkdown;

namespace LLMDesktopAssistant.Agents.Memory.MVVM
{
	/// <summary>
	/// ViewModel for the "Facts" section of an attached memory block. Allows searching,
	/// adding, superseding and deleting semantic facts of the block.
	/// </summary>
	[ViewModelFor(typeof(MemoryBlockFactsView))]
	public class MemoryBlockFactsViewModel : ViewModelBase
	{
		private readonly MemoryBlock _block;
		private readonly IMemoryFactStore _factStore;

		/// <summary>
		/// Gets the facts returned by the last executed search.
		/// </summary>
		public RangeObservableCollection<MemoryFactItemViewModel> SearchResults { get; } = [];

		private string _searchQuery = string.Empty;
		/// <summary>
		/// Gets or sets the fact search query.
		/// </summary>
		public string SearchQuery
		{
			get => _searchQuery;
			set => SetProperty(ref _searchQuery, value);
		}

		/// <summary>
		/// Gets a value indicating whether the last search returned any facts.
		/// </summary>
		public bool HasResults => SearchResults.Count > 0;

		private double _minImportance;
		/// <summary>
		/// Gets or sets the minimum importance score of facts to return in search results,
		/// from 0.0 (any importance) to 1.0 (only the most important).
		/// </summary>
		public double MinImportance
		{
			get => _minImportance;
			set => SetProperty(ref _minImportance, value);
		}

		/// <summary>
		/// Gets the command that searches facts by <see cref="SearchQuery"/>.
		/// </summary>
		public AsyncRelayCommand SearchCommand { get; }

		/// <summary>
		/// Gets the command that opens the "Add Fact" dialog and stores the entered fact.
		/// </summary>
		public AsyncRelayCommand AddFactCommand { get; }

		/// <summary>
		/// Gets the command that permanently deletes all facts of the block after a confirmation.
		/// </summary>
		public AsyncRelayCommand ClearFactsCommand { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="MemoryBlockFactsViewModel"/> class.
		/// </summary>
		/// <param name="block">The memory block whose facts are managed.</param>
		/// <param name="factStore">The fact store used for fact operations.</param>
		public MemoryBlockFactsViewModel(MemoryBlock block, IMemoryFactStore factStore)
		{
			_block = block;
			_factStore = factStore;

			SearchCommand = new AsyncRelayCommand(SearchAsync);
			AddFactCommand = new AsyncRelayCommand(AddFactAsync);
			ClearFactsCommand = new AsyncRelayCommand(ClearFactsAsync);
		}

		private async Task SearchAsync()
		{
			if (string.IsNullOrWhiteSpace(SearchQuery))
				return;

			try
			{
				var results = await _factStore.SearchAsync(_block, SearchQuery, minImportance: MinImportance, maxCount: 20);
				SearchResults.Reset(results.Select(r => new MemoryFactItemViewModel(_block, _factStore, r, RemoveResult)));
				RaisePropertyChanged(nameof(HasResults));
			}
			catch (Exception ex)
			{
				ShowError(LocalizationManager.LocalizeStatic("settings.memory.facts.search.error"), ex.Message);
			}
		}

		private async Task AddFactAsync()
		{
			var vm = new AddFactDialogViewModel
			{
				Title = LocalizationManager.LocalizeStatic("settings.memory.fact.add.title"),
				Placeholder = LocalizationManager.LocalizeStatic("settings.memory.fact.add.placeholder"),
				ImportanceLabel = LocalizationManager.LocalizeStatic("settings.memory.facts.importance.label"),
				SubmitText = LocalizationManager.LocalizeStatic("settings.memory.facts.add.action"),
				CancelText = LocalizationManager.LocalizeStatic("common.cancel")
			};

			_ = DialogManager.ShowDialogAsync(vm);
			var result = await vm.Result;
			if (result == null)
				return;

			try
			{
				var stored = await _factStore.StoreAsync(_block, result.Text, importance: result.Importance);
				ShowSuccess(LocalizationManager.LocalizeStaticFormat("settings.memory.fact.add.success", stored.Id));
			}
			catch (Exception ex)
			{
				ShowError(LocalizationManager.LocalizeStatic("settings.memory.fact.add.error"), ex.Message);
			}
		}

		private async Task ClearFactsAsync()
		{
			var confirm = new ConfirmDialogViewModel
			{
				Title = LocalizationManager.LocalizeStatic("settings.memory.facts.clear.title"),
				Description = LocalizationManager.LocalizeStatic("settings.memory.facts.clear.confirm"),
				ConfirmText = LocalizationManager.LocalizeStatic("settings.memory.facts.clear.action"),
				CancelText = LocalizationManager.LocalizeStatic("common.cancel"),
				IsDanger = true
			};

#pragma warning disable CS8605 // Unboxing is safe: the confirmation dialog always returns bool.
			var confirmed = (bool)await DialogManager.ShowDialogAsync(confirm);
#pragma warning restore CS8605
			if (!confirmed)
				return;

			try
			{
				int count = await _factStore.ClearAsync(_block);
				SearchResults.Clear();
				RaisePropertyChanged(nameof(HasResults));

				ServiceRegistry.Provider.GetRequiredService<IToastService>().ShowSuccess(
					LocalizationManager.LocalizeStatic("settings.memory.facts.clear.success"),
					LocalizationManager.LocalizeStaticFormat("settings.memory.facts.clear.result", count));
			}
			catch (Exception ex)
			{
				ShowError(LocalizationManager.LocalizeStatic("settings.memory.facts.clear.error"), ex.Message);
			}
		}

		private void RemoveResult(MemoryFactItemViewModel item)
		{
			SearchResults.Remove(item);
			RaisePropertyChanged(nameof(HasResults));
		}

		private static void ShowSuccess(string message) =>
			ServiceRegistry.Provider.GetRequiredService<IToastService>().ShowSuccess(message);

		private static void ShowError(string title, string message) =>
			ServiceRegistry.Provider.GetRequiredService<IToastService>().ShowError(title, message);
	}

	/// <summary>
	/// ViewModel for a single fact search result. Provides delete (soft) and supersede operations.
	/// </summary>
	public class MemoryFactItemViewModel : ViewModelBase
	{
		private readonly MemoryBlock _block;
		private readonly IMemoryFactStore _factStore;
		private readonly MemoryFactResult _result;
		private readonly Action<MemoryFactItemViewModel> _removed;

		/// <summary>
		/// Gets the identifier of the fact.
		/// </summary>
		public int Id => _result.Id;

		/// <summary>
		/// Gets the text of the fact.
		/// </summary>
		public string Text => _result.Text;

		/// <summary>
		/// Gets the importance of the fact.
		/// </summary>
		public double Importance => _result.Importance;

		/// <summary>
		/// Gets the formatted creation date of the fact.
		/// </summary>
		public string CreatedAt => _result.CreatedAt.ToLocalTime().ToString("g");

		/// <summary>
		/// Gets the formatted relevance scores of the fact.
		/// </summary>
		public string Scores => string.Format("cos: {0}, bm25: {1}",
			_result.CosineScore?.ToString("0.00") ?? "-",
			_result.Bm25Score?.ToString("0.00") ?? "-");

		/// <summary>
		/// Gets the command that soft-deletes the fact after a confirmation.
		/// </summary>
		public AsyncRelayCommand DeleteCommand { get; }

		/// <summary>
		/// Gets the command that supersedes the fact with a replacement text entered by the user.
		/// </summary>
		public AsyncRelayCommand SupersedeCommand { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="MemoryFactItemViewModel"/> class.
		/// </summary>
		/// <param name="block">The memory block that contains the fact.</param>
		/// <param name="factStore">The fact store used for fact operations.</param>
		/// <param name="result">The fact search result.</param>
		/// <param name="removed">The callback invoked when the fact is removed from the result list.</param>
		public MemoryFactItemViewModel(MemoryBlock block, IMemoryFactStore factStore, MemoryFactResult result, Action<MemoryFactItemViewModel> removed)
		{
			_block = block;
			_factStore = factStore;
			_result = result;
			_removed = removed;

			DeleteCommand = new AsyncRelayCommand(DeleteAsync);
			SupersedeCommand = new AsyncRelayCommand(SupersedeAsync);
		}

		private async Task DeleteAsync()
		{
			var confirm = new ConfirmDialogViewModel
			{
				Title = LocalizationManager.LocalizeStatic("settings.memory.fact.delete.title"),
				Description = LocalizationManager.LocalizeStaticFormat("settings.memory.fact.delete.confirm", Id),
				ConfirmText = LocalizationManager.LocalizeStatic("settings.memory.fact.delete.action"),
				CancelText = LocalizationManager.LocalizeStatic("common.cancel"),
				IsDanger = true
			};

#pragma warning disable CS8605 // Unboxing is safe: the confirmation dialog always returns bool.
			var confirmed = (bool)await DialogManager.ShowDialogAsync(confirm);
#pragma warning restore CS8605
			if (!confirmed)
				return;

			try
			{
				await _factStore.SoftDeleteAsync(_block, Id);
				_removed(this);
				ShowSuccess(LocalizationManager.LocalizeStatic("settings.memory.fact.delete.success"));
			}
			catch (Exception ex)
			{
				ShowError(LocalizationManager.LocalizeStatic("settings.memory.fact.delete.error"), ex.Message);
			}
		}

		private async Task SupersedeAsync()
		{
			var vm = new TextInputDialogViewModel
			{
				Title = LocalizationManager.LocalizeStatic("settings.memory.fact.supersede.title"),
				Description = LocalizationManager.LocalizeStaticFormat("settings.memory.fact.supersede.description", Id),
				Label = LocalizationManager.LocalizeStatic("settings.memory.fact.supersede.label"),
				Value = Text,
				IsMultiline = true,
				IsRequired = true
			};

			var replacement = (string?)await DialogManager.ShowDialogAsync(vm);
			if (string.IsNullOrWhiteSpace(replacement))
				return;

			try
			{
				var stored = await _factStore.SupersedeAsync(_block, Id, replacement, importance: Importance);
				_removed(this);
				ShowSuccess(LocalizationManager.LocalizeStaticFormat("settings.memory.fact.supersede.success", stored.Id));
			}
			catch (Exception ex)
			{
				ShowError(LocalizationManager.LocalizeStatic("settings.memory.fact.supersede.error"), ex.Message);
			}
		}

		private static void ShowSuccess(string message) =>
			ServiceRegistry.Provider.GetRequiredService<IToastService>().ShowSuccess(message);

		private static void ShowError(string title, string message) =>
			ServiceRegistry.Provider.GetRequiredService<IToastService>().ShowError(title, message);
	}
}
