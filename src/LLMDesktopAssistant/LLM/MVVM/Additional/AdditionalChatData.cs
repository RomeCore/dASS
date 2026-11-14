using LiteDB;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.LLM.MVVM.Additional
{
	/// <summary>
	/// The base class for additional chat data.
	/// This class is required to be BSON-serializable if not temporary.
	/// </summary>
	public class AdditionalChatData : NotifyPropertyChanged, IChatAttachment
	{
		/// <summary>
		/// Gets the order of this additional chat data. Used for sorting in the UI. Lower values appear first.
		/// </summary>
		[BsonIgnore]
		public virtual int Order { get; } = 0;

		/// <summary>
		/// Gets or sets the replacement key that can be used to replace other instance stored in
		/// <see cref="AdditionalChatDataCollection"/> with the same key.
		/// </summary>
		public string? ReplacementKey { get; init; }

		/// <summary>
		/// Gets or sets a value indicating whether the additional chat data is visible.
		/// Tip: chat data can be invisible when used only to store data that is not displayed in the UI.
		/// </summary>
		public bool IsVisible
		{
			get;
			set => SetProperty(ref field, value);
		} = true;

		/// <summary>
		/// Gets or sets a value indicating whether the additional chat data is intended to be disposed manually.
		/// If set to <see langword="true"/>, the <see cref="ChatObjectBase"/> will not dispose this instance upon
		/// chat object disposal or when this data is removed from the chat object.
		/// </summary>
		public bool ManualDispose
		{
			get;
			set => SetProperty(ref field, value);
		} = false;

		/// <summary>
		/// Gets or sets a value indicating whether the additional chat data is temporary.
		/// Temporary chat data are not stored in the database, and are only used for short-lived operations.
		/// </summary>
		[BsonIgnore]
		public bool IsTemporary
		{
			get;
			set => SetProperty(ref field, value);
		} = false;
	}
}