using System.ComponentModel;
using System.Text;
using LLMDesktopAssistant.Agents.Tasks;

namespace LLMDesktopAssistant.Agents.Memory
{
	/// <summary>
	/// Creates the delegate tools used by the automatic memory recording and retrieval agents
	/// and by agent tasks. The tools operate on the pre-resolved memory blocks
	/// and do not require user confirmation.
	/// </summary>
	public sealed class AgentMemoryTools
	{
		private readonly IMemoryFactStore _factStore;
		private readonly IMemoryLogStore _logStore;
		private readonly int _sourceChatId;
		private readonly int _sourceMessageId;
		private readonly IReadOnlyList<MemoryBlock> _readableFactBlocks;
		private readonly IReadOnlyList<MemoryBlock> _writableFactBlocks;
		private readonly IReadOnlyList<MemoryBlock> _readableLogBlocks;
		private readonly IReadOnlyList<MemoryBlock> _writableLogBlocks;

		/// <summary>
		/// Initializes a new instance with a single block list used for both reading and writing.
		/// Intended for the automatic memory hooks that pre-filter blocks by access mode.
		/// </summary>
		public AgentMemoryTools(IMemoryFactStore factStore, IMemoryLogStore logStore, int sourceChatId,
			IReadOnlyList<MemoryBlock> factBlocks, IReadOnlyList<MemoryBlock> logBlocks, int sourceMessageId)
			: this(factStore, logStore, sourceChatId, factBlocks, factBlocks, logBlocks, logBlocks, sourceMessageId)
		{
		}

		/// <summary>
		/// Initializes a new instance with separate readable and writable block lists,
		/// used to build the manual task toolset with per-block access enforcement.
		/// </summary>
		public AgentMemoryTools(IMemoryFactStore factStore, IMemoryLogStore logStore, int sourceChatId,
			IReadOnlyList<MemoryBlock> readableFactBlocks, IReadOnlyList<MemoryBlock> writableFactBlocks,
			IReadOnlyList<MemoryBlock> readableLogBlocks, IReadOnlyList<MemoryBlock> writableLogBlocks,
			int sourceMessageId)
		{
			_factStore = factStore;
			_logStore = logStore;
			_sourceChatId = sourceChatId;
			_sourceMessageId = sourceMessageId;
			_readableFactBlocks = readableFactBlocks;
			_writableFactBlocks = writableFactBlocks;
			_readableLogBlocks = readableLogBlocks;
			_writableLogBlocks = writableLogBlocks;
		}
		/// <summary>
		/// Creates the tools available to the automatic memory recorder:
		/// searching, storing, superseding, forgetting facts and appending episodic logs.
		/// </summary>
		/// <returns>The list of agent tools.</returns>
		public ImmutableList<AgentTool> CreateRecorderTools()
		{
			var tools = ImmutableList.CreateBuilder<AgentTool>();
			if (_readableFactBlocks.Count > 0)
			{
				AddTool(tools, "search_facts", """
					Searches the memory blocks for facts relevant to the given queries and returns them with their IDs and relevance scores.
					Use multiple hypothetical queries to greatly improve semantic matching.
					""", SearchFactsAsync);
				AddTool(tools, "record_fact", """
					Stores a new fact in the specified memory block with the given importance.
					If a very similar fact already exists (cosine score >= 0.9), it is automatically superseded.
					""", RecordFactAsync);
				AddTool(tools, "supersede_fact", """
					Replaces an existing fact (by its ID) with a new fact in the specified memory block.
					Use it when the new information contradicts or updates an existing fact.
					""", SupersedeFactAsync);
				AddTool(tools, "forget_fact", """
					Forgets (soft-deletes) a fact from the specified memory block by its ID.
					Use it when an existing fact is no longer true or relevant.
					""", ForgetFactAsync);
			}
			if (_readableLogBlocks.Count > 0)
			{
				AddTool(tools, "search_logs", """
					Searches the episodic logs of the memory blocks using BM25 keyword search.
					Transient, consolidated and ignored logs are excluded from search results.
					""", SearchLogsAsync);
				AddTool(tools, "view_logs", """
					Views active and transient episodic logs of the memory blocks within the given time window (real time and/or alternative timeline).
					When no window is specified, the most recent logs are returned. Logs are ordered by their begin time, newest first.
					""", ViewLogsAsync);
				AddTool(tools, "append_log", """
					Appends a new episodic log entry to the specified memory block.
					Logs are immutable: they cannot be edited, only deleted. The log text is added to the keyword search index.
					""", AppendLogAsync);
			}
			return tools.ToImmutable();
		}

