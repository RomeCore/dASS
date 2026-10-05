using System.ComponentModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Localization;
using Material.Icons;

namespace LLMDesktopAssistant.Addons.MVVM
{
	/// <summary>
	/// The single view model of an addon card. It knows nothing about concrete addon types:
	/// everything type-specific is expressed as <see cref="IAddonCardElement"/> instances
	/// produced by an addon card factory.
	/// </summary>
	[ViewModelFor(typeof(AddonCardView))]
	public class AddonCardViewModel : ViewModelBase
	{
		/// <summary>
		/// Gets all elements of the card, in their original order (not grouped by kind).
		/// </summary>
		public ImmutableList<IAddonCardElement> Elements { get; }

		/// <summary>
		/// Gets the elements of the card that edit (or indicate) overrides: header changes and block changes.
		/// Used by the reset command.
		/// </summary>
		public ImmutableList<IAddonCardChange> Changes { get; }

		/// <summary>
		/// Gets the icon of the card, usually taken from the addon type descriptor.
		/// </summary>
		public VisualIconKind? Icon { get; init; }

		/// <summary>
		/// Gets the brush used to paint the name prefix of the addon.
		/// </summary>
		public IBrush? NamePrefixBrush { get; init; }

		/// <summary>
		/// Gets the name prefix of the addon.
		/// </summary>
		public LocaleKeyBase? NamePrefix { get; init; }

		/// <summary>
		/// Gets the display name of the addon.
		/// </summary>
		public required LocaleKeyBase Name { get; init; }

		/// <summary>
		/// Gets the optional secondary name shown next to the name (for example, the identifier
		/// of an addon whose display name differs from it).
		/// </summary>
		public LocaleKeyBase? Subtitle { get; init; }

		/// <summary>
		/// Gets the description of the addon.
		/// </summary>
		public LocaleKeyBase? Description { get; init; }

		/// <summary>
		/// Gets the header elements placed to the left of the name.
		/// </summary>
		public ImmutableList<IAddonCardHeaderElement> LeftHeaderElements { get; }

		/// <summary>
		/// Gets the header elements placed to the right of the name.
		/// </summary>
		public ImmutableList<IAddonCardHeaderElement> RightHeaderElements { get; }

		/// <summary>
		/// Gets the top-level chips of the card.
		/// </summary>
		public ImmutableList<IAddonCardChip> Chips { get; }

		/// <summary>
		/// Gets the always-visible blocks of the card.
		/// </summary>
		public ImmutableList<IAddonCardBlock> Blocks { get; }

		/// <summary>
		/// Gets the blocks toggled by their own buttons in the action row.
		/// </summary>
		public ImmutableList<IAddonCardBlock> CollapsibleBlocks { get; }

		/// <summary>
		/// Gets the blocks shown when the details section is expanded.
		/// </summary>
		public ImmutableList<IAddonCardBlock> DetailBlocks { get; }

		/// <summary>
		/// Gets the elements shown on the left side of the action row (path, tags, selectors, etc).
		/// </summary>
		public ImmutableList<IAddonCardActionRowElement> ActionRowElements { get; }

		/// <summary>
		/// Gets the action buttons of the card.
		/// </summary>
		public ImmutableList<IAddonCardAction> Actions { get; }

		/// <summary>
		/// Gets a value indicating whether the card has a name prefix.
		/// </summary>
		public bool HasNamePrefix => !string.IsNullOrWhiteSpace(NamePrefix?.Value);

		/// <summary>
		/// Gets a value indicating whether the card has a subtitle.
		/// </summary>
		public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle?.Value);

		/// <summary>
		/// Gets a value indicating whether the card has a non-empty description.
		/// </summary>
		public bool HasDescription => !string.IsNullOrWhiteSpace(Description?.Value);

		/// <summary>
		/// Gets a value indicating whether the addon (or, for a group card, any of its visible children)
		/// has any overridden values, and therefore whether the reset button makes sense. Updated live
		/// as long as the changes implement change notification.
		/// </summary>
		public bool HasChanges => Changes.Any(c => c.IsChanged) || Children.Any(c => c.IsVisible && c.HasChanges);

		/// <summary>
		/// Gets a value indicating whether the card has a details section.
		/// </summary>
		public bool HasDetails => DetailBlocks.Count > 0;

		/// <summary>
		/// Gets the command that resets all overrides of this addon back to the definition values.
		/// </summary>
		public ICommand? ResetCommand { get; init; }

		/// <summary>
		/// Gets or sets a value indicating whether the details section is expanded.
		/// </summary>
		public bool IsDetailsVisible
		{
			get;
			set => SetProperty(ref field, value);
		}

		public bool IsVisible
		{
			get;
			set => SetProperty(ref field, value);
		}

		public bool IsChildrenExpanded
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Gets a value indicating whether the card holds child cards (i.e. whether it is a group card).
		/// </summary>
		public bool HasChildren => Children.Count > 0;

		private ImmutableList<AddonCardViewModel> _children = [];

		/// <summary>
		/// Gets the child cards of the group card. The children are owned by the list: the group card only
		/// observes them, so that <see cref="HasChanges"/> covers the visible children.
		/// </summary>
		public ImmutableList<AddonCardViewModel> Children
		{
			get => _children;
			init
			{
				_children = value;

				foreach (var child in _children)
					child.PropertyChanged += OnChildPropertyChanged;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AddonCardViewModel"/> class.
		/// </summary>
		/// <param name="elements">All elements of the card, in any order (they are grouped by kind).</param>
		public AddonCardViewModel(IEnumerable<IAddonCardElement> elements)
		{
			Elements = [.. elements.OrderBy(e => e.Order)];
			Changes = [.. Elements.OfType<IAddonCardChange>()];

			LeftHeaderElements = [.. Elements.OfType<IAddonCardHeaderElement>().Where(e => e.IsShownLeft)];
			RightHeaderElements = [.. Elements.OfType<IAddonCardHeaderElement>().Where(e => !e.IsShownLeft)];

			Chips = [.. Elements.OfType<IAddonCardChip>()];

			Blocks = [.. Elements.OfType<IAddonCardBlock>().Where(b => b.Visibility == AddonCardBlockVisibility.Inline)];
			CollapsibleBlocks = [.. Elements.OfType<IAddonCardBlock>().Where(b => b.Visibility == AddonCardBlockVisibility.Collapsible)];
			DetailBlocks = [.. Elements.OfType<IAddonCardBlock>().Where(b => b.Visibility == AddonCardBlockVisibility.Details)];

			ActionRowElements = [.. Elements.OfType<IAddonCardActionRowElement>()];
			Actions = [.. Elements.OfType<IAddonCardAction>()];

			foreach (var change in Changes)
				if (change is INotifyPropertyChanged notifier)
					notifier.PropertyChanged += OnChangePropertyChanged;
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				foreach (var change in Changes)
					if (change is INotifyPropertyChanged notifier)
						notifier.PropertyChanged -= OnChangePropertyChanged;
				foreach (var child in Children)
					child.PropertyChanged -= OnChildPropertyChanged;
				foreach (var element in Elements)
					if (element is IDisposable disposableElement)
						disposableElement.Dispose();
			}
		}

		private void OnChangePropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(IAddonCardChange.IsChanged))
				RaisePropertyChanged(nameof(HasChanges));
		}

		private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is nameof(HasChanges) or nameof(IsVisible))
				RaisePropertyChanged(nameof(HasChanges));
		}
	}
}
