using CommunityToolkit.Mvvm.Input;
using LiteDB;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Utils;
using LLMDesktopAssistant.Utils.Files;

namespace LLMDesktopAssistant.Tools.MVVM.Diff
{
	/// <summary>
	/// ViewModel for displaying a diff with optional confirmation controls.
	/// Supports two modes: read-only (just display) and confirmation (with checkboxes and accept/decline buttons).
	/// BSON-serializable for persistence in LiteDB when <see cref="AdditionalChatData.IsTemporary"/> is <see langword="false"/>.
	/// Stores the diff as a list of <see cref="HunkGroup"/> chunks (compatible with <see cref="UnifiedDiff.Compute"/>).
	/// </summary>
	[ViewModelFor(typeof(TextDiffAdditionalView))]
	public class TextDiffAdditionalViewModel : AdditionalChatData
	{
		private string _title = string.Empty;
		/// <summary>
		/// Gets or sets the title displayed above the diff (e.g., file path or description).
		/// </summary>
		public string Title
		{
			get => _title;
			set => SetProperty(ref _title, value);
		}

		private string _description = string.Empty;
		/// <summary>
		/// Gets or sets the description text shown below the title.
		/// </summary>
		public string Description
		{
			get => _description;
			set => SetProperty(ref _description, value);
		}

		private bool _isReadOnly = true;
		/// <summary>
		/// Gets or sets whether this diff is in read-only mode.
		/// In read-only mode, no checkboxes or action buttons are shown.
		/// In confirmation mode, users can toggle chunks and accept/decline changes.
		/// </summary>
		public bool IsReadOnly
		{
			get => _isReadOnly;
			set
			{
				if (SetProperty(ref _isReadOnly, value))
					RaisePropertyChanged(nameof(AreCheckboxesInteractive));
			}
		}

		private readonly RangeObservableCollection<HunkGroupViewModel> _chunkViewModels = [];
		/// <summary>
		/// Gets the observable collection of chunk view models used for UI binding.
		/// Each wrapper provides an <see cref="HunkGroupViewModel.IsEnabled"/> property
		/// for toggling individual chunks on/off.
		/// </summary>
		public RangeObservableCollection<HunkGroupViewModel> ChunkViewModels
		{
			get => _chunkViewModels;
			set => _chunkViewModels.Reset(value);
		}

		private bool? _isConfirmed;
		/// <summary>
		/// Gets or sets the confirmation state.
		/// <see langword="null"/> while pending, <see langword="true"/> when accepted, <see langword="false"/> when declined.
		/// </summary>
		public bool? IsConfirmed
		{
			get => _isConfirmed;
			set
			{
				if (SetProperty(ref _isConfirmed, value))
				{
					RaisePropertyChanged(nameof(IsPending));
					RaisePropertyChanged(nameof(IsAccepted));
					RaisePropertyChanged(nameof(IsDeclined));
					RaisePropertyChanged(nameof(AreCheckboxesInteractive));
				}
			}
		}

		private string? _notes;
		public string? Notes
		{
			get => _notes;
			set => SetProperty(ref _notes, value);
		}

		/// <summary>
		/// Gets whether the confirmation is still pending.
		/// </summary>
		[BsonIgnore]
		public bool IsPending => _isConfirmed == null;

		/// <summary>
		/// Gets whether the checkboxes are interactive.
		/// In read-only mode or after confirmation/decline, checkboxes are disabled
		/// to prevent changing already-applied decisions.
		/// </summary>
		[BsonIgnore]
		public bool AreCheckboxesInteractive => !IsReadOnly && IsPending;

		/// <summary>
		/// Gets whether the changes were accepted.
		/// </summary>
		[BsonIgnore]
		public bool IsAccepted => _isConfirmed == true;

		/// <summary>
		/// Gets whether the changes were declined.
		/// </summary>
		[BsonIgnore]
		public bool IsDeclined => _isConfirmed == false;

