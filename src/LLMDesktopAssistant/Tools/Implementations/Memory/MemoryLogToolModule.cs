using System.ComponentModel;
using System.Text;
using LLMDesktopAssistant.Agents.Memory;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Localization;
using Material.Icons;

namespace LLMDesktopAssistant.Tools.Implementations.Memory
{
	/// <summary>
	/// Provides tools for appending, searching, viewing and deleting episodic memory logs
	/// in the memory blocks attached to the sender agent.
	/// </summary>
	[ToolModule]
	public class MemoryLogToolModule : MemoryToolModuleBase
	{
		private readonly IMemoryLogStore _memoryLogStore;

		/// <summary>
		/// Initializes a new instance of the <see cref="MemoryLogToolModule"/> class.
		/// </summary>
		/// <param name="chat">The chat instance where the tools are executed.</param>
		/// <param name="chatSettings">The settings service of the chat.</param>
		/// <param name="agentManager">The agent management service used to resolve agent memory attachments.</param>
		/// <param name="memoryLogStore">The store used to access episodic memory logs.</param>
		public MemoryLogToolModule(Chat chat, IChatSettingsService chatSettings, IAgentManagementService agentManager,
			IMemoryLogStore memoryLogStore)
			: base(chat, chatSettings, agentManager)
		{
			_memoryLogStore = memoryLogStore;

			AddTool(new ToolInitializationInfo
			{
				Executor = AppendAsync,
				Name = "memory-append_log",
				Description = """
					Appends a new episodic log entry to the specified memory block.
					Logs are immutable: they cannot be edited, only deleted. The log text is added to the keyword search index.
					""",
				DefaultExpectedBehaviour = ToolBehaviour.SemanticMemoryWrite,
				NameKey = Locale.GetKey("tool.name.memory-append_log"),
				DescriptionKey = Locale.GetKey("tool.description.memory-append_log"),
				CategoryKey = Locale.GetKey("tool.category.memory")
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = SearchAsync,
				Name = "memory-search_log",
				Description = """
					Searches active episodic logs in the specified memory blocks (or all enabled blocks) using BM25 keyword search.
					Transient, consolidated and ignored logs are excluded from search results.
					""",
				DefaultExpectedBehaviour = ToolBehaviour.SemanticMemoryRead,
				NameKey = Locale.GetKey("tool.name.memory-search_log"),
				DescriptionKey = Locale.GetKey("tool.description.memory-search_log"),
				CategoryKey = Locale.GetKey("tool.category.memory")
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = ViewLogsAsync,
				Name = "memory-view_logs",
				Description = """
					Views episodic logs of the specified memory blocks (or all enabled blocks).
					These logs are used to store events (such as such as game, DnD or development history).
					Logs can be filtered by a real-time window and/or an alternative timeline ordinal window.
					When no window is specified, the most recent logs are returned. Logs are ordered by their begin time, newest first.
					""",
				DefaultExpectedBehaviour = ToolBehaviour.SemanticMemoryRead,
				NameKey = Locale.GetKey("tool.name.memory-view_logs"),
				DescriptionKey = Locale.GetKey("tool.description.memory-view_logs"),
				CategoryKey = Locale.GetKey("tool.category.memory")
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = DeleteAsync,
				Name = "memory-delete_log",
				Description = """
					Permanently deletes an episodic log from the specified memory block by its ID.
					The log is removed from the database and the keyword index and cannot be restored.
					""",
				DefaultExpectedBehaviour = ToolBehaviour.SemanticMemoryDelete,
				NameKey = Locale.GetKey("tool.name.memory-delete_log"),
				DescriptionKey = Locale.GetKey("tool.description.memory-delete_log"),
				CategoryKey = Locale.GetKey("tool.category.memory")
			});
		}

