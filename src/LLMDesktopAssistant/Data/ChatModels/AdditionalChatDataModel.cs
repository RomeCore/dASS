using LiteDB;
using LLMDesktopAssistant.LLM.MVVM.Additional;

namespace LLMDesktopAssistant.Data.ChatModels
{
	/// <summary>
	/// The additional chat data model that can be applied to a message, a tool call, or a chat.
	/// </summary>
	public class AdditionalChatDataModel
	{
		/// <summary>
		/// The unique identifier for the additional chat data model.
		/// </summary>
		[BsonId]
		public int Id { get; set; }

		/// <summary>
		/// Gets or sets a value indicating the order of the additional chat data model.
		/// </summary>
		public int Order { get; set; }

		/// <summary>
		/// Gets or sets a value indicating kind of parent for this additional chat data model.
		/// </summary>
		public ChatDataParentKind ParentKind { get; set; }

		/// <summary>
		/// Gets or sets the parent ID that this additional chat data model belongs to.
		/// </summary>
		public int ParentId { get; set; }

		/// <summary>
		/// The additional chat data model associated with the parent.
		/// </summary>
		public AdditionalChatData Data
		{
			get => field ??= new();
			set;
		}
	}
}