		/// <summary>
		/// Gets the total number of removed lines across all chunks.
		/// Uses <see cref="ChunkViewModels"/> if available, otherwise falls back to <see cref="Chunks"/>.
		/// </summary>
		[BsonIgnore]
		public int TotalRemoved
		{
			get
			{
				int count = 0;
				foreach (var chunk in ChunkViewModels)
					if (chunk.IsEnabled)
						foreach (var line in chunk.Group.Lines)
							if (line.Kind == '-') count++;
				return count;
			}
		}

		/// <summary>
		/// Gets the total number of added lines across all chunks.
		/// Uses <see cref="ChunkViewModels"/> if available, otherwise falls back to <see cref="Chunks"/>.
		/// </summary>
		[BsonIgnore]
		public int TotalAdded
		{
			get
			{
				int count = 0;
				foreach (var chunk in ChunkViewModels)
					if (chunk.IsEnabled)
						foreach (var line in chunk.Group.Lines)
							if (line.Kind == '+') count++;
				return count;
			}
		}

		/// <summary>
		/// Gets the total number of chunks that have changes.
		/// Uses <see cref="ChunkViewModels"/> if available, otherwise falls back to <see cref="Chunks"/>.
		/// </summary>
		[BsonIgnore]
		public int ChangedChunksCount
		{
			get
			{
				int count = 0;
				foreach (var chunk in ChunkViewModels.Select(vm => vm.Group))
				{
					bool hasChanges = false;
					foreach (var line in chunk.Lines)
					{
						if (line.Kind is '+' or '-')
						{
							hasChanges = true;
							break;
						}
					}
					if (hasChanges) count++;
				}
				return count;
			}
		}

		private TaskCompletionSource<ToolApprovalResult>? _consentTcs;
		private ToolApprovalResult? _resolvedConsent;
		/// <summary>
		/// Gets the consent task that completes when the user accepts or declines the changes. The full
		/// result travels so the confirmation stage is the single owner of memorization.
		/// </summary>
		[BsonIgnore]
		[ChangeTracker.Untracked]
		public Task<ToolApprovalResult> ConsentResultTask
		{
			get
			{
				if (_consentTcs == null)
				{
					_consentTcs = new TaskCompletionSource<ToolApprovalResult>(TaskCreationOptions.RunContinuationsAsynchronously);
					var resolved = _resolvedConsent ??
						(_isConfirmed is { } confirmed ? CreateApproval(confirmed, _notes) : null);
					if (resolved is not null)
						_consentTcs.TrySetResult(resolved);
				}
				return _consentTcs.Task;
			}
		}

		/// <summary>
		/// Gets the command that resolves the confirmation with a <see cref="ToolApprovalResult"/>.
		/// Executed by the <see cref="ToolConsentPanel"/>.
		/// </summary>
		[BsonIgnore]
		[ChangeTracker.Untracked]
		public IRelayCommand ConsentCommand { get; private set; }

		
		/// <summary>
		/// Command to enable all chunks (confirmation mode).
		/// </summary>
		[BsonIgnore]
		[ChangeTracker.Untracked]
		public IRelayCommand EnableAllCommand { get; private set; }

		/// <summary>
		/// Command to disable all chunks (confirmation mode).
		/// </summary>
		[BsonIgnore]
		[ChangeTracker.Untracked]
		public IRelayCommand DisableAllCommand { get; private set; }

		public TextDiffAdditionalViewModel()
		{
			ConsentCommand = new RelayCommand<ToolApprovalResult?>(ResolveConsent);
			EnableAllCommand = new RelayCommand(EnableAll, () => ChunkViewModels.Any(vm => !vm.IsEnabled));
			DisableAllCommand = new RelayCommand(DisableAll, () => ChunkViewModels.Any(vm => vm.IsEnabled));
		}

		/// <summary>
		/// Resolves the confirmation from a <see cref="ToolApprovalResult"/> produced by the
		/// <see cref="ToolConsentPanel"/>: accepts the changes when approved, declines otherwise.
		/// The confirmation stage memorizes the decision; the card itself owns no memorization.
		/// </summary>
		/// <param name="approval">The approval result, or <see langword="null"/>.</param>
		public void ResolveConsent(ToolApprovalResult? approval)
		{
			if (approval == null)
				return;

			_resolvedConsent = approval;
			_consentTcs?.TrySetResult(approval);

			Notes = approval.Notes;
			IsConfirmed = approval.Approved;
		}

