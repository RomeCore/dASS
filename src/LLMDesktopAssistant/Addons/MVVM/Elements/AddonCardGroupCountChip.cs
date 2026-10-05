using System.ComponentModel;
using Avalonia.Media;
using LLMDesktopAssistant.Localization;
using Material.Icons;

namespace LLMDesktopAssistant.Addons.MVVM.Elements
{
	/// <summary>
	/// The chip of a group card that shows how many children the group has and, while the search query
	/// hides some of them, how many of them are visible. The chip updates itself live.
	/// </summary>
	public class AddonCardGroupCountChip : AddonCardElementBase, IAddonCardChip
	{
		private readonly ImmutableList<AddonCardViewModel> _children;
		private LocaleKeyBase? _label;

		/// <summary>
		/// Initializes a new instance of the <see cref="AddonCardGroupCountChip"/> class.
		/// </summary>
		/// <param name="children">The cards of the children of the group.</param>
		public AddonCardGroupCountChip(ImmutableList<AddonCardViewModel> children)
		{
			_children = children;

			foreach (var child in children)
				child.PropertyChanged += Child_PropertyChanged;

			Refresh();
		}

		/// <inheritdoc/>
		public bool HasBorder => true;

		/// <inheritdoc/>
		public IBrush? Brush => null;

		/// <inheritdoc/>
		public double Opacity => 1;

		/// <inheritdoc/>
		public VisualIconKind? Icon => null;

		/// <inheritdoc/>
		public LocaleKeyBase? Label
		{
			get => _label;
			private set => SetProperty(ref _label, value);
		}

		/// <inheritdoc/>
		public LocaleKeyBase? ToolTip => null;

		/// <inheritdoc/>
		public object? Content => null;

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				foreach (var child in _children)
					child.PropertyChanged -= Child_PropertyChanged;
			}
		}

		private void Child_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is nameof(AddonCardViewModel.IsVisible))
				Refresh();
		}

		private void Refresh()
		{
			var total = _children.Count;
			var visible = _children.Count(child => child.IsVisible);

			Label = visible != total
				? Locale.GetConstKey(Locale.Format("card.group.visible_count", visible, total))
				: Locale.GetConstKey(Locale.Format("card.group.count", total));
		}
	}
}
