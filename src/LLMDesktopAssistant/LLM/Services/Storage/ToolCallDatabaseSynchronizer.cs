using System.ComponentModel;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Data;
using LLMDesktopAssistant.Data.ChatModels;
using LLMDesktopAssistant.LLM.Domain;
using RCLargeLanguageModels.Tasks;

namespace LLMDesktopAssistant.LLM.Services.Storage
{
	/// <summary>
	/// Synchronizes one tool call (domain object) with its <see cref="ToolCallModel"/> row and
	/// with the additional view models nested inside it (<see cref="ChatObjectBase.AdditionalData"/>).
	/// Created per tool call by <see cref="MessageDatabaseSynchronizer"/>.
	/// </summary>
	public class ToolCallDatabaseSynchronizer : Disposable
	{
		private readonly ChatDatabase _database;
		private readonly ToolCallModel _model;
		private readonly AdditionalChatDataCollectionSynchronizer _additionalDataSync;

		/// <summary>
		/// The domain object being synchronized.
		/// </summary>
		public ToolCall Target { get; }

		public static ToolCallDatabaseSynchronizer FromTarget(ChatDatabase database, ToolCall target, int messageId)
		{
			var model = new ToolCallModel
			{
				MessageId = messageId
			};
			CopyToModel(model, target);
			database.ToolCalls.Insert(model);
			var additionalDataSync = AdditionalChatDataCollectionSynchronizer.FromTarget(database, target, ChatDataParentKind.ToolCall, model.Id);
			return new ToolCallDatabaseSynchronizer(database, target, model, additionalDataSync);
		}

		public static ToolCallDatabaseSynchronizer FromModel(ChatDatabase database, ToolCallModel model)
		{
			var target = CreateFromModel(model);
			var additionalDataSync = AdditionalChatDataCollectionSynchronizer.FromOwnerModels(database, target, ChatDataParentKind.ToolCall, model.Id);
			return new ToolCallDatabaseSynchronizer(database, target, model, additionalDataSync);
		}

		private ToolCallDatabaseSynchronizer(ChatDatabase database, ToolCall target,
			ToolCallModel model, AdditionalChatDataCollectionSynchronizer additionalDataSync)
		{
			_database = database;
			Target = target;
			_model = model;
			_additionalDataSync = additionalDataSync;

			target.PropertyChanged += OnTargetPropertyChanged;
		}

		private void OnTargetPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			CopyToModel(_model, Target);
			_database.ToolCalls.Update(_model);
		}

		/// <summary>
		/// Updates the order of the tool call in the database.
		/// Used for later correct ordering of tool calls when loading the chat history.
		/// </summary>
		/// <param name="order">The new order of the tool call.</param>
		public void UpdateOrder(int order)
		{
			if (order != _model.Order)
			{
				_model.Order = order;
				_database.ToolCalls.Update(_model);
			}
		}

		private static ToolCall CreateFromModel(ToolCallModel model)
		{
			return new ToolCall
			{
				ToolCallId = model.ToolCallId,
				ToolName = model.ToolName,
				Title = model.Title,
				Arguments = model.FunctionArguments,
				ResultContent = model.ResultContent,
				CanBeCompacted = model.CanBeCompacted,
				UseMarkdown = model.UseMarkdown,
				StructuredResult = string.IsNullOrEmpty(model.StructuredResult) ? null
					: JsonNode.Parse(model.StructuredResult),
				Status = model.Status switch
				{
					ToolStatusModel.NotExecuted => ToolStatus.None,
					ToolStatusModel.ExecutionBegin => ToolStatus.ExecutionInterrupted,
					ToolStatusModel.Success => ToolStatus.Success,
					ToolStatusModel.Cancelled => ToolStatus.Cancelled,
					ToolStatusModel.Error => ToolStatus.Error,
					_ => ToolStatus.None
				},
				StatusIcon = model.StatusIcon,
				StatusTitle = model.StatusTitle,
				ExpectedBehaviour = model.ExpectedBehaviour
			};
		}

		private static void CopyToModel(ToolCallModel model, ToolCall from)
		{
			model.ToolCallId = from.ToolCallId;
			model.ToolName = from.ToolName;
			model.Title = from.Title;
			model.FunctionArguments = from.Arguments;
			model.Status = from.Status switch
			{
				ToolStatus.None => ToolStatusModel.NotExecuted,
				ToolStatus.Executing => ToolStatusModel.ExecutionBegin,
				ToolStatus.Success => ToolStatusModel.Success,
				ToolStatus.Error => ToolStatusModel.Error,
				ToolStatus.Cancelled => ToolStatusModel.Cancelled,
				_ => ToolStatusModel.NotExecuted,
			};
			model.StatusIcon = from.StatusIcon;
			model.StatusTitle = from.StatusTitle;
			model.ExpectedBehaviour = from.ExpectedBehaviour;
			model.ResultContent = from.ResultContent;
			model.CanBeCompacted = from.CanBeCompacted;
			model.UseMarkdown = from.UseMarkdown;
			model.StructuredResult = from.StructuredResult?.ToJsonString();
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				Target.PropertyChanged -= OnTargetPropertyChanged;
				_additionalDataSync.Dispose();
			}
		}

		/// <summary>
		/// Removes the tool call row and all its additional view model rows from the database.
		/// Disposes this synchronizer.
		/// </summary>
		public void Delete()
		{
			ThrowIfDisposed();

			_database.ToolCalls.Delete(_model.Id);
			_additionalDataSync.DeleteAll();

			Dispose();
		}
	}
}
