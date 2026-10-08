using System.Collections.Specialized;
using System.ComponentModel;
using LLMDesktopAssistant.Data;
using LLMDesktopAssistant.Data.ChatModels;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using RCLargeLanguageModels.Tasks;

namespace LLMDesktopAssistant.LLM.Services.Storage
{

	/// <summary>
	/// Synchronizes ONE chat message (domain object) with its <see cref="MessageModel"/> and everything
	/// nested inside it (tool calls with their additional view models, message additional view models).
	/// Created separately for each message, owner is <see cref="IChatStorageService"/>.
	/// </summary>
	public class MessageDatabaseSynchronizer : Disposable
	{
		private readonly ChatDatabase _database;
		private readonly MessageModel _model;
		private readonly Dictionary<ToolCall, ToolCallDatabaseSynchronizer> _toolCallSyncs = [];
		private readonly AdditionalChatDataCollectionSynchronizer _additionalDataSync;

		/// <summary>
		/// The ID of the <see cref="MessageModel"/> where data is stored in the database.
		/// </summary>
		public int ModelId => _model.Id;

		/// <summary>
		/// The domain object being synchronized.
		/// </summary>
		public ChatMessage Target { get; }

		public static MessageDatabaseSynchronizer FromTarget(ChatDatabase database, ChatMessage target)
		{
			var model = new MessageModel();
			CopyToModel(model, target);
			database.Messages.Insert(model);
			var toolCallSyncs = target.ToolCalls.Select(t => ToolCallDatabaseSynchronizer.FromTarget
				(database, t, model.Id)).ToDictionary(t => t.Target);
			var additionalDataSync = AdditionalChatDataCollectionSynchronizer.FromTarget(database, target, ChatDataParentKind.Message, model.Id);
			return new MessageDatabaseSynchronizer(database, target, model, toolCallSyncs, additionalDataSync);
		}
		
		public static MessageDatabaseSynchronizer FromModel(ChatDatabase database, MessageModel model)
		{
			var toolCalls = database.ToolCalls.Find(t => t.MessageId == model.Id).OrderBy(d => d.Order).ThenBy(d => d.Id);
			var toolCallSyncs = toolCalls.Select(t => ToolCallDatabaseSynchronizer.FromModel(database, t)).ToDictionary(t => t.Target);
			var target = CreateFromModel(model);
			target.ToolCalls.Reset(toolCallSyncs.Keys);
			var additionalDataSync = AdditionalChatDataCollectionSynchronizer.FromOwnerModels(database, target, ChatDataParentKind.Message, model.Id);
			return new MessageDatabaseSynchronizer(database, target, model, toolCallSyncs, additionalDataSync);
		}

		private MessageDatabaseSynchronizer(ChatDatabase database, ChatMessage target, MessageModel model,
			Dictionary<ToolCall, ToolCallDatabaseSynchronizer> toolCallSyncs,
			AdditionalChatDataCollectionSynchronizer additionalDataSync)
		{
			_database = database;
			Target = target;
			_model = model;
			_toolCallSyncs = toolCallSyncs;
			_additionalDataSync = additionalDataSync;

			Target.PropertyChanged += OnTargetPropertyChanged;
			Target.ToolCalls.CollectionChanged += OnToolCallsCollectionChanged;
		}

		private void OnTargetPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			CopyToModel(_model, Target);
			_database.Messages.Update(_model);
		}

