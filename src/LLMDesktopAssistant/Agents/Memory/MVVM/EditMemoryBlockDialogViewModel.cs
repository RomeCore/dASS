using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;
using LLMDesktopAssistant.Settings;
using Material.Icons;

namespace LLMDesktopAssistant.Agents.Memory.MVVM
{
	/// <summary>
	/// ViewModel for the "Edit Memory Block" dialog. Edits the block contents directly
	/// and also hosts block management operations: duplicate, rename and delete.
	/// The dialog is split into sections selected through a tree: block information,
	/// facts management and logs management.
	/// </summary>
	[ViewModelFor(typeof(EditMemoryBlockDialogView))]
	public class EditMemoryBlockDialogViewModel : ViewModelBase
	{
		private readonly IMemoryDatabaseManager _databaseManager;
		private EditMemoryBlockSection? _selectedSection;

		/// <summary>
		/// Gets the memory block being edited.
		/// </summary>
		public MemoryBlock Block { get; }

		/// <summary>
		/// Gets the identifier of the edited block.
		/// </summary>
		public string BlockId => Block.Id;

		/// <summary>
		/// Gets the settings category that stores the shared memory block definitions.
		/// </summary>
		public static SettingsCategory<MemoryBlock> BlocksCategory { get; } = SettingsManager.GetCategory<MemoryBlock>();

		private bool _isEditingId;
		/// <summary>
		/// Gets or sets a value indicating whether the block ID rename editor is visible.
		/// </summary>
		public bool IsEditingId
		{
			get => _isEditingId;
			set => SetProperty(ref _isEditingId, value);
		}

		private string? _newId;
		/// <summary>
		/// Gets or sets the block ID being entered for the rename operation.
		/// </summary>
		public string? NewId
		{
			get => _newId;
			set => SetProperty(ref _newId, value);
		}

		/// <summary>
		/// Gets the ID of the block duplicated by this dialog, or <see langword="null"/>.
		/// </summary>
		public string? DuplicatedBlockId { get; private set; }

		/// <summary>
		/// Gets the previous ID when the block was renamed by this dialog, or <see langword="null"/>.
		/// </summary>
		public string? RenamedFromId { get; private set; }

		/// <summary>
		/// Gets the new ID when the block was renamed by this dialog, or <see langword="null"/>.
		/// </summary>
		public string? RenamedToId { get; private set; }

		/// <summary>
		/// Gets the ID of the block deleted by this dialog, or <see langword="null"/>.
		/// </summary>
		public string? DeletedBlockId { get; private set; }

		/// <summary>
		/// Gets the command that closes the dialog. Block edits are applied immediately.
		/// </summary>
		public ICommand CloseCommand { get; }

		/// <summary>
		/// Gets the command that duplicates the block together with its database contents.
		/// </summary>
		public AsyncRelayCommand DuplicateCommand { get; }

		/// <summary>
		/// Gets the command that starts renaming the block ID.
		/// </summary>
		public RelayCommand RenameCommand { get; }

		/// <summary>
		/// Gets the command that confirms the block ID rename.
		/// </summary>
		public AsyncRelayCommand ConfirmRenameCommand { get; }

		/// <summary>
		/// Gets the command that cancels the block ID rename.
		/// </summary>
		public ICommand CancelRenameCommand { get; }

		/// <summary>
		/// Gets the command that deletes the block together with its database contents.
		/// </summary>
		public AsyncRelayCommand DeleteCommand { get; }

		/// <summary>
		/// Gets the command that clears all facts and logs stored in the block,
		/// keeping the block itself and its configuration.
		/// </summary>
		public AsyncRelayCommand ClearCommand { get; }

		/// <summary>
		/// Gets the tree of sections of the dialog. The sections are created lazily when
		/// selected: block information, facts management and logs management.
		/// </summary>
		public IReadOnlyList<EditMemoryBlockSection> Sections { get; }