		/// <summary>
		/// Creates the tools available to the automatic memory retriever:
		/// searching facts and episodic logs in the readable memory blocks.
		/// </summary>
		/// <returns>The list of agent tools.</returns>
		public ImmutableList<AgentTool> CreateReaderTools()
		{
			var tools = ImmutableList.CreateBuilder<AgentTool>();
			if (_readableFactBlocks.Count > 0)
			{
				AddTool(tools, "search_facts", """
					Searches the memory blocks for facts relevant to the given queries and returns them with their IDs and relevance scores.
					Use multiple hypothetical queries to greatly improve semantic matching.
					""", SearchFactsAsync);
			}
			if (_readableLogBlocks.Count > 0)
			{
				AddTool(tools, "search_logs", """
					Searches the episodic logs of the memory blocks using BM25 keyword search.
					Transient, consolidated and ignored logs are excluded from search results.
					""", SearchLogsAsync);
				AddTool(tools, "view_logs", """
					Views active and transient episodic logs of the memory blocks within the given time window (real time and/or alternative timeline).
					When no window is specified, the most recent logs are returned. Logs are ordered by their begin time, newest first.
					""", ViewLogsAsync);
			}

			return tools.ToImmutable();
		}

		/// <summary>
		/// Creates the manual memory toolset for agent tasks: search and view tools over readable
		/// blocks, and write tools over writable blocks, with per-block access enforcement.
		/// </summary>
		/// <returns>The list of agent tools.</returns>
		public ImmutableList<AgentTool> CreateManualTools()
		{
			var tools = ImmutableList.CreateBuilder<AgentTool>();
			if (_readableFactBlocks.Count > 0)
			{
				AddTool(tools, "memory-search_facts", """
					Searches the memory blocks for facts relevant to the given queries and returns them with their IDs and relevance scores.
					Use multiple hypothetical queries to greatly improve semantic matching.
					""", SearchFactsAsync);
			}
			if (_writableFactBlocks.Count > 0)
			{
				AddTool(tools, "memory-record_fact", """
					Stores a new fact in the specified memory block with the given importance.
					If a very similar fact already exists (cosine score >= 0.9), it is automatically superseded.
					""", RecordFactAsync);
				AddTool(tools, "memory-supersede_fact", """
					Replaces an existing fact (by its ID) with a new fact in the specified memory block.
					Use it when the new information contradicts or updates an existing fact.
					""", SupersedeFactAsync);
				AddTool(tools, "memory-forget_fact", """
					Forgets (soft-deletes) a fact from the specified memory block by its ID.
					Use it when an existing fact is no longer true or relevant.
					""", ForgetFactAsync);
			}
			if (_readableLogBlocks.Count > 0)
			{
				AddTool(tools, "memory-search_logs", """
					Searches the episodic logs of the memory blocks using BM25 keyword search.
					Transient, consolidated and ignored logs are excluded from search results.
					""", SearchLogsAsync);
				AddTool(tools, "memory-view_logs", """
					Views active and transient episodic logs of the memory blocks within the given time window (real time and/or alternative timeline).
					When no window is specified, the most recent logs are returned. Logs are ordered by their begin time, newest first.
					""", ViewLogsAsync);
			}
			if (_writableLogBlocks.Count > 0)
			{
				AddTool(tools, "memory-append_log", """
					Appends a new episodic log entry to the specified memory block.
					Logs are immutable: they cannot be edited, only deleted. The log text is added to the keyword search index.
					""", AppendLogAsync);
			}
			return tools.ToImmutable();
		}

		private static void AddTool(ImmutableList<AgentTool>.Builder tools, string name, string description, Delegate executor)
		{
			tools.Add(new DelegateAgentTool(name, null, description, Tools.ToolSource.Native, executor));
		}

