using Avalonia.Media;
using LLMDesktopAssistant.Localization;
using Material.Icons;

namespace LLMDesktopAssistant.LLM.MVVM.Additional
{
	/// <summary>
	/// The base class for message parts - small additional view models that are rendered
	/// as compact chips (see <see cref="AdditionalMessagePartView"/>) in a WrapPanel
	/// at the bottom of a message or a tool call.
	/// </summary>
	[ViewModelFor(typeof(AdditionalMessagePartView))]
	public abstract class AdditionalMessagePart : AdditionalChatData
	{
		private VisualIconKind? _chipIcon;
		/// <summary>
		/// Gets or sets the icon shown on the part chip.
		/// </summary>
		public VisualIconKind? ChipIcon
		{
			get => _chipIcon;
			set => SetProperty(ref _chipIcon, value);
		}

		private LocaleKeyBase? _chipTitle;
		/// <summary>
		/// Gets or sets the title shown on the part chip. A locale key rather than a rendered string, so the chip
		/// relocalizes when the language changes (like the message's <c>Error</c>).
		/// </summary>
		public LocaleKeyBase? ChipTitle
		{
			get => _chipTitle;
			set => SetProperty(ref _chipTitle, value);
		}

		private Color? _chipColor;
		/// <summary>
		/// Gets or sets the accent color of the part chip (tints the icon and the chip border).
		/// </summary>
		public Color? ChipColor
		{
			get => _chipColor;
			set => SetProperty(ref _chipColor, value);
		}

		private bool _isRestorable = true;
		/// <summary>
		/// Gets or sets a value indicating whether this part is restored into the input draft when
		/// the message is edited. Parts injected by commands set this to <see langword="false"/>
		/// so editing does not clone them back into the user input.
		/// </summary>
		public bool IsRestorable
		{
			get => _isRestorable;
			set => SetProperty(ref _isRestorable, value);
		}

		/// <summary>
		/// Creates a shallow copy of this part. Used when editing messages to avoid
		/// sharing part instances between the draft state and the original message.
		/// </summary>
		public virtual AdditionalMessagePart Clone() => (AdditionalMessagePart)MemberwiseClone();
	}
}
