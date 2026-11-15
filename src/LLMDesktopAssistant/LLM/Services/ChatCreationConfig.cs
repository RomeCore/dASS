using LLMDesktopAssistant.Data;

namespace LLMDesktopAssistant.LLM.Services
{
	[ChatService(typeof(IChatCreationConfig))]
	public class ChatCreationConfig : IChatCreationConfig
	{
		private int? _chatId = null;
		public int ChatId
		{
			get => _chatId ?? throw new InvalidOperationException("ChatId has not been set.");
			set
			{
				if (_chatId is not null)
					throw new InvalidOperationException("ChatId cannot be changed after it has been set.");
				_chatId = value;
			}
		}

		private DateTime? _createdAt = null;
		public DateTime CreatedAt
		{
			get => _createdAt ?? throw new InvalidOperationException("CreatedAt has not been set.");
			set
			{
				if (_createdAt is not null)
					throw new InvalidOperationException("CreatedAt cannot be changed after it has been set.");
				_createdAt = value;
			}
		}

		public bool IsTemporary { get; set; }

		private ChatDatabase? _database = null;
		public ChatDatabase Database
		{
			get => _database ?? throw new InvalidOperationException("Database has not been set.");
			set
			{
				if (_database is not null)
					throw new InvalidOperationException("Database cannot be changed after it has been set.");
				_database = value;
			}
		}
	}
}