		private async Task<AgentToolCallResult> SearchFactsAsync(
			[Description("The memory blocks where the facts should be searched. Use null to search in all available memory blocks")] string[]? blocks,
			[Description("""
				The queries to search by in user's language.
				Use multiple hypothetical (HyDE) queries to greatly improve semantic matching.
				Avoid direct questions, use hypothetical answers instead (e.g. "The user's food preferences is vegetarian or meat eater.")
				""")] string[] queries,
			[Description("The minimum importance score of facts to return, from 0.0 (any importance) to 1.0 (only the most important)")] double minImportance = 0.0,
			CancellationToken cancellationToken = default)
		{
			var blocksToSearch = ResolveBlocks(_readableFactBlocks, blocks);
			if (blocksToSearch.Count == 0)
				return Error("No memory blocks to search in.");

			try
			{
				var perBlockResults = await Task.WhenAll(blocksToSearch.Select(block => Task.WhenAll(
					queries.Select(query => _factStore.SearchAsync(block, query, minImportance: minImportance, maxCount: 10, cancellationToken)))));
				var sb = new StringBuilder();
				int totalFound = 0;

				for (int i = 0; i < blocksToSearch.Count; i++)
				{
					var merged = MergeFactResults(perBlockResults[i]);
					if (merged.Count == 0)
						continue;

					totalFound += merged.Count;
					sb.AppendLine($"### {blocksToSearch[i].Name}");
					foreach (var fact in merged)
					{
						var cosineScore = fact.CosineScore?.ToString("0.00") ?? "unknown";
						var bm25Score = fact.Bm25Score?.ToString("0.00") ?? "unknown";
						sb.AppendLine($"- **{fact.Text}** [id: {fact.Id}] (cosine score: {cosineScore}, bm25 score: {bm25Score})");
					}
					sb.AppendLine();
				}

				if (totalFound == 0)
					return Success("No matching facts found.");

				return Success(sb.ToString());
			}
			catch (Exception ex)
			{
				return Error($"Failed to search facts. Error: {ex.Message}");
			}
		}

		private async Task<AgentToolCallResult> RecordFactAsync(
			[Description("The memory block where the fact should be stored")] string block,
			[Description("The fact to store in the memory block in user's language. Examples: 'User is vegetarian' or 'User likes pizza'")] string fact,
			[Description("The importance of the fact, from 0.0 (least important) to 1.0 (most important)")] double importance,
			CancellationToken cancellationToken = default)
		{
			var targetBlock = _writableFactBlocks.FirstOrDefault(b => b.Name == block);
			if (targetBlock is null)
				return Error($"Memory block '{block}' is not found or does not allow writing.");

			try
			{
				var similarFacts = await _factStore.SearchAsync(targetBlock, fact, maxCount: 10, cancellationToken: cancellationToken);
				var highestScore = similarFacts.MaxBy(f => f.CosineScore ?? 0);

				if (highestScore is not null && highestScore.CosineScore >= 0.9)
				{
					var storedFact = await _factStore.SupersedeAsync(targetBlock, highestScore.Id, fact,
						_sourceChatId, _sourceMessageId, importance, cancellationToken);
					return Success($"Fact stored with supersede successfully. ID: {storedFact.Id}, superseded fact [id: {highestScore.Id}]: {highestScore.Text}");
				}

				var stored = await _factStore.StoreAsync(targetBlock, fact,
					_sourceChatId, _sourceMessageId, importance, cancellationToken);
				return Success($"Fact stored successfully. ID: {stored.Id}");
			}
			catch (Exception ex)
			{
				return Error($"Failed to store fact in memory block '{block}'. Error: {ex.Message}");
			}
		}

		private async Task<AgentToolCallResult> SupersedeFactAsync(
			[Description("The memory block that contains the fact to supersede")] string block,
			[Description("The ID of the fact to supersede")] int supersededId,
			[Description("The text of the replacement fact")] string fact,
			[Description("The importance of the replacement fact, from 0.0 (least important) to 1.0 (most important)")] double importance,
			CancellationToken cancellationToken = default)
		{
			var targetBlock = _writableFactBlocks.FirstOrDefault(b => b.Name == block);
			if (targetBlock is null)
				return Error($"Memory block '{block}' is not found or does not allow writing.");

			try
			{
				var storedFact = await _factStore.SupersedeAsync(targetBlock, supersededId, fact,
					_sourceChatId, _sourceMessageId, importance, cancellationToken);
				return Success($"Fact stored with supersede successfully. ID: {storedFact.Id}");
			}
			catch (Exception ex)
			{
				return Error($"Failed to supersede fact [id: {supersededId}] in memory block '{block}'. Error: {ex.Message}");
			}
		}

