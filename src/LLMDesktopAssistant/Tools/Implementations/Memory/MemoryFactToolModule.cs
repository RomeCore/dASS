using System.ComponentModel;
using System.Text;
using LLMDesktopAssistant.Agents.Memory;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using Material.Icons;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Tools.Implementations.Memory
{
	[ToolModule]
	public class MemoryFactToolModule : MemoryToolModuleBase
	{
		private readonly IMemoryFactStore _memoryFactStore;

		/// <summary>
		/// Initializes a new instance of the <see cref="MemoryFactToolModule"/> class.
		/// </summary>
		/// <param name="chat">The chat instance where the tools are executed.</param>
		/// <param name="chatSettings">The settings service of the chat.</param>
		/// <param name="agentManager">The agent management service used to resolve agent memory attachments.</param>
		/// <param name="memoryFactStore">The store used to store and manage semantic memory facts.</param>
		public MemoryFactToolModule(Chat chat, IChatSettingsService chatSettings, IAgentManagementService agentManager,
			IMemoryFactStore memoryFactStore)
			: base(chat, chatSettings, agentManager)
		{
			_memoryFactStore = memoryFactStore;

			AddTool(new ToolInitializationInfo
			{
				Executor = StoreAsync,
				Name = "memory-store_fact",
				Description = """
					Stores a fact in the specified memory block with the given importance.
					""",
				DefaultExpectedBehaviour = ToolBehaviour.SemanticMemoryWrite,
				NameKey = Locale.GetKey("tool.name.memory-store_fact"),
				DescriptionKey = Locale.GetKey("tool.description.memory-store_fact"),
				CategoryKey = Locale.GetKey("tool.category.memory")
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = RetrieveAsync,
				Name = "memory-retrieve_fact",
				Description = """
					Retrieves facts with their IDs from the specified memory blocks (or all enabled blocks) by the given query.
					HyDE query is used to improve semantic matching and may be provided additionally.
					""",
				DefaultExpectedBehaviour = ToolBehaviour.SemanticMemoryRead,
				NameKey = Locale.GetKey("tool.name.memory-retrieve_fact"),
				DescriptionKey = Locale.GetKey("tool.description.memory-retrieve_fact"),
				CategoryKey = Locale.GetKey("tool.category.memory")
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = ForgetAsync,
				Name = "memory-forget_fact",
				Description = """
					Forgets (deletes) a fact from the specified memory block by its ID.
					By default performs a soft delete: the fact is marked as Deleted and can be restored later.
					Use mode="hard" to remove the fact permanently.
					""",
				DefaultExpectedBehaviour = ToolBehaviour.SemanticMemoryDelete,
				NameKey = Locale.GetKey("tool.name.memory-forget_fact"),
				DescriptionKey = Locale.GetKey("tool.description.memory-forget_fact"),
				CategoryKey = Locale.GetKey("tool.category.memory")
			});
		}

		private async Task<ToolExecutionResult> StoreAsync(
			[Description("The memory block where the fact should be stored")] string block,
			[Description("The fact to store in the memory block in user's language. Examples: 'User is vegetarian' or 'User likes pizza'")] string fact,
			[Description("The importance of the fact, from 0.0 (least important) to 1.0 (most important)")] double importance,
			IToolExecutionContext ctx,
			[Description("The ID of the fact to supersede. If not provided, conflicts will be reported. If 0, the fact will be stored without superseding any existing facts.")] int? supersedeId = null,
			CancellationToken cancellationToken = default)
		{
			ctx.Call.SetStatus(MaterialIconKind.DatabaseAdd, $"**{fact}** → *{block}*");

			var (targetBlock, error) = GetBlock(ctx, block, requireReading: false, requireWriting: true, requireFacts: true);
			if (targetBlock is null)
			{
				ctx.Call.SetResult(error ?? $"Memory block '{block}' is not available.");
				return ToolExecutionResult.Failed;
			}

			try
			{
				int messageId = ((ChatToolExecutionContext)ctx).FindMessageId();

				var similarFacts = await _memoryFactStore.SearchAsync(targetBlock, fact, maxCount: 10, cancellationToken: cancellationToken);
				var highestScore = similarFacts.MaxBy(f => f.CosineScore ?? 0);

				if (highestScore is not null)
				{
					if (highestScore.CosineScore >= 0.9)
					{
						// Automatically supersede if the similarity is very high
						supersedeId ??= highestScore.Id;
					}
					else if (highestScore.CosineScore < 0.6)
					{
						supersedeId ??= 0;
					}

					if (supersedeId is null)
					{
						var conflicts = string.Join(Environment.NewLine, similarFacts
							.Where(f => (f.CosineScore ?? 0) >= 0.6)
							.Select(f => $"- **{f.CosineScore:0.00}** [id: {f.Id}] {f.Text}"));

						ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseRefresh);
						ctx.Call.SetResult($"""
							Conflict detected with similar facts (cosine score >= 0.6):
							{conflicts}

							Call tool again with supersedeId=<fact id> to supersede the desired fact or supersedeId=0 to append a new fact.
							""");
						return ToolExecutionResult.Succeeded;
					}
					else
					{
						if (supersedeId == 0)
						{
							var storedFact = await _memoryFactStore.StoreAsync(targetBlock, fact, Chat.Id, messageId, importance, cancellationToken);
							ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseCheck);
							ctx.Call.SetResult($"Fact stored successfully. ID: {storedFact.Id}");
							return ToolExecutionResult.Succeeded;
						}
						else
						{
							var storedFact = await _memoryFactStore.SupersedeAsync(targetBlock, supersedeId.Value, fact, Chat.Id, messageId, importance, cancellationToken);
							ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseEdit);
							if (supersedeId.Value == highestScore.Id)
								ctx.Call.SetResult($"Fact stored with supersede successfully. ID: {storedFact.Id}, supersed fact [id: {highestScore.Id}]: {highestScore.Text}");
							else
								ctx.Call.SetResult($"Fact stored with supersede successfully. ID: {storedFact.Id}");
							return ToolExecutionResult.Succeeded;
						}
					}
				}
				else
				{
					var storedFact = await _memoryFactStore.StoreAsync(targetBlock, fact, Chat.Id, messageId, importance, cancellationToken);
					ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseCheck);
					ctx.Call.SetResult($"Fact stored successfully. ID: {storedFact.Id}");
					return ToolExecutionResult.Succeeded;
				}
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseOff);
				ctx.Call.SetResult($"Failed to store fact in memory block '{block}'. Error: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private async Task<ToolExecutionResult> RetrieveAsync(
			[Description("The memory blocks where the fact should be retrieved. Use null to retrieve from all enabled memory blocks")] string[]? blocks,
			[Description("""
				The queries to retrieve by in user's language.
				Use multiple HyDE (hypotetical) queries to greatly improve semantic matching.
				Avoid direct questions (such as "What user's preferences about food?"),
				use hypothetical answers instead (e.g. "The user's food preferences is vegetarian or meat eater.")
				or keywords (e.g. "food preferences" or "dietary restrictions")
				""")] string[] queries,
			IToolExecutionContext ctx,
			[Description("The minimum importance score of facts to return, from 0.0 (any importance) to 1.0 (only the most important)")] double minImportance = 0.0,
			CancellationToken cancellationToken = default)
		{
			ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseSearch);

			if (queries.Length == 0)
			{
				ctx.Call.SetResult("No queries blocks to search by.");
				return ToolExecutionResult.Failed;
			}

			ctx.Call.SetStatusTitle($"**{queries[0]}**");

			var resolution = GetBlocks(ctx, blocks, requireReading: true, requireWriting: false, requireFacts: true);

			if (!resolution.HasBlocks)
			{
				ctx.Call.SetResult(resolution.BuildError("No memory blocks with facts enabled are available to search in."));
				return ToolExecutionResult.Failed;
			}

			try
			{
				var perBlockResults = await Task.WhenAll(resolution.Blocks.Select(async block =>
				{
					var queryBatches = await Task.WhenAll(queries.Select(query =>
					{
						return _memoryFactStore.SearchAsync(block, query, minImportance: minImportance, maxCount: 10, cancellationToken: cancellationToken);
					}));
					return (block, MergeResults(queryBatches));
				}));

				var sb = new StringBuilder();
				int totalFound = 0;

				foreach (var (block, facts) in perBlockResults)
				{
					if (facts.Count == 0)
						continue;

					totalFound += facts.Count;
					sb.AppendLine($"### {block.Name}");
					foreach (var fact in facts)
					{
						var cosineScore = fact.CosineScore?.ToString("0.00") ?? "unknown";
						var bm25Score = fact.Bm25Score?.ToString("0.00") ?? "unknown";
						sb.AppendLine($"- **{fact.Text}** [id: {fact.Id}] (cosine score: {cosineScore}, bm25 score: {bm25Score})");
					}
					sb.AppendLine();
				}

				if (sb.Length == 0)
					sb.AppendLine("No matching facts found.");

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
				ctx.Call.SetResult($"Failed to retrieve facts. Error: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private async Task<ToolExecutionResult> ForgetAsync(
			[Description("The memory block that contains the fact")] string block,
			[Description("The ID of the fact to forget")] int factId,
			IToolExecutionContext ctx,
			[Description("The deletion mode: 'soft' (default) marks the fact as Deleted and keeps it restorable; 'hard' removes the fact permanently.")] string mode = "soft",
			CancellationToken cancellationToken = default)
		{
			ctx.Call.SetStatus(MaterialIconKind.DatabaseRemove, $"**{block}** [id: {factId}]");

			var (targetBlock, error) = GetBlock(ctx, block, requireReading: false, requireWriting: true, requireFacts: true);
			if (targetBlock is null)
			{
				ctx.Call.SetResult(error ?? $"Memory block '{block}' is not available.");
				return ToolExecutionResult.Failed;
			}

			try
			{
				bool hardDelete = string.Equals(mode, "hard", StringComparison.OrdinalIgnoreCase);

				if (hardDelete)
					await _memoryFactStore.HardDeleteAsync(targetBlock, factId, cancellationToken);
				else
					await _memoryFactStore.SoftDeleteAsync(targetBlock, factId, cancellationToken);

				ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseCheck);
				ctx.Call.SetResult(hardDelete
					? $"Fact [id: {factId}] permanently deleted from memory block '{block}'."
					: $"Fact [id: {factId}] forgotten in memory block '{block}'. It can be restored later.");
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseOff);
				ctx.Call.SetResult($"Failed to forget fact [id: {factId}] in memory block '{block}'. Error: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private static List<MemoryFactResult> MergeResults(MemoryFactResult[][] queryBatches)
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
	}
}