		private void OnToolCallsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.OldItems != null)
				foreach (ToolCall oldToolCall in e.OldItems)
					if (_toolCallSyncs.Remove(oldToolCall, out var sync))
						sync.Delete();

			if (e.NewItems != null)
				foreach (ToolCall newToolCall in e.NewItems)
					_toolCallSyncs[newToolCall] = ToolCallDatabaseSynchronizer.FromTarget(_database, newToolCall, _model.Id);

			int i = 0;
			foreach (var toolCall in Target.ToolCalls)
				if (_toolCallSyncs.TryGetValue(toolCall, out var sync))
					sync.UpdateOrder(i++);
		}

		private static ChatMessage CreateFromModel(MessageModel model)
		{
			return model.Role switch
			{
				RoleModel.User => new UserMessage
				{
					CreatedAt = model.CreatedAt,
					Content = model.Content,
					SenderLogin = model.Sender,
					Visibility = model.Visibility,
					IsRevealed = model.IsRevealed,
					VisibleTo = model.VisibleTo,
					IsVisibleToWhiteList = model.IsVisibleToWhiteList,
					Error = model.Error,
					IsDisabledForAgents = model.IsDisabledForAgents
				},
				RoleModel.Assistant => new AssistantMessage
				{
					CreatedAt = model.CreatedAt,
					SenderAgentId = Guid.TryParse(model.Sender, out var senderAgent) ? senderAgent : Guid.Empty,
					AgentStageId = model.AgentStageId,
					IsUserLike = model.IsUserLike,
					ReasoningContent = model.ReasoningContent,
					Content = model.Content,
					Error = model.Error,
					IsDisabledForAgents = model.IsDisabledForAgents,
					Status = model.Status switch
					{
						MessageStatusModel.Pending => AssistantMessageStatus.Pending,
						MessageStatusModel.Success => AssistantMessageStatus.Success,
						MessageStatusModel.Error => AssistantMessageStatus.Error,
						MessageStatusModel.Cancelled => AssistantMessageStatus.Cancelled,
						_ => AssistantMessageStatus.Pending
					},
					CompletionToken = CompletionToken.Success
				},
				_ => throw new ArgumentOutOfRangeException(nameof(model.Role), "Invalid role"),
			};
		}

		/// <summary>
		/// Copies the current domain values to the model. Called on every property change.
		/// </summary>
		private static void CopyToModel(MessageModel model, ChatMessage from)
		{
			model.CreatedAt = from.CreatedAt;
			model.Content = from.Content;
			model.Error = from.Error;
			model.IsDisabledForAgents = from.IsDisabledForAgents;

			switch (from)
			{
				case UserMessage userMessage:
					model.Role = RoleModel.User;
					model.Sender = userMessage.SenderLogin;
					model.Visibility = userMessage.Visibility;
					model.IsRevealed = userMessage.IsRevealed;
					model.VisibleTo = userMessage.VisibleTo;
					model.IsVisibleToWhiteList = userMessage.IsVisibleToWhiteList;
					break;

				case AssistantMessage assistantMessage:
					model.Role = RoleModel.Assistant;
					model.Sender = assistantMessage.SenderAgentId.ToString();
					model.AgentStageId = assistantMessage.AgentStageId;
					model.IsUserLike = assistantMessage.IsUserLike;
					model.ReasoningContent = assistantMessage.ReasoningContent;
					model.Status = assistantMessage.Status switch
					{
						AssistantMessageStatus.Pending => MessageStatusModel.Pending,
						AssistantMessageStatus.Success => MessageStatusModel.Success,
						AssistantMessageStatus.Error => MessageStatusModel.Error,
						AssistantMessageStatus.Cancelled => MessageStatusModel.Cancelled,
						_ => MessageStatusModel.Pending
					};
					break;
			}
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				Target.PropertyChanged -= OnTargetPropertyChanged;
				Target.ToolCalls.CollectionChanged -= OnToolCallsCollectionChanged;

				foreach (var sync in _toolCallSyncs.Values)
					sync.Dispose();
				_toolCallSyncs.Clear();

				_additionalDataSync.Dispose();
			}
		}

		/// <summary>
		/// Removes the message and ALL its nested entities (tool calls, their additional view models,
		/// message additional view models) from the database. Disposes this synchronizer.
		/// Called when the node with this message is deleted.
		/// </summary>
		public void Delete()
		{
			DeleteFromDatabase(_database, _model.Id);

			Dispose();
		}

		/// <summary>
		/// Removes the message row and ALL its nested rows (tool calls with their additional view models,
		/// message additional view models) from the database. Used when no domain object / synchronizer
		/// exists for the message (for example, when deleting whole side branches that are not loaded).
		/// </summary>
		public static void DeleteFromDatabase(ChatDatabase database, int messageId)
		{
			foreach (var toolCall in database.ToolCalls.Find(t => t.MessageId == messageId).ToList())
				database.AdditionalChatData.DeleteMany(avm => avm.ParentKind == ChatDataParentKind.ToolCall && avm.ParentId == toolCall.Id);
			database.ToolCalls.DeleteMany(t => t.MessageId == messageId);
			AdditionalChatDataCollectionSynchronizer.DeleteFromDatabase(database, ChatDataParentKind.Message, messageId);

			database.Messages.Delete(messageId);
		}
	}
}
