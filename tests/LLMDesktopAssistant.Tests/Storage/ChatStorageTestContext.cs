using LLMDesktopAssistant.Data;
using LLMDesktopAssistant.Data.ChatModels;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Storage;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Tests.Fakes;

namespace LLMDesktopAssistant.Tests.Storage;

/// <summary>
/// Test harness around <see cref="ChatStorageService"/> backed by an in-memory database.
/// </summary>
internal sealed class ChatStorageTestContext : IDisposable
{
	public ChatDatabase Database { get; }
	public Chat Chat { get; }
	public ChatCreationConfig Config { get; }
	public IChatStorageLock StorageLock { get; }
	public ChatStorageService Service { get; }

	public ChatStorageTestContext(Func<ChatCreationConfig, IChatStorageLock>? storageLockFactory = null)
	{
		Database = new ChatDatabase(null);
		Chat = new Chat(new EmptyServiceProvider());
		Config = new ChatCreationConfig
		{
			ChatId = 1,
			CreatedAt = DateTime.Now,
			Database = Database
		};

		storageLockFactory ??= config => new ChatStorageLock(config);
		StorageLock = storageLockFactory(Config);
		Service = new ChatStorageService(Chat, Config, new FakeChatSettingsService(), StorageLock, Chat.Services);
		Service.Reload();
	}

	/// <summary>The persisted chat row.</summary>
	public ChatModel Model => Database.Chats.FindAll().Single();

	/// <summary>All message nodes ordered by id.</summary>
	public List<MessageNodeModel> Nodes => Database.MessageNodes.FindAll().OrderBy(n => n.Id).ToList();

	/// <summary>All message rows ordered by id.</summary>
	public List<MessageModel> Messages => Database.Messages.FindAll().OrderBy(m => m.Id).ToList();

	/// <summary>The root node of the currently selected branch (as pointed by the chat row).
	/// Multiple root nodes may exist (one per root branch), so the chat pointer is the source of truth.</summary>
	public MessageNodeModel RootNode => Nodes.Single(n => n.Id == Model.RootNodeId);

	public MessageNodeModel NodeByMessageId(int messageId) => Nodes.Single(n => n.MessageId == messageId);

	public static UserMessage CreateMessage(string content) => new()
	{
		Content = content,
		SenderLogin = "tester",
		Visibility = MessageVisibility.Always,
		VisibleTo = [],
		IsVisibleToWhiteList = false
	};

	public void Dispose()
	{
		Service.Dispose();
		Database.Dispose();
	}

}

/// <summary>Minimal <see cref="IChatSettingsService"/> for storage tests.</summary>
internal sealed class FakeChatSettingsService : IChatSettingsService
{
	public ChatSettings Settings { get; private set; } = new();

	public event EventHandler? SettingsChanged { add { } remove { } }

	public void SetSettings(ChatSettings settings) => Settings = settings;

	public void LoadFromProfile(string profileName = "-default") { }
}

/// <summary>
/// Wraps a real <see cref="IChatStorageLock"/> and can inject a failure after the action body completes
/// (simulating an error that happens between the operation and the commit).
/// </summary>
internal sealed class FaultInjectingStorageLock(IChatStorageLock inner) : IChatStorageLock
{
	public bool FailAfterAction { get; set; }

	public void Lock() => inner.Lock();

	public void Unlock() => inner.Unlock();

	public void DoTransaction(Action action)
	{
		inner.DoTransaction(() =>
		{
			action();
			if (FailAfterAction)
				throw new InvalidOperationException("Injected failure");
		});
	}
}
