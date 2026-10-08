namespace LLMDesktopAssistant.Addons.MVVM
{
	public class AddonCardContext<TAddon, TChange> : NotifyPropertyChanged
		where TAddon : AddonChangedBase<TAddon, TChange>
		where TChange : AddonChangeBase, new()
	{
		public required TAddon Addon { get; init; }

		/// <summary>
		/// Gets the change set the card edits, or <see langword="null"/> for a read-only card.
		/// </summary>
		public AddonSetConfigurationBase<TChange>? SetConfig { get; init; }

		/// <summary>
		/// Gets the command invoked when a tag chip of the card is clicked. The tag is passed as the command
		/// parameter; pages usually bind it to their search box. When <see langword="null"/>, the tags are
		/// rendered as plain (non-clickable) chips.
		/// </summary>
		public ICommand? TagClickCommand { get; init; }

		/// <summary>
		/// Gets the callback invoked after the addon file was deleted, so that the page can rebuild its list.
		/// </summary>
		public Action? OnDeleted { get; init; }

		/// <summary>
		/// Gets a value indicating whether the card is allowed to edit the overrides of the addon.
		/// </summary>
		public bool CanEdit => SetConfig is not null;

		public TChange? Change
		{
			get => field ??= SetConfig?.Changes.GetValueOrDefault(Addon.Key);
			private set => SetProperty(ref field, value);
		}

		public TChange EnsureChange()
		{
			if (Change is not null)
				return Change;

			if (SetConfig is null)
				throw new InvalidOperationException("Cannot get change in the read-only context.");

			if (!SetConfig.Changes.TryGetValue(Addon.Key, out var change))
			{
				change = new TChange();
				SetConfig.Changes.Add(Addon.Key, change);
			}
			Change = change;
			return Change;
		}

		public void Reset()
		{
			SetConfig?.Changes.Remove(Addon.Key);
			Change = null;
		}
	}
}
