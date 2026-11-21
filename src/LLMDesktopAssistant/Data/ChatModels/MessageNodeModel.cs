using LiteDB;

namespace LLMDesktopAssistant.Data.ChatModels
{
	/// <summary>
	/// Represents a node in a conversation tree, containing messages and child nodes.
	/// </summary>
	public sealed class MessageNodeModel
	{
		/// <summary>
		/// The unique identifier for the message node. This value is used to identify the message in the database.
		/// </summary>
		[BsonId]
		public int Id { get; set; }

		/// <summary>
		/// The chat ID associated with the message node.
		/// </summary>
		public int ChatId { get; set; }

		/// <summary>
		/// The value indicates whether the current message node is a root node.
		/// </summary>
		public bool IsRootNode { get; set; }

		/// <summary>
		/// The parent node ID that points either to <see cref="MessageNodeModel"/> or <see cref="ChatModel"/> based on <see cref="IsRootNode"/>.
		/// </summary>
		public int ParentId { get; set; } = -1;

		/// <summary>
		/// The identifier that leads to target <see cref="MessageModel"/> associated with this node.
		/// </summary>
		public int MessageId { get; set; }

		/// <summary>
		/// The ID of the selected node in the conversation. Can be -1 when no node is currently selected.
		/// </summary>
		public int SelectedNodeId { get; set; } = -1;
	}
}