		/// <summary>
		/// Gets or sets the currently selected section. Selecting a section creates its view
		/// model lazily on first access.
		/// </summary>
		public EditMemoryBlockSection? SelectedSection
		{
			get => _selectedSection;
			set => SetProperty(ref _selectedSection, value);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="EditMemoryBlockDialogViewModel"/> class.
		/// </summary>
		/// <param name="block">The memory block to edit.</param>
		/// <param name="databaseManager">The memory database manager used for block database operations.</param>
		/// <param name="factStore">The fact store used by the facts management section.</param>
		/// <param name="logStore">The log store used by the logs management section.</param>
		public EditMemoryBlockDialogViewModel(
			MemoryBlock block,
			IMemoryDatabaseManager databaseManager,
			IMemoryFactStore factStore,
			IMemoryLogStore logStore)
		{
			Block = block;
			_databaseManager = databaseManager;

			CloseCommand = new RelayCommand(() => DialogManager.CloseDialog(true));
			DuplicateCommand = new AsyncRelayCommand(DuplicateAsync);
			RenameCommand = new RelayCommand(Rename);
			ConfirmRenameCommand = new AsyncRelayCommand(ConfirmRenameAsync);
			CancelRenameCommand = new RelayCommand(() => IsEditingId = false);
			DeleteCommand = new AsyncRelayCommand(DeleteAsync);
			ClearCommand = new AsyncRelayCommand(ClearAsync);

			Sections =
			[
				new EditMemoryBlockSection(LocalizationManager.LocalizeStatic("settings.memory.section.info.title"),
					MaterialIconKind.InfoCircle,
					() => this),

				new EditMemoryBlockSection(LocalizationManager.LocalizeStatic("settings.memory.section.facts.title"),
					MaterialIconKind.FormatListBulleted,
					() => new MemoryBlockFactsViewModel(Block, factStore)),

				new EditMemoryBlockSection(LocalizationManager.LocalizeStatic("settings.memory.section.logs.title"),
					MaterialIconKind.History,
					() => new MemoryBlockLogsViewModel(Block, logStore))
			];
			_selectedSection = Sections[0];
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				foreach (var section in Sections)
				{
					if (!ReferenceEquals(section.ViewModel, this) && section.ViewModel is IDisposable disposable)
						disposable.Dispose();
				}
			}
		}

		private async Task DuplicateAsync()
		{
			var id = GenerateBlockId();
			if (!BlocksCategory.Copy(Block.Id, id))
				return;

			var copy = BlocksCategory.Get(id);
			copy.Name = Block.Name + " (" + LocalizationManager.LocalizeStatic("settings.memory.copy_suffix") + ")";

			await _databaseManager.CopyAsync(Block.Id, id);

			DuplicatedBlockId = id;
			DialogManager.CloseDialog(true);
		}

		private void Rename()
		{
			NewId = Block.Id;
			IsEditingId = true;
		}

		private async Task ConfirmRenameAsync()
		{
			var oldId = Block.Id;
			var newId = NewId?.Trim();
			if (string.IsNullOrWhiteSpace(newId) || newId == oldId)
			{
				IsEditingId = false;
				return;
			}

			if (!BlocksCategory.Rename(oldId, newId))
				return;

			await _databaseManager.RenameAsync(oldId, newId);

			RenamedFromId = oldId;
			RenamedToId = newId;
			IsEditingId = false;
			DialogManager.CloseDialog(true);
		}

		private async Task DeleteAsync()
		{
			var confirm = new ConfirmDialogViewModel
			{
				Title = LocalizationManager.LocalizeStatic("settings.memory.delete.title"),
				Description = LocalizationManager.LocalizeStatic("settings.memory.delete.confirm"),
				ConfirmText = LocalizationManager.LocalizeStatic("settings.memory.delete.action"),
				CancelText = LocalizationManager.LocalizeStatic("common.cancel"),
				IsDanger = true
			};

#pragma warning disable CS8605 // Unboxing is safe: the confirmation dialog always returns bool.
			var confirmed = (bool)await DialogManager.ShowDialogAsync(confirm);
#pragma warning restore CS8605

			if (!confirmed)
				return;

			var id = Block.Id;
			try
			{
				await _databaseManager.DeleteAsync(id);
				BlocksCategory.Remove(id);

				DeletedBlockId = id;
				DialogManager.CloseDialog(true);

				var toast = ServiceRegistry.Provider.GetRequiredService<IToastService>();
				toast.ShowSuccess(LocalizationManager.LocalizeStatic("settings.memory.delete.success"));
			}
			catch (Exception ex)
			{
				var toast = ServiceRegistry.Provider.GetRequiredService<IToastService>();
				toast.ShowError(LocalizationManager.LocalizeStatic("settings.memory.delete.error"), ex.Message);
			}
		}

		private async Task ClearAsync()
		{
			var confirm = new ConfirmDialogViewModel
			{
				Title = LocalizationManager.LocalizeStatic("settings.memory.clear.title"),
				Description = LocalizationManager.LocalizeStatic("settings.memory.clear.confirm"),
				ConfirmText = LocalizationManager.LocalizeStatic("settings.memory.clear.action"),
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
				var (facts, logs) = await _databaseManager.ClearAsync(Block, clearFacts: true, clearLogs: true);

				var toast = ServiceRegistry.Provider.GetRequiredService<IToastService>();
				toast.ShowSuccess(
					LocalizationManager.LocalizeStatic("settings.memory.clear.success"),
					LocalizationManager.LocalizeStaticFormat("settings.memory.clear.result", facts, logs));
			}
			catch (Exception ex)
			{
				var toast = ServiceRegistry.Provider.GetRequiredService<IToastService>();
				toast.ShowError(LocalizationManager.LocalizeStatic("settings.memory.clear.error"), ex.Message);
			}
		}

		private static string GenerateBlockId()
		{
			var taken = BlocksCategory.Ids.ToHashSet();
			int i = 1;
			while (taken.Contains($"block-{i}"))
				i++;
			return $"block-{i}";
		}
	}
}