		private async Task<AgentToolCallResult> ForgetFactAsync(
			[Description("The memory block that contains the fact")] string block,
			[Description("The ID of the fact to forget")] int factId,
			CancellationToken cancellationToken = default)
		{
			var targetBlock = _writableFactBlocks.FirstOrDefault(b => b.Name == block);
			if (targetBlock is null)
				return Error($"Memory block '{block}' is not found or does not allow writing.");

			try
			{
				await _factStore.SoftDeleteAsync(targetBlock, factId, cancellationToken);
				return Success($"Fact [id: {factId}] forgotten in memory block '{block}'. It can be restored later.");
			}
			catch (Exception ex)
			{
				return Error($"Failed to forget fact [id: {factId}] in memory block '{block}'. Error: {ex.Message}");
			}
		}

		private async Task<AgentToolCallResult> AppendLogAsync(
			[Description("The memory block where the log should be appended")] string block,
			[Description("The text of the log describing what happened. Must not be empty.")] string text,
			[Description("The importance of the log, from 0.0 (least important) to 1.0 (most important)")] double importance,
			[Description("The real-time timestamp when the log began in UTC ISO 8601 format (e.g. 2026-08-10T15:00:00Z). Defaults to now.")] DateTime? timeStampBegin = null,
			[Description("The real-time timestamp when the log ended in UTC ISO 8601 format (e.g. 2026-08-10T15:00:00Z). Defaults to timeStampBegin.")] DateTime? timeStampEnd = null,
			[Description("The alternative timeline ordinal when the log began (for example, the day number)")] double timeLineOrdinalBegin = 0,
			[Description("The alternative timeline details when the log began (for example, \"Day 3, 14:00\")")] string timeLineDetailsBegin = "",
			[Description("The alternative timeline ordinal when the log ended. Defaults to the begin ordinal.")] double? timeLineOrdinalEnd = null,
			[Description("The alternative timeline details when the log ended. Defaults to the begin details.")] string? timeLineDetailsEnd = null,
			CancellationToken cancellationToken = default)
		{
			var targetBlock = _writableLogBlocks.FirstOrDefault(b => b.Name == block);
			if (targetBlock is null)
				return Error($"Memory block '{block}' is not found or does not allow writing.");

			try
			{
				var log = await _logStore.AppendAsync(
					targetBlock,
					text,
					timeStampBegin: timeStampBegin,
					timeStampEnd: timeStampEnd,
					timeLineOrdinalBegin: timeLineOrdinalBegin,
					timeLineDetailsBegin: timeLineDetailsBegin,
					timeLineOrdinalEnd: timeLineOrdinalEnd ?? timeLineOrdinalBegin,
					timeLineDetailsEnd: string.IsNullOrEmpty(timeLineDetailsEnd) ? timeLineDetailsBegin : timeLineDetailsEnd,
					sourceChatId: _sourceChatId,
					sourceMessageId: _sourceMessageId,
					importance: importance,
					cancellationToken: cancellationToken);
				return Success($"Log appended successfully. ID: {log.Id}");
			}
			catch (Exception ex)
			{
				return Error($"Failed to append log to memory block '{block}'. Error: {ex.Message}");
			}
		}

		private async Task<AgentToolCallResult> SearchLogsAsync(
			[Description("The memory blocks where the logs should be searched. Use null to search in all available memory blocks")] string[]? blocks,
			[Description("The search query text")] string query,
			[Description("The maximum number of logs to return")] int maxCount = 5,
			[Description("The minimum importance score of the logs. Logs with lower scores are excluded.")] double minImportance = 0.0,
			CancellationToken cancellationToken = default)
		{
			var blocksToSearch = ResolveBlocks(_readableLogBlocks, blocks);
			if (blocksToSearch.Count == 0)
				return Error("No memory blocks to search in.");

			try
			{
				var perBlockResults = await Task.WhenAll(blocksToSearch.Select(block =>
					_logStore.SearchAsync(block, query, minImportance: minImportance, maxCount, cancellationToken)));
				var sb = new StringBuilder();
				int totalFound = 0;

				for (int i = 0; i < blocksToSearch.Count; i++)
				{
					var logs = perBlockResults[i];
					if (logs.Length == 0)
						continue;

					totalFound += logs.Length;
					sb.AppendLine($"### {blocksToSearch[i].Name}");
					foreach (var log in logs)
					{
						var bm25Score = log.Bm25Score?.ToString("0.00") ?? "unknown";
						sb.AppendLine($"- **{log.Text}** [id: {log.Id}] (bm25 score: {bm25Score}, importance: {log.Importance:0.00}, {log.TimeStampBegin:yyyy-MM-dd HH:mm} UTC)");
					}
					sb.AppendLine();
				}

				if (totalFound == 0)
					return Success("No matching logs found.");

				return Success(sb.ToString());
			}
			catch (Exception ex)
			{
				return Error($"Failed to search logs. Error: {ex.Message}");
			}
		}

