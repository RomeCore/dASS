using System.Text;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Providers;
using LLMDesktopAssistant.Utils;
using Serilog;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// Service that automatically generates titles and topics for chat conversations.
	/// Uses a dedicated LLM call with a specialized prompt template.
	/// Title and Topic are set directly on the Chat, which is reactive (NotifyPropertyChanged),
	/// so the UI and database are updated automatically through the existing infrastructure.
	/// </summary>
	[ChatService(typeof(IChatNamingService))]
	[ChatService(typeof(IChatExecutionHook))]
	public class ChatNamingService(
		Chat chat,
		IChatSettingsService chatSettings,
		IAgentTaskExecutor agentTaskExecutor,
		IModelManager modelManager,
		IChatMessageQuoteRenderer quoteRenderer,
		ITemplateLibraryAccessor templates,
		MessagesInterface messagesInterface
		) : IChatNamingService, IChatExecutionHook
	{
		private readonly SemaphoreSlim _semaphore = new(1, 1);

		/// <inheritdoc />
		public Task OnExecutionFinishedAsync(Chat chat, CancellationToken cancellationToken = default)
		{
			return TryNameChatAsync(cancellationToken);
		}

		public async Task TryNameChatAsync(CancellationToken cancellationToken = default)
		{
			try
			{
				if (!IsDefaultTitle(chat.Title))
					return;

				var rounds = messagesInterface.GroupMessagesIntoRounds();
				if (rounds.Count < 1)
					return;

				await NameChatAsync(cancellationToken);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to auto-name chat: {Error}", ex.Message);
			}
		}

		public async Task<bool> NameChatAsync(CancellationToken cancellationToken = default)
		{
			if (_semaphore.CurrentCount == 0) return false; // Already renaming, skip this call
			await _semaphore.WaitAsync(cancellationToken);
			
			try
			{
				var namingLLM = modelManager.TryGetModel(chatSettings.Settings.Memory.GetEffectiveSummarization().SummarizerModel);
				if (namingLLM == null)
				{
					Log.Warning("Cannot name chat: no naming LLM available.");
					return false;
				}

				var namingTemplate = templates.GetTextTemplate("naming_prompt");
				var namingPrompt = namingTemplate.Render();
				var namingInput = BuildNamingInput();

				var namingTask = agentTaskExecutor.Execute(new AgentTaskLaunchParameters
				{
					TaskName = LocalizationManager.LocalizeStatic("chat.naming.task"),
					TriggeredChat = chat,
					Model = namingLLM,
					InitialMessages = [
						new AgentSystemMessage { Content = namingPrompt },
						new AgentUserMessage { Content = namingInput }
					]
				});
				await namingTask;

				var content = MarkdownCodeBlockExtractor.TryExtract(namingTask.LastGeneratedContent ?? string.Empty);
				if (string.IsNullOrWhiteSpace(content))
				{
					Log.Warning("Chat naming returned empty result.");
					return false;
				}

				string? title = null;
				string? topic = null;

				try
				{
					var json = System.Text.Json.JsonDocument.Parse(content);
					var root = json.RootElement;

					if (root.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == System.Text.Json.JsonValueKind.String)
						title = titleEl.GetString();

					if (root.TryGetProperty("topic", out var topicEl) && topicEl.ValueKind == System.Text.Json.JsonValueKind.String)
						topic = topicEl.GetString();
				}
				catch (System.Text.Json.JsonException)
				{
					title = content.Trim();
				}

				if (!string.IsNullOrWhiteSpace(title))
				{
					chat.Title = title.Trim();
				}

				if (!string.IsNullOrWhiteSpace(topic))
				{
					chat.Topic = topic.Trim();
				}

				return true;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to name chat: {Error}", ex.Message);

				return false;
			}
			finally
			{
				_semaphore.Release();
			}
		}

		private string BuildNamingInput()
		{
			var sb = new StringBuilder();
			sb.AppendLine("Generate a title and topic for this conversation:");

			var rounds = messagesInterface.GroupMessagesIntoRounds(3);
			foreach (var round in rounds)
			{
				foreach (var branched in round)
				{
					var rendered = quoteRenderer.Render(branched.Message).Content;
					if (!string.IsNullOrWhiteSpace(rendered))
					{
						sb.AppendLine(rendered);
					}
				}
			}

			sb.AppendLine();
			sb.AppendLine("---");
			sb.AppendLine("Respond with a JSON object containing 'title' (max 60 chars) and 'topic' (short category like 'coding', 'writing', 'roleplay', 'dnd', etc.).");
			sb.AppendLine("Example: {\"title\": \"Fixing the login bug\", \"topic\": \"coding\"}");

			return sb.ToString();
		}

		private static bool IsDefaultTitle(string title)
		{
			if (string.IsNullOrWhiteSpace(title))
				return true;

			var defaultTitle = LocalizationManager.LocalizeStatic("chat.new");
			if (string.Equals(title, defaultTitle, StringComparison.OrdinalIgnoreCase))
				return true;

			var defaultTemporaryTitle = LocalizationManager.LocalizeStatic("chat.new_temporary");
			if (string.Equals(title, defaultTemporaryTitle, StringComparison.OrdinalIgnoreCase))
				return true;

			return false;
		}
	}
}