		private async Task<ToolExecutionResult> AppendAsync(
			[Description("The memory block where the log should be appended")] string block,
			[Description("The text of the log describing what happened. Must not be empty.")] string text,
			[Description("The importance of the log, from 0.0 (least important) to 1.0 (most important)")] double importance,
			IToolExecutionContext ctx,
			[Description("The real-time timestamp when the log began in UTC ISO 8601 format (e.g. 2026-08-10T15:00:00Z). Defaults to now.")] DateTime? timeStampBegin = null,
			[Description("The real-time timestamp when the log ended in UTC ISO 8601 format (e.g. 2026-08-10T15:00:00Z). Defaults to timeStampBegin.")] DateTime? timeStampEnd = null,
			[Description("The alternative timeline ordinal when the log began (for example, the day number)")] double timeLineOrdinalBegin = 0,
			[Description("The alternative timeline details when the log began (for example, \"Day 3, 14:00\")")] string timeLineDetailsBegin = "",
			[Description("The alternative timeline ordinal when the log ended. Defaults to the begin ordinal.")] double? timeLineOrdinalEnd = null,
			[Description("The alternative timeline details when the log ended. Defaults to the begin details.")] string? timeLineDetailsEnd = null,
			CancellationToken cancellationToken = default)
		{
			ctx.Call.SetStatus(MaterialIconKind.DatabaseAdd, $"**{text}** → *{block}*");

			var (targetBlock, error) = GetBlock(ctx, block, requireReading: false, requireWriting: true, requireLogs: true);
			if (targetBlock is null)
			{
				ctx.Call.SetResult(error ?? $"Memory block '{block}' is not available.");
				return ToolExecutionResult.Failed;
			}

			try
			{
				// A script invocation has no owning message; the log then records no message link.
				int messageId = ctx is ChatToolExecutionContext chatContext ? chatContext.FindMessageId() : 0;

				var log = await _memoryLogStore.AppendAsync(
					targetBlock,
					text,
					timeStampBegin: timeStampBegin,
					timeStampEnd: timeStampEnd,
					timeLineOrdinalBegin: timeLineOrdinalBegin,
					timeLineDetailsBegin: timeLineDetailsBegin,
					timeLineOrdinalEnd: timeLineOrdinalEnd ?? timeLineOrdinalBegin,
					timeLineDetailsEnd: string.IsNullOrEmpty(timeLineDetailsEnd) ? timeLineDetailsBegin : timeLineDetailsEnd,
					sourceChatId: Chat.Id,
					sourceMessageId: messageId,
					importance: importance,
					cancellationToken: cancellationToken);

				ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseCheck);
				ctx.Call.SetResult($"Log appended successfully. ID: {log.Id}");
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseOff);
				ctx.Call.SetResult($"Failed to append log to memory block '{block}'. Error: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private async Task<ToolExecutionResult> SearchAsync(
			[Description("The memory blocks where the logs should be searched. Use null to search in all enabled memory blocks")] string[]? blocks,
			[Description("The search query text")] string query,
			IToolExecutionContext ctx,
			[Description("The maximum number of logs to return")] int maxCount = 5,
			[Description("The minimum importance score of the logs. Logs with lower scores are excluded.")] double minImportance = 0.0,
			CancellationToken cancellationToken = default)
		{
			ctx.Call.SetStatus(MaterialIconKind.DatabaseSearch, $"**{query}**");

			var resolution = GetBlocks(ctx, blocks, requireReading: true, requireWriting: false, requireLogs: true);

			if (!resolution.HasBlocks)
			{
				ctx.Call.SetResult(resolution.BuildError("No memory blocks with logs enabled are available to search in."));
				return ToolExecutionResult.Failed;
			}

			try
			{
				var perBlockResults = await Task.WhenAll(resolution.Blocks.Select(block =>
					_memoryLogStore.SearchAsync(block, query, minImportance: minImportance, maxCount, cancellationToken)));

				var sb = new StringBuilder();
				int totalFound = 0;

				for (int i = 0; i < resolution.Blocks.Count; i++)
				{
					var block = resolution.Blocks[i];
					var logs = perBlockResults[i];
					if (logs.Length == 0)
						continue;

					totalFound += logs.Length;
					sb.AppendLine($"### {block.Name}");
					foreach (var log in logs)
					{
						var bm25Score = log.Bm25Score?.ToString("0.00") ?? "unknown";
						sb.AppendLine($"- **{log.Text}** [id: {log.Id}] (bm25 score: {bm25Score}, importance: {log.Importance:0.00}, {log.TimeStampBegin:yyyy-MM-dd HH:mm} UTC)");
					}
					sb.AppendLine();
				}
				if (resolution.Excluded.Count > 0)
				{
					sb.AppendLine("Note: some blocks were excluded from search.");
					sb.AppendJoin(' ', resolution.Excluded.Values);
				}

				if (totalFound == 0)
				{
					ctx.Call.SetResult("No matching logs found.");
					return ToolExecutionResult.Succeeded;
				}

				ctx.Call.UseMarkdown = true;
				ctx.Call.SetResult(sb.ToString());
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseOff);
				ctx.Call.SetResult($"Failed to search logs. Error: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private async Task<ToolExecutionResult> ViewLogsAsync(
			[Description("The memory blocks where the logs should be viewed. Use null to view logs in all enabled memory blocks")] string[]? blocks,
			IToolExecutionContext ctx,
			[Description("The inclusive lower bound of the real-time window in UTC ISO 8601 format (e.g. 2026-08-10T15:00:00Z)")] DateTime? from = null,
			[Description("The inclusive upper bound of the real-time window in UTC ISO 8601 format (e.g. 2026-08-10T15:00:00Z)")] DateTime? to = null,
			[Description("The inclusive lower bound of the alternative timeline ordinal window (for example, the day number)")] double? timeLineFrom = null,
			[Description("The inclusive upper bound of the alternative timeline ordinal window")] double? timeLineTo = null,
			[Description("The maximum number of logs to return. When no time window is specified, the most recent logs are returned.")] int maxCount = 20,
			[Description("The minimum importance score of the logs. Logs with lower scores are excluded.")] double minImportance = 0.0,
			CancellationToken cancellationToken = default)
		{
			ctx.Call.SetStatus(MaterialIconKind.DatabaseSearch,
				from.HasValue || to.HasValue || timeLineFrom.HasValue || timeLineTo.HasValue
					? LocalizationManager.LocalizeStatic("tool.status.memory-view_logs.time_window")
					: LocalizationManager.LocalizeStaticFormat("tool.status.memory-view_logs.latest", maxCount));

			var resolution = GetBlocks(ctx, blocks, requireReading: true, requireWriting: false, requireLogs: true);

			if (!resolution.HasBlocks)
			{
				ctx.Call.SetResult(resolution.BuildError("No memory blocks with logs enabled are available to view."));
				return ToolExecutionResult.Failed;
			}

			try
			{
				var perBlockResults = await Task.WhenAll(resolution.Blocks.Select(block =>
					_memoryLogStore.GetByTimeAsync(block, from, to, timeLineFrom, timeLineTo, minImportance, maxCount, cancellationToken)));

				var sb = new StringBuilder();

				for (int i = 0; i < resolution.Blocks.Count; i++)
				{
					var block = resolution.Blocks[i];
					var logs = perBlockResults[i];
					if (logs.Length == 0)
						continue;

					sb.AppendLine($"### {block.Name}");
					foreach (var log in logs)
					{
						var timeline = string.IsNullOrEmpty(log.TimeLineDetailsBegin) ? "" : $", timeline: {log.TimeLineDetailsBegin}";
						sb.AppendLine($"- {log.Text} [id: {log.Id}] ({log.TimeStampBegin:yyyy-MM-dd HH:mm} UTC, status: {log.Status}, importance: {log.Importance:0.00}{timeline})");
					}
					sb.AppendLine();
				}

				if (sb.Length == 0)
					sb.AppendLine("No logs found in the specified time window.");

				if (resolution.Excluded.Count > 0)
				{
					sb.AppendLine("Note: some blocks were excluded from search.");
					sb.AppendJoin(' ', resolution.Excluded.Values);
				}

				ctx.Call.UseMarkdown = true;
				ctx.Call.SetResult(sb.ToString());
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseOff);
				ctx.Call.SetResult($"Failed to view logs. Error: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private async Task<ToolExecutionResult> DeleteAsync(
			[Description("The memory block that contains the log")] string block,
			[Description("The ID of the log to delete")] int logId,
			IToolExecutionContext ctx,
			CancellationToken cancellationToken = default)
		{
			ctx.Call.SetStatus(MaterialIconKind.DatabaseRemove, $"**{block}** [id: {logId}]");

			var (targetBlock, error) = GetBlock(ctx, block, requireReading: false, requireWriting: true, requireLogs: true);
			if (targetBlock is null)
			{
				ctx.Call.SetResult(error ?? $"Memory block '{block}' is not available.");
				return ToolExecutionResult.Failed;
			}

			try
			{
				await _memoryLogStore.HardDeleteAsync(targetBlock, logId, cancellationToken);

				ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseCheck);
				ctx.Call.SetResult($"Log [id: {logId}] permanently deleted from memory block '{block}'.");
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseOff);
				ctx.Call.SetResult($"Failed to delete log [id: {logId}] in memory block '{block}'. Error: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}
	}
}