		/// <summary>
		/// Accepts all changes and marks the confirmation as complete. Used by programmatic paths
		/// (cancellation), never by the consent panel, which goes through <see cref="ResolveConsent"/>.
		/// </summary>
		public void Confirm(string? notes) => ResolveConsent(CreateApproval(true, notes));

		/// <summary>
		/// Declines all changes and marks the confirmation as complete.
		/// </summary>
		public void Decline(string? notes) => ResolveConsent(CreateApproval(false, notes));

		private static ToolApprovalResult CreateApproval(bool approved, string? notes) => new()
		{
			Approved = approved,
			Memorization = ToolApprovalMemorization.Once,
			Notes = notes
		};

		/// <summary>
		/// Enables all chunks (checks all checkboxes).
		/// </summary>
		public void EnableAll()
		{
			foreach (var vm in ChunkViewModels)
				vm.IsEnabled = true;
		}

		/// <summary>
		/// Disables all chunks (unchecks all checkboxes).
		/// </summary>
		public void DisableAll()
		{
			foreach (var vm in ChunkViewModels)
				vm.IsEnabled = false;
		}
		
		/// <summary>
		/// Called when a chunk's <see cref="HunkGroupViewModel.IsEnabled"/> changes.
		/// </summary>
		public void OnChunkIsEnabledChanged()
		{
			EnableAllCommand.NotifyCanExecuteChanged();
			DisableAllCommand.NotifyCanExecuteChanged();
			RaisePropertyChanged(nameof(TotalAdded));
			RaisePropertyChanged(nameof(TotalRemoved));
		}

		/// <summary>
		/// Loads the diff from a <see cref="HunkGroups"/> result (produced by <see cref="UnifiedDiff.Compute"/>).
		/// </summary>
		/// <param name="groups">The diff groups to load.</param>
		public void LoadFromHunkGroups(HunkGroups groups)
		{
			var vms = groups.Groups.Select(g => new HunkGroupViewModel(g, isEnabled: true)).ToList();
			foreach (var vm in vms)
				vm.PropertyChanged += OnHunkGroupViewModelPropertyChanged;
			ChunkViewModels = [..vms];
		}

		private void OnHunkGroupViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(HunkGroupViewModel.IsEnabled))
				OnChunkIsEnabledChanged();
		}

		/// <summary>
		/// Builds a <see cref="HunkGroups"/> from the (enabled) chunks for further processing.
		/// </summary>
		/// <returns>A <see cref="HunkGroups"/> containing only the enabled chunks.</returns>
		public HunkGroups BuildEnabledHunkGroups()
		{
			var result = new List<HunkGroup>();
			foreach (var chunk in ChunkViewModels)
				if (chunk.IsEnabled)
					result.Add(chunk.Group);
			return new HunkGroups { Groups = result };
		}

		/// <summary>
		/// Builds a <see cref="HunkGroups"/> from the (enabled) chunks for further processing.
		/// </summary>
		/// <returns>A <see cref="HunkGroups"/> containing only the enabled chunks.</returns>
		public HunkGroups BuildDisabledHunkGroups()
		{
			var result = new List<HunkGroup>();
			foreach (var chunk in ChunkViewModels)
				if (!chunk.IsEnabled)
					result.Add(chunk.Group);
			return new HunkGroups { Groups = result };
		}

		/// <summary>
		/// Applies the (enabled) diff chunks to the original text and returns the modified result.
		/// </summary>
		/// <param name="originalText">The original text content before changes.</param>
		/// <returns>The modified text after applying all enabled diff chunks.</returns>
		public string ApplyToText(string originalText)
		{
			return BuildEnabledHunkGroups().ApplyToText(originalText);
		}
	}
}
