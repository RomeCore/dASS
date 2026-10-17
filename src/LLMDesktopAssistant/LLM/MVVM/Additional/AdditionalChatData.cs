using LiteDB;
using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.LLM.MVVM.Additional
{
	/// <summary>
	/// The base class for additional message view models.
	/// This class is required to be BSON-serializable if not temporary.
	/// </summary>
	public class AdditionalChatData : NotifyPropertyChanged, IChatAttachment
	{
		/// <summary>
		/// Gets the order of this additional view model. Used for sorting in the UI. Lower values appear first.
		/// </summary>
		[BsonIgnore]
		public virtual int Order { get; } = 0;

		private bool _isVisible = true;
		/// <summary>
		/// Gets or sets a value indicating whether the additional view model is visible.
		/// Tip: view model can be invisible when used only to store data that is not displayed in the UI.
		/// </summary>
		public bool IsVisible
		{
			get => _isVisible;
			set => SetProperty(ref _isVisible, value);
		}

		private bool _isTemporary = false;
		/// <summary>
		/// Gets or sets a value indicating whether the additional view model is temporary.
		/// Temporary view models are not stored in the database, and are only used for short-lived operations.
		/// </summary>
		[BsonIgnore]
		public bool IsTemporary
		{
			get => _isTemporary;
			set => SetProperty(ref _isTemporary, value);
		}
	}
}