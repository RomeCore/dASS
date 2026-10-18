using System.Diagnostics;
using LLMDesktopAssistant.Data;
using LLMDesktopAssistant.Data.ChatModels;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services.Storage;
using LLMDesktopAssistant.Services;
using Serilog;

namespace LLMDesktopAssistant.LLM.Services
{
	public class ChatManagementService(
		IServiceProvider services,
		ChatDatabase database
		) : IChatManagementService
	{
		private ChatInfo CreateChatInfo(ChatModel model)
		{
			return new ChatInfo
			{
				Id = model.Id,
				Title = model.Title,
				Topic = model.Topic,
				CreatedAt = model.CreatedAt,
				LastModifiedAt = model.LastModifiedAt
			};
		}

		public IEnumerable<ChatInfo> GetChats()
		{
			return database.Chats.FindAll().Select(CreateChatInfo).ToList();
		}

		private IServiceScope OpenChatScope(ChatDatabase database, int chatId)
		{
			var sw = new Stopwatch();

			sw.Start();
			var scope = services.CreateScope();
			Log.Debug("Chat scope opened in {ElapsedMilliseconds} ms", sw.ElapsedMilliseconds);

			sw.Restart();
			var existingChat = database.Chats.FindById(chatId);

			var chatConfig = scope.ServiceProvider.GetRequiredService<IChatCreationConfig>();
			chatConfig.ChatId = chatId;
			chatConfig.CreatedAt = existingChat?.CreatedAt ?? DateTime.Now;
			chatConfig.Database = database;

			// Ensure at least one default agent exists for this chat
			var chat = scope.ServiceProvider.GetRequiredService<Chat>();
			scope.ServiceProvider.GetRequiredService<IChatSettingsService>().Settings.Agents.EnsureDefaultAgent();

			var storage = scope.ServiceProvider.GetRequiredService<IChatStorageService>();
			storage.Reload();
			Log.Debug("Chat storage reloaded in {ElapsedMilliseconds} ms, message count: {MessageCount}", sw.ElapsedMilliseconds, chat.Messages.Count);

			// Activate all chat-specific services
			sw.Restart();
			var collection = scope.ServiceProvider.GetRequiredKeyedService<IServiceCollection>(ServiceKeys.ChatServices);
			foreach (var service in collection)
			{
				if (!service.IsKeyedService && service.Lifetime != ServiceLifetime.Transient && !service.ServiceType.ContainsGenericParameters)
					scope.ServiceProvider.GetServices(service.ServiceType);
			}
			Log.Debug("Chat services activated in {ElapsedMilliseconds} ms", sw.ElapsedMilliseconds);

			return scope;
		}

		public IServiceScope OpenChatScope(int chatId)
		{
			return OpenChatScope(database, chatId);
		}

		public IServiceScope OpenMemoryChat(string title)
		{
			const int memoryChatId = 1;
			var memoryDatabase = new ChatDatabase(null);

			var model = new ChatModel
			{
				Id = memoryChatId,
				Title = title,
				CreatedAt = DateTime.Now,
				LastModifiedAt = DateTime.Now,
				RootNodeId = -1,
				LeafNodeId = -1
			};

			memoryDatabase.Chats.Insert(model);
			return OpenChatScope(memoryDatabase, memoryChatId);
		}

		public void ClearEmptyChats()
		{
			database.Chats.DeleteMany(c => c.LeafNodeId == -1 && c.RootNodeId == -1);
		}

		public ChatInfo CreateChat(string title)
		{
			var model = new ChatModel
			{
				Title = title,
				CreatedAt = DateTime.Now,
				LastModifiedAt = DateTime.Now,
				RootNodeId = -1,
				LeafNodeId = -1
			};

			database.Chats.Insert(model);

			return CreateChatInfo(model);
		}

		public void DeleteChat(int chatId)
		{
			var nodesToDelete = new List<int>();
			var nodesToCheck = database.MessageNodes
				.Find(m => m.IsRootNode && m.ParentId == chatId)
				.Select(m => m.Id)
				.ToList();

			while (nodesToCheck.Count > 0)
			{
				var lastElement = nodesToCheck[nodesToCheck.Count - 1];
				var childNodes = database.MessageNodes
					.Find(m => !m.IsRootNode && m.ParentId == lastElement)
					.Select(m => m.Id)
					.ToList();

				nodesToDelete.Add(lastElement);
				nodesToCheck.RemoveAt(nodesToCheck.Count - 1);
				nodesToCheck.AddRange(childNodes);
			}

			for (int i = 0; i < nodesToDelete.Count; i++)
			{
				DeleteNode(nodesToDelete[i]);
			}

			AdditionalChatDataCollectionSynchronizer.DeleteFromDatabase(database, ChatDataParentKind.Chat, chatId);
			database.Chats.Delete(chatId);
		}

		private void DeleteNode(int nodeId)
		{
			var node = database.MessageNodes.FindById(nodeId);
			if (node == null) return;

			var message = database.Messages.FindById(node.MessageId);
			if (message == null)
			{
				database.MessageNodes.Delete(nodeId);
				return;
			}

			MessageDatabaseSynchronizer.DeleteFromDatabase(database, message.Id);
			database.MessageNodes.Delete(nodeId);
		}
	}
}