		private async Task<AgentToolCallResult> ViewLogsAsync(
			[Description("The memory blocks where the logs should be viewed. Use null to view logs in all available memory blocks")] string[]? blocks,
			[Description("The inclusive lower bound of the real-time window in UTC ISO 8601 format (e.g. 2026-08-10T15:00:00Z)")] DateTime? from = null,
			[Description("The inclusive upper bound of the real-time window in UTC ISO 8601 format (e.g. 2026-08-10T15:00:00Z)")] DateTime? to = null,
			[Description("The inclusive lower bound of the alternative timeline ordinal window (for example, the day number)")] double? timeLineFrom = null,
			[Description("The inclusive upper bound of the alternative timeline ordinal window")] double? timeLineTo = null,
			[Description("The maximum number of logs to return. When no time window is specified, the most recent logs are returned.")] int maxCount = 20,
			[Description("The minimum importance score of the logs. Logs with lower scores are excluded.")] double minImportance = 0.0,
			CancellationToken cancellationToken = default)
		{
			var blocksToView = ResolveBlocks(_readableLogBlocks, blocks);
			if (blocksToView.Count == 0)
				return Error("No memory blocks to view.");

			try
			{
				var perBlockResults = await Task.WhenAll(blocksToView.Select(block =>
					_logStore.GetByTimeAsync(block, from, to, timeLineFrom, timeLineTo, minImportance, maxCount, cancellationToken)));
				var sb = new StringBuilder();
				int totalFound = 0;

				for (int i = 0; i < blocksToView.Count; i++)
				{
					var logs = perBlockResults[i];
					if (logs.Length == 0)
						continue;

					totalFound += logs.Length;
					sb.AppendLine($"### {blocksToView[i].Name}");
					foreach (var log in logs)
					{
						var timeline = string.IsNullOrEmpty(log.TimeLineDetailsBegin) ? "" : $", timeline: {log.TimeLineDetailsBegin}";
						sb.AppendLine($"- **{log.Text}** [id: {log.Id}] ({log.TimeStampBegin:yyyy-MM-dd HH:mm} UTC, status: {log.Status}, importance: {log.Importance:0.00}{timeline})");
					}
					sb.AppendLine();
				}

				if (totalFound == 0)
					return Success("No logs found in the specified time window.");

				return Success(sb.ToString());
			}
			catch (Exception ex)
			{
				return Error($"Failed to view logs. Error: {ex.Message}");
			}
		}

		private static List<MemoryBlock> ResolveBlocks(IReadOnlyList<MemoryBlock> available, string[]? names)
		{
			if (names is null || names.Length == 0)
				return available.ToList();
			var namesSet = names.ToHashSet();
			return available.Where(b => namesSet.Contains(b.Name)).ToList();
		}

		private static List<MemoryFactResult> MergeFactResults(MemoryFactResult[][] queryBatches)
		{
			var merged = new Dictionary<int, MemoryFactResult>();
			foreach (var fact in queryBatches.SelectMany(b => b))
			{
				if (!merged.TryGetValue(fact.Id, out var existing) || (fact.RrfScore ?? 0) > (existing.RrfScore ?? 0))
					merged[fact.Id] = fact;
			}

			return merged.Values
				.OrderByDescending(f => f.RrfScore ?? f.CosineScore ?? f.Bm25Score ?? 0)
				.ToList();
		}

		private static AgentToolCallResult Success(string content) => new() { Success = true, Content = content };

		private static AgentToolCallResult Error(string content) => new() { Success = false, Content = content };
	}
}
