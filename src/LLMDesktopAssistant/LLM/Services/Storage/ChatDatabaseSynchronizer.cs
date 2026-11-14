using System.ComponentModel;
using LLMDesktopAssistant.Data;
using LLMDesktopAssistant.Data.ChatModels;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Settings;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.Services.Storage
{
	/// <summary>
	/// Synchronizes the chat itself (domain object) with its <see cref="ChatModel"/> row:
	/// title, topic, settings profile and the persisted <see cref="Chat.UserInputState"/>.
	/// Owned by <see cref="IChatStorageService"/>, created once per chat scope.
	/// </summary>
	public class ChatDatabaseSynchronizer : Disposable
	{
		private readonly ChatDatabase _database;
		private readonly IChatSettingsService _chatSettings;
		private readonly AdditionalChatDataCollectionSynchronizer _additionalDataSynchronizer;
		private ChangeTracker? _userInputTracker;

		/// <summary>
		/// The chat domain object being synchronized.
		/// </summary>
		public Chat Target { get; }

		/// <summary>
		/// The database model of the chat.
		/// </summary>
		public ChatModel Model { get; }

		/// <summary>
		/// Loads (or creates when missing) the chat model, applies its values to the domain object
		/// and subscribes to all changes.
		/// </summary>
		public ChatDatabaseSynchronizer(ChatDatabase database, Chat target,
			IChatSettingsService chatSettings, IServiceProvider services)
		{
			_database = database;
			Target = target;
			_chatSettings = chatSettings;

			var model = database.Chats.FindById(target.Id);
			if (model == null)
			{
				// The chat is not stored yet (e.g. an in-memory chat): create the row.
				model = new ChatModel
				{
					Id = target.Id,
					Title = target.Title,
					Topic = target.Topic,
					RootNodeId = -1,
					LeafNodeId = -1,
					SettingsProfile = chatSettings.Settings.Id,
					CreatedAt = target.CreatedAt == DateTime.MinValue ? DateTime.Now : target.CreatedAt,
					LastModifiedAt = DateTime.Now
				};
				database.Chats.Insert(model);
				target.Id = model.Id;
			}
			else
			{
				// Load stored values into the domain.
				target.Title = model.Title;
				target.Topic = model.Topic;
				chatSettings.LoadFromProfile(model.SettingsProfile);
			}

			_additionalDataSynchronizer = AdditionalChatDataCollectionSynchronizer.FromOwnerModels(database,
				target, ChatDataParentKind.Chat, target.Id, services);
			Model = model;

			// Attach the persisted input state: the same object lives in both the domain and the model,
			// so its changes are saved by the change tracker below.
			target.UserInputState = model.UserInputState ??= new UserInputState();

			target.PropertyChanged += OnChatPropertyChanged;
			AttachUserInputTracker();
		}

		/// <summary>
		/// Updates the database model with the current state of the chat.
		/// </summary>
		public void UpdateModel()
		{
			_database.Chats.Update(Model);
		}

		/// <summary>
		/// Re-reads the committed tree state (root/leaf nodes and last modified date) from the database.
		/// Must be called when a storage transaction has been rolled back: the cached model must not
		/// keep values that were never committed.
		/// </summary>
		public void RestoreTreeState()
		{
			var model = _database.Chats.FindById(Model.Id);
			if (model == null)
				return;

			Model.RootNodeId = model.RootNodeId;
			Model.LeafNodeId = model.LeafNodeId;
			Model.LastModifiedAt = model.LastModifiedAt;
		}

		/// <summary>
		/// Called on every chat property change: copies the mutable chat values into the model.
		/// </summary>
		private void OnChatPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			Model.Title = Target.Title;
			Model.Topic = Target.Topic;
			Model.SettingsProfile = _chatSettings.Settings.Id;

			_database.Chats.Update(Model);

			// When the whole input state is replaced, re-attach the tracker to the new object.
			if (e.PropertyName == nameof(Chat.UserInputState))
			{
				Model.UserInputState = Target.UserInputState;
				_database.Chats.Update(Model);
				AttachUserInputTracker();
			}
		}

		/// <summary>
		/// Deeply tracks the <see cref="Chat.UserInputState"/> (text, parts collection, etc.)
		/// and saves the chat row on every change.
		/// </summary>
		private void AttachUserInputTracker()
		{
			_userInputTracker?.Dispose();
			_userInputTracker = new ChangeTracker(Target.UserInputState, () =>
			{
				Model.UserInputState = Target.UserInputState;
				_database.Chats.Update(Model);
			});
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				Target.PropertyChanged -= OnChatPropertyChanged;
				_additionalDataSynchronizer.Dispose();
				_userInputTracker?.Dispose();
				_userInputTracker = null;
			}
		}
	}
}
