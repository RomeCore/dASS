using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Agents.Memory;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Agents.Memory.MVVM
{
	/// <summary>
	/// ViewModel for the "Logs" section of an attached memory block. Allows searching
	/// by text or by time windows (real time and alternative timeline), appending new
	/// logs through a dialog and deleting episodic logs of the block.
	/// </summary>
	[ViewModelFor(typeof(MemoryBlockLogsView))]
	public class MemoryBlockLogsViewModel : ViewModelBase
	{
		private readonly MemoryBlock _block;
		private readonly IMemoryLogStore _logStore;

		/// <summary>
		/// Gets the logs returned by the last executed search.
		/// </summary>
		public RangeObservableCollection<MemoryLogItemViewModel> SearchResults { get; } = [];

		private string _searchQuery = string.Empty;
		/// <summary>
		/// Gets or sets the log search query.
		/// </summary>
		public string SearchQuery
		{
			get => _searchQuery;
			set => SetProperty(ref _searchQuery, value);
		}

		private bool _isTimeSearch;
		/// <summary>
		/// Gets or sets a value indicating whether the search uses time windows
		/// instead of a text query.
		/// </summary>
		public bool IsTimeSearch
		{
			get => _isTimeSearch;
			set => SetProperty(ref _isTimeSearch, value);
		}

		// Real-time search window.

		private DateTimeOffset? _fromDate;
		/// <summary>
		/// Gets or sets the date part of the inclusive lower bound of the real-time window.
		/// </summary>
		public DateTimeOffset? FromDate
		{
			get => _fromDate;
			set => SetProperty(ref _fromDate, value);
		}

		private TimeSpan? _fromTime;
		/// <summary>
		/// Gets or sets the time part of the inclusive lower bound of the real-time window.
		/// </summary>
		public TimeSpan? FromTime
		{
			get => _fromTime;
			set => SetProperty(ref _fromTime, value);
		}

		private DateTimeOffset? _toDate;
		/// <summary>
		/// Gets or sets the date part of the inclusive upper bound of the real-time window.
		/// </summary>
		public DateTimeOffset? ToDate
		{
			get => _toDate;
			set => SetProperty(ref _toDate, value);
		}

		private TimeSpan? _toTime;
		/// <summary>
		/// Gets or sets the time part of the inclusive upper bound of the real-time window.
		/// </summary>
		public TimeSpan? ToTime
		{
			get => _toTime;
			set => SetProperty(ref _toTime, value);
		}

		// Alternative timeline search window.

		private decimal? _timeLineFrom;
		/// <summary>
		/// Gets or sets the inclusive lower bound of the alternative time ordinal window.
		/// </summary>
		public decimal? TimeLineFrom
		{
			get => _timeLineFrom;
			set => SetProperty(ref _timeLineFrom, value);
		}

		private decimal? _timeLineTo;
		/// <summary>
		/// Gets or sets the inclusive upper bound of the alternative time ordinal window.
		/// </summary>
		public decimal? TimeLineTo
		{
			get => _timeLineTo;
			set => SetProperty(ref _timeLineTo, value);
		}

		/// <summary>
		/// Gets a value indicating whether the last search returned any logs.
		/// </summary>
		public bool HasResults => SearchResults.Count > 0;

		private double _minImportance;
		/// <summary>
		/// Gets or sets the minimum importance score of logs to return in search results,
		/// from 0.0 (any importance) to 1.0 (only the most important).
		/// </summary>
		public double MinImportance
		{
			get => _minImportance;
			set => SetProperty(ref _minImportance, value);
		}

		/// <summary>
		/// Gets the command that searches logs by <see cref="SearchQuery"/> or by the time windows.
		/// </summary>
		public AsyncRelayCommand SearchCommand { get; }

		/// <summary>
		/// Gets the command that opens the "Add Log" dialog and appends the entered log.
		/// </summary>
		public AsyncRelayCommand AddLogCommand { get; }

		/// <summary>
		/// Gets the command that permanently deletes all logs of the block after a confirmation.
		/// </summary>
		public AsyncRelayCommand ClearLogsCommand { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="MemoryBlockLogsViewModel"/> class.
		/// </summary>
		/// <param name="block">The memory block whose logs are managed.</param>
		/// <param name="logStore">The log store used for log operations.</param>
		public MemoryBlockLogsViewModel(MemoryBlock block, IMemoryLogStore logStore)
		{
			_block = block;
			_logStore = logStore;

			SearchCommand = new AsyncRelayCommand(SearchAsync);
			AddLogCommand = new AsyncRelayCommand(AddLogAsync);
			ClearLogsCommand = new AsyncRelayCommand(ClearLogsAsync);
		}

		private async Task SearchAsync()
		{
			if (IsTimeSearch)
			{
				await SearchByTimeAsync();
				return;
			}

			if (string.IsNullOrWhiteSpace(SearchQuery))
				return;

			try
			{
				var results = await _logStore.SearchAsync(_block, SearchQuery, minImportance: MinImportance, maxCount: 20);
				SearchResults.Reset(results.Select(l => new MemoryLogItemViewModel(_block, _logStore, l, RemoveResult)));
				RaisePropertyChanged(nameof(HasResults));
			}
			catch (Exception ex)
			{
				ShowError(LocalizationManager.LocalizeStatic("settings.memory.logs.search.error"), ex.Message);
			}
		}

		private async Task SearchByTimeAsync()
		{
			try
			{
				var results = await _logStore.GetByTimeAsync(
					_block,
					from: Combine(FromDate, FromTime),
					to: Combine(ToDate, ToTime),
					timeLineFrom: (double?)TimeLineFrom,
					timeLineTo: (double?)TimeLineTo,
					minImportance: MinImportance,
					maxCount: 100);

				SearchResults.Reset(results.Select(l => new MemoryLogItemViewModel(_block, _logStore, l, RemoveResult)));
				RaisePropertyChanged(nameof(HasResults));
			}
			catch (Exception ex)
			{
				ShowError(LocalizationManager.LocalizeStatic("settings.memory.logs.time_search.error"), ex.Message);
			}
		}

		private async Task AddLogAsync()
		{
			var vm = new AddLogDialogViewModel
			{
				Title = LocalizationManager.LocalizeStatic("settings.memory.log.add.title"),
				Placeholder = LocalizationManager.LocalizeStatic("settings.memory.log.add.placeholder"),
				ImportanceLabel = LocalizationManager.LocalizeStatic("settings.memory.logs.importance.label"),
				BeginLabel = LocalizationManager.LocalizeStatic("settings.memory.log.begin.label"),
				EndLabel = LocalizationManager.LocalizeStatic("settings.memory.log.end.label"),
				TimelineLabel = LocalizationManager.LocalizeStatic("settings.memory.log.timeline.label"),
				OrdinalLabel = LocalizationManager.LocalizeStatic("settings.memory.log.ordinal.label"),
				DetailsLabel = LocalizationManager.LocalizeStatic("settings.memory.log.details.label"),
				SubmitText = LocalizationManager.LocalizeStatic("settings.memory.logs.add.action"),
				CancelText = LocalizationManager.LocalizeStatic("common.cancel")
			};

			_ = DialogManager.ShowDialogAsync(vm);
			var result = await vm.Result;
			if (result == null)
				return;

			try
			{
				var appended = await _logStore.AppendAsync(
					_block,
					result.Text,
					importance: result.Importance,
					timeStampBegin: result.TimeStampBegin,
					timeStampEnd: result.TimeStampEnd,
					timeLineOrdinalBegin: result.TimeLineOrdinalBegin,
					timeLineDetailsBegin: result.TimeLineDetailsBegin,
					timeLineOrdinalEnd: result.TimeLineOrdinalEnd,
					timeLineDetailsEnd: result.TimeLineDetailsEnd);

				ShowSuccess(LocalizationManager.LocalizeStaticFormat("settings.memory.log.add.success", appended.Id));
			}
			catch (Exception ex)
			{
				ShowError(LocalizationManager.LocalizeStatic("settings.memory.log.add.error"), ex.Message);
			}
		}

		private async Task ClearLogsAsync()
		{
			var confirm = new ConfirmDialogViewModel
			{
				Title = LocalizationManager.LocalizeStatic("settings.memory.logs.clear.title"),
				Description = LocalizationManager.LocalizeStatic("settings.memory.logs.clear.confirm"),
				ConfirmText = LocalizationManager.LocalizeStatic("settings.memory.logs.clear.action"),
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
				int count = await _logStore.ClearAsync(_block);
				SearchResults.Clear();
				RaisePropertyChanged(nameof(HasResults));

				ServiceRegistry.Provider.GetRequiredService<IToastService>().ShowSuccess(
					LocalizationManager.LocalizeStatic("settings.memory.logs.clear.success"),
					LocalizationManager.LocalizeStaticFormat("settings.memory.logs.clear.result", count));
			}
			catch (Exception ex)
			{
				ShowError(LocalizationManager.LocalizeStatic("settings.memory.logs.clear.error"), ex.Message);
			}
		}

		private void RemoveResult(MemoryLogItemViewModel item)
		{
			SearchResults.Remove(item);
			RaisePropertyChanged(nameof(HasResults));
		}

		private static DateTime? Combine(DateTimeOffset? date, TimeSpan? time)
		{
			if (date == null && time == null)
				return null;

			var day = date?.Date ?? DateTime.Today;
			return day + (time ?? TimeSpan.Zero);
		}

		private static void ShowSuccess(string message) =>
			ServiceRegistry.Provider.GetRequiredService<IToastService>().ShowSuccess(message);

		private static void ShowError(string title, string message) =>
			ServiceRegistry.Provider.GetRequiredService<IToastService>().ShowError(title, message);
	}

	/// <summary>
	/// ViewModel for a single log search result. Provides a delete (hard) operation.
	/// </summary>
	public class MemoryLogItemViewModel : ViewModelBase
	{
		private readonly MemoryBlock _block;
		private readonly IMemoryLogStore _logStore;
		private readonly MemoryLogResult _result;
		private readonly Action<MemoryLogItemViewModel> _removed;

		/// <summary>
		/// Gets the identifier of the log.
		/// </summary>
		public int Id => _result.Id;

		/// <summary>
		/// Gets the text of the log.
		/// </summary>
		public string Text => _result.Text;

		/// <summary>
		/// Gets the importance of the log.
		/// </summary>
		public double Importance => _result.Importance;

		/// <summary>
		/// Gets the formatted begin timestamp of the log.
		/// </summary>
		public string TimeStampBegin => _result.TimeStampBegin.ToLocalTime().ToString("g");

		/// <summary>
		/// Gets the formatted alternative timeline of the log, when set.
		/// </summary>
		public string Timeline
		{
			get
			{
				if (_result.TimeLineOrdinalBegin == 0 && string.IsNullOrEmpty(_result.TimeLineDetailsBegin))
					return string.Empty;

				var details = string.IsNullOrEmpty(_result.TimeLineDetailsBegin)
					? _result.TimeLineOrdinalBegin.ToString()
					: $"{_result.TimeLineOrdinalBegin} · {_result.TimeLineDetailsBegin}";

				if (_result.TimeLineOrdinalEnd != _result.TimeLineOrdinalBegin || _result.TimeLineDetailsEnd != _result.TimeLineDetailsBegin)
					return $"{details} → {_result.TimeLineOrdinalEnd} · {_result.TimeLineDetailsEnd}";

				return details;
			}
		}

		/// <summary>
		/// Gets the formatted BM25 relevance score of the log.
		/// </summary>
		public string Bm25Score => _result.Bm25Score?.ToString("0.00") ?? "-";

		/// <summary>
		/// Gets the command that hard-deletes the log after a confirmation.
		/// </summary>
		public AsyncRelayCommand DeleteCommand { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="MemoryLogItemViewModel"/> class.
		/// </summary>
		/// <param name="block">The memory block that contains the log.</param>
		/// <param name="logStore">The log store used for log operations.</param>
		/// <param name="result">The log search result.</param>
		/// <param name="removed">The callback invoked when the log is removed from the result list.</param>
		public MemoryLogItemViewModel(MemoryBlock block, IMemoryLogStore logStore, MemoryLogResult result, Action<MemoryLogItemViewModel> removed)
		{
			_block = block;
			_logStore = logStore;
			_result = result;
			_removed = removed;

			DeleteCommand = new AsyncRelayCommand(DeleteAsync);
		}

		private async Task DeleteAsync()
		{
			var confirm = new ConfirmDialogViewModel
			{
				Title = LocalizationManager.LocalizeStatic("settings.memory.log.delete.title"),
				Description = LocalizationManager.LocalizeStaticFormat("settings.memory.log.delete.confirm", Id),
				ConfirmText = LocalizationManager.LocalizeStatic("settings.memory.log.delete.action"),
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
				await _logStore.HardDeleteAsync(_block, Id);
				_removed(this);
				ShowSuccess(LocalizationManager.LocalizeStatic("settings.memory.log.delete.success"));
			}
			catch (Exception ex)
			{
				ShowError(LocalizationManager.LocalizeStatic("settings.memory.log.delete.error"), ex.Message);
			}
		}

		private static void ShowSuccess(string message) =>
			ServiceRegistry.Provider.GetRequiredService<IToastService>().ShowSuccess(message);

		private static void ShowError(string title, string message) =>
			ServiceRegistry.Provider.GetRequiredService<IToastService>().ShowError(title, message);
	}
}
