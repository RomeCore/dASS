using LiteDB;
using LLMDesktopAssistant.Data.ChatModels;

namespace LLMDesktopAssistant.Data
{
	/// <summary>
	/// Manages the database for storing and retrieving conversation data.
	/// </summary>
	public class ChatDatabase : IDisposable
	{
		public ILiteDatabase Database { get; }
		public ILiteCollection<ChatModel> Chats { get; }
		public ILiteCollection<MessageNodeModel> MessageNodes { get; }
		public ILiteCollection<MessageModel> Messages { get; }
		public ILiteCollection<ToolCallModel> ToolCalls { get; }
		public ILiteCollection<AdditionalChatDataModel> AdditionalChatData { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ChatDatabase"/> class.
		/// </summary>
		/// <param name="path">The path to the database file. Or the "Memory=true;" if you want to use in-memory database.</param>
		public ChatDatabase(string? path)
		{
			if (path is not null && Path.GetDirectoryName(path) is string dir)
				Directory.CreateDirectory(dir);
			Database = new LiteDatabase(path ?? "Filename=:memory:");

			Chats = Database.GetCollection<ChatModel>();
			MessageNodes = Database.GetCollection<MessageNodeModel>();
			Messages = Database.GetCollection<MessageModel>();
			ToolCalls = Database.GetCollection<ToolCallModel>();
			AdditionalChatData = Database.GetCollection<AdditionalChatDataModel>();

			MessageNodes.EnsureIndex(x => x.ParentId);
			MessageNodes.EnsureIndex(x => x.SelectedNodeId);
			MessageNodes.EnsureIndex(x => x.IsRootNode);
			ToolCalls.EnsureIndex(x => x.MessageId);
			ToolCalls.EnsureIndex(x => x.ToolCallId);
			AdditionalChatData.EnsureIndex(x => x.ParentKind);
			AdditionalChatData.EnsureIndex(x => x.ParentId);
		}

		/// <summary>
		/// Disposes the database connection.
		/// </summary>
		public void Dispose()
		{
			Database?.Dispose();
		}
	}
}
