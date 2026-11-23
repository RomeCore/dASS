using System.Diagnostics;
using LLMDesktopAssistant.Data;
using LLMDesktopAssistant.Data.ChatModels;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services.Storage;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;
using LLMDesktopAssistant.Startup;
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
				SubTopic = model.SubTopic,
				Favorite = model.Favorite,
				Pinned = model.Pinned,
				HasUserInputDraft = model.UserInputState?.IsEmpty == false,
				AllMessageCount = database.MessageNodes.Count(m => m.ChatId == model.Id),
				CreatedAt = model.CreatedAt,
				LastModifiedAt = model.LastModifiedAt,
				Parts = [.. database.AdditionalChatData
					.Find(d => d.ParentId == model.Id && d.ParentKind == ChatDataParentKind.Chat && d.IsPart)
					.OrderBy(d => d.Order).ThenBy(d => d.Id)
					.Select(d => d.Data)
					.OfType<AdditionalMessagePart>()]
			};
		}

		public IEnumerable<ChatInfo> GetChats()
		{
			return database.Chats.FindAll().Select(CreateChatInfo).ToList();
		}

		private IServiceScope OpenChatScope(ChatDatabase database, int chatId, bool isTemporary)
		{
			var sw = new Stopwatch();

			sw.Start();
			var scope = services.CreateScope();

			sw.Restart();
			var existingChat = database.Chats.FindById(chatId);

			var chatConfig = scope.ServiceProvider.GetRequiredService<IChatCreationConfig>();
			chatConfig.ChatId = chatId;
			chatConfig.CreatedAt = existingChat?.CreatedAt ?? DateTime.Now;
			chatConfig.Database = database;
			chatConfig.IsTemporary = isTemporary;

			// Ensure at least one default agent exists for this chat
			var chat = scope.ServiceProvider.GetRequiredService<Chat>();
			scope.ServiceProvider.GetRequiredService<IChatSettingsService>().Settings.Agents.EnsureDefaultAgent();

			var storage = scope.ServiceProvider.GetRequiredService<IChatStorageService>();
			storage.Reload();
			Log.Debug("Chat storage reloaded in {ElapsedMilliseconds} ms, message count: {MessageCount}", sw.ElapsedMilliseconds, chat.Messages.Count);

			// Activate all chat-specific services
			sw.Restart();
			var collection = scope.ServiceProvider.GetRequiredKeyedService<IServiceCollection>(ServiceKeys.ChatServices);
			var activatable = collection
				.Where(service => !service.IsKeyedService
					&& service.Lifetime != ServiceLifetime.Transient
					&& !service.ServiceType.ContainsGenericParameters)
				.ToList();

			for (var index = 0; index < activatable.Count; index++)
			{
				var service = activatable[index];
				scope.ServiceProvider.GetServices(service.ServiceType);

				// The splash publishes this on every chat, and the manager is a no-op once the handle
				// is detached - so the formatting only happens while somebody is actually watching.
				if (StartupSplashManager.IsAttached)
					StartupSplashManager.SetStageProgress(
						(double)(index + 1) / activatable.Count,
						Locale.GetFormattedKey(
							"splash.loading_module",
							service.ServiceType.Name,
							$"{index + 1}/{activatable.Count}"));
			}

			Log.Debug("Chat services activated in {ElapsedMilliseconds} ms", sw.ElapsedMilliseconds);

			// Opening handlers run last: every chat service is constructed and the history is loaded, so a hook sees
			// the chat exactly as the first turn will. Nothing waits for them.
			foreach (var opening in scope.ServiceProvider.GetServices<IChatOpeningHandler>())
			{
				try
				{
					opening.OnChatOpened();
				}
				catch (Exception ex)
				{
					Log.Warning(ex, "Failed to run a chat opening handler: {Error}", ex.Message);
				}
			}

			return scope;
		}

		public IServiceScope OpenChatScope(int chatId)
		{
			return OpenChatScope(database, chatId, isTemporary: false);
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
			return OpenChatScope(memoryDatabase, memoryChatId, isTemporary: true);
		}

		public void ClearEmptyChats()
		{
			var potential = database.Chats.Find(c => c.LeafNodeId == -1 && c.RootNodeId == -1 && !c.Pinned).ToList();
			foreach (var chat in potential)
				if (chat.UserInputState is null || chat.UserInputState.IsEmpty)
					database.Chats.Delete(chat.Id);
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

		/// <inheritdoc />
		public void UpdateChat(int chatId,
			string? title = null,
			string? topic = null,
			string? subTopic = null,
			bool? favorite = null,
			bool? pinned = null)
		{
			var model = database.Chats.FindById(chatId);
			if (model is null)
				return;

			model.Title = title ?? model.Title;
			model.Topic = topic ?? model.Topic;
			model.SubTopic = subTopic ?? model.SubTopic;
			model.Favorite = favorite ?? model.Favorite;
			model.Pinned = pinned ?? model.Pinned;
			database.Chats.Update(model);
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
