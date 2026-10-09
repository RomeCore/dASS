using LLMDesktopAssistant.Data;
using LLMDesktopAssistant.Data.ChatModels;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Storage;

namespace LLMDesktopAssistant.Tests.Storage;

/// <summary>
/// Tests for <see cref="ChatStorageLock"/>: transactions must be atomic, a failure must never
/// leave an open transaction behind (the previous implementation leaked one and poisoned the
/// thread), and the lock must be reentrant and always released.
/// </summary>
[Collection("Storage")]
public class ChatStorageLockTests
{
	private static (ChatDatabase Database, ChatStorageLock StorageLock) CreateStorage()
	{
		var database = new ChatDatabase(null);
		var config = new ChatCreationConfig
		{
			ChatId = 1,
			CreatedAt = DateTime.Now,
			Database = database
		};
		return (database, new ChatStorageLock(config));
	}

	[Fact]
	public void DoTransaction_CommitsActionChanges()
	{
		var (database, storageLock) = CreateStorage();
		using var _ = database;

		storageLock.DoTransaction(() => database.Chats.Insert(new ChatModel { Title = "first" }));

		Assert.Equal(1, database.Chats.Count());
		Assert.Equal("first", database.Chats.FindAll().Single().Title);
	}

	[Fact]
	public void DoTransaction_RollsBackAndRethrows_WhenActionThrows()
	{
		var (database, storageLock) = CreateStorage();
		using var _ = database;

		var exception = Assert.Throws<InvalidOperationException>(() => storageLock.DoTransaction(() =>
		{
			database.Chats.Insert(new ChatModel { Title = "first" });
			throw new InvalidOperationException("boom");
		}));

		Assert.Equal("boom", exception.Message);
		Assert.Equal(0, database.Chats.Count());
	}

	[Fact]
	public void DoTransaction_AfterFailure_NextTransactionSucceeds()
	{
		var (database, storageLock) = CreateStorage();
		using var _ = database;

		Assert.Throws<InvalidOperationException>(() =>
			storageLock.DoTransaction(() => throw new InvalidOperationException("boom")));

		// Regression: a failed transaction must never stay open, otherwise the next
		// BeginTrans on this thread returns false ("Failed to begin transaction.").
		storageLock.DoTransaction(() => database.Chats.Insert(new ChatModel { Title = "second" }));

		Assert.Equal(1, database.Chats.Count());
	}

	[Fact]
	public void DoTransaction_MultipleSequentialTransactions_AllCommit()
	{
		var (database, storageLock) = CreateStorage();
		using var _ = database;

		for (int i = 0; i < 3; i++)
		{
			int index = i;
			storageLock.DoTransaction(() => database.Chats.Insert(new ChatModel { Title = $"item-{index}" }));
		}

		Assert.Equal(3, database.Chats.Count());
	}

	[Fact]
	public void DoTransaction_Failure_DoesNotStrandRawTransactionOnSameThread()
	{
		var (database, storageLock) = CreateStorage();
		using var _ = database;

		storageLock.DoTransaction(() => database.Chats.Insert(new ChatModel { Title = "first" }));
		Assert.Throws<InvalidOperationException>(() =>
			storageLock.DoTransaction(() => throw new InvalidOperationException("boom")));

		// A raw transaction on the same thread must still be available.
		Assert.True(database.Database.BeginTrans());
		database.Chats.Insert(new ChatModel { Title = "raw" });
		Assert.True(database.Database.Commit());

		Assert.Equal(2, database.Chats.Count());
	}

	[Fact]
	public void Lock_IsReentrant_AndTransactionCanBeRunWhileHeld()
	{
		var (database, storageLock) = CreateStorage();
		using var _ = database;

		storageLock.Lock();
		storageLock.Lock();
		try
		{
			storageLock.DoTransaction(() => database.Chats.Insert(new ChatModel { Title = "nested" }));
		}
		finally
		{
			storageLock.Unlock();
			storageLock.Unlock();
		}

		Assert.Equal(1, database.Chats.Count());
		AssertOtherThreadCanEnterLock(storageLock);
	}

	[Fact]
	public void Lock_ReleasedAfterFailedTransaction()
	{
		var (database, storageLock) = CreateStorage();
		using var _ = database;

		storageLock.Lock();
		try
		{
			Assert.Throws<InvalidOperationException>(() =>
				storageLock.DoTransaction(() => throw new InvalidOperationException("boom")));
		}
		finally
		{
			storageLock.Unlock();
		}

		AssertOtherThreadCanEnterLock(storageLock);
	}

	private static void AssertOtherThreadCanEnterLock(IChatStorageLock storageLock)
	{
		bool acquired = false;
		var thread = new Thread(() =>
		{
			storageLock.Lock();
			acquired = true;
			storageLock.Unlock();
		});
		thread.Start();
		Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "The storage lock was not released.");
		Assert.True(acquired);
	}
}
