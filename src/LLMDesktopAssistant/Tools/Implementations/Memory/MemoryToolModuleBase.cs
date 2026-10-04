using LLMDesktopAssistant.Agents.Memory;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;

namespace LLMDesktopAssistant.Tools.Implementations.Memory
{
	/// <summary>
	/// The result of memory block resolution. Contains the resolved blocks and the reasons
	/// why the requested blocks were excluded, so tools can report meaningful errors.
	/// </summary>
	public sealed class MemoryBlockResolution
	{
		private readonly IReadOnlyList<MemoryBlock> _blocks;
		private readonly IReadOnlyDictionary<string, string> _excluded;

		/// <summary>
		/// Initializes a new instance of the <see cref="MemoryBlockResolution"/> class.
		/// </summary>
		/// <param name="blocks">The resolved memory blocks.</param>
		/// <param name="excluded">The requested blocks that were excluded, mapped to the exclusion reason.</param>
		public MemoryBlockResolution(IReadOnlyList<MemoryBlock> blocks, IReadOnlyDictionary<string, string> excluded)
		{
			_blocks = blocks;
			_excluded = excluded;
		}

		/// <summary>
		/// Gets the resolved memory blocks.
		/// </summary>
		public IReadOnlyList<MemoryBlock> Blocks => _blocks;

		/// <summary>
		/// Gets the requested blocks that were excluded, mapped to the exclusion reason.
		/// </summary>
		public IReadOnlyDictionary<string, string> Excluded => _excluded;

		/// <summary>
		/// Gets a value indicating whether at least one memory block was resolved.
		/// </summary>
		public bool HasBlocks => _blocks.Count > 0;

		/// <summary>
		/// Builds an error message from the given generic message and the exclusion reasons.
		/// </summary>
		/// <param name="noBlocksMessage">The generic message used when no blocks were resolved.</param>
		/// <returns>The combined error message.</returns>
		public string BuildError(string noBlocksMessage)
		{
			if (_excluded.Count == 0)
				return noBlocksMessage;

			return string.Join(" ", _excluded.Values.Prepend(noBlocksMessage));
		}
	}

	/// <summary>
	/// Base class for memory-related tool modules. Provides access to the memory blocks of the
	/// sender agent and hides all memory tools when memory is disabled in the chat settings.
	/// </summary>
	public abstract class MemoryToolModuleBase : ToolModule
	{
		private readonly Chat _chat;
		private readonly IChatSettingsService _chatSettings;
		private readonly IAgentManagementService _agentManager;

		/// <summary>
		/// Gets the chat instance where the tools are executed.
		/// </summary>
		protected Chat Chat => _chat;

		/// <summary>
		/// Gets the agent management service used to resolve agent memory attachments.
		/// </summary>
		protected IAgentManagementService AgentManager => _agentManager;

		/// <summary>
		/// Initializes a new instance of the <see cref="MemoryToolModuleBase"/> class.
		/// </summary>
		/// <param name="chat">The chat instance where the tools are executed.</param>
		/// <param name="chatSettings">The settings service of the chat.</param>
		/// <param name="agentManager">The agent management service used to resolve agent memory attachments.</param>
		protected MemoryToolModuleBase(Chat chat, IChatSettingsService chatSettings, IAgentManagementService agentManager)
		{
			_chat = chat;
			_chatSettings = chatSettings;
			_agentManager = agentManager;
		}

		/// <inheritdoc/>
		public override IEnumerable<ToolInfo> GetTools()
		{
			var memoryOpts = _chatSettings.Settings.Memory.GetEffectiveMemoryOptions();
			if (memoryOpts.EnableMemory && memoryOpts.ManualControlEnabled)
				return base.GetTools();
			return [];
		}

		/// <summary>
		/// Resolves a single memory block attached to the sender agent by its name,
		/// checking the specified access and content requirements.
		/// </summary>
		/// <param name="ctx">The tool execution context.</param>
		/// <param name="name">The name of the block to resolve.</param>
		/// <param name="requireReading">Whether the block must allow reading.</param>
		/// <param name="requireWriting">Whether the block must allow writing.</param>
		/// <param name="requireFacts">Whether the block must have facts enabled.</param>
		/// <param name="requireLogs">Whether the block must have logs enabled.</param>
		/// <returns>The resolved block and the exclusion reason, or <see langword="null"/> error when resolved.</returns>
		protected (MemoryBlock? Block, string? Error) GetBlock(ToolExecutionContext ctx, string name,
			bool requireReading, bool requireWriting, bool requireFacts = false, bool requireLogs = false)
		{
			var resolution = GetBlocks(ctx, [name], requireReading, requireWriting, requireFacts, requireLogs);
			if (resolution.HasBlocks)
				return (resolution.Blocks[0], null);

			return (null, resolution.BuildError($"Memory block '{name}' is not available."));
		}

		/// <summary>
		/// Resolves the memory blocks attached to the sender agent of the specified context,
		/// optionally filtered by names and access requirements. Blocks that are excluded
		/// are reported together with the exclusion reason.
		/// </summary>
		/// <param name="ctx">The tool execution context.</param>
		/// <param name="names">The block names to filter by, or <see langword="null"/> to include all attached blocks.</param>
		/// <param name="requireReading">Whether to include only blocks that allow reading.</param>
		/// <param name="requireWriting">Whether to include only blocks that allow writing.</param>
		/// <param name="requireFacts">Whether to include only blocks with facts enabled.</param>
		/// <param name="requireLogs">Whether to include only blocks with logs enabled.</param>
		/// <returns>The resolution result with the resolved blocks and the exclusion reasons.</returns>
		protected MemoryBlockResolution GetBlocks(ToolExecutionContext ctx, string[]? names,
			bool requireReading, bool requireWriting, bool requireFacts = false, bool requireLogs = false)
		{
			var agent = _agentManager.TryGetSenderAgentDescriptor(ctx.Message);
			if (agent is null)
				return new MemoryBlockResolution([], new Dictionary<string, string>());

			var attachments = agent.Memory.GetEffectiveBlocks(_chatSettings.Settings);

			var blocks = new List<MemoryBlock>();
			var excluded = new Dictionary<string, string>();
			var requested = names?.ToHashSet();

			foreach (var attachment in attachments)
			{
				if (attachment.Reference.Object is not { } block)
					continue;
				if (requested is not null && !requested.Contains(block.Name))
					continue;

				if (!attachment.Enabled)
				{
					excluded[block.Name] = $"Memory block '{block.Name}' is disabled in the agent settings.";
					continue;
				}
				if (!block.FactsEnabled && !block.LogsEnabled)
				{
					excluded[block.Name] = $"Memory block '{block.Name}' has both facts and logs disabled.";
					continue;
				}
				if (requireReading && !attachment.AllowsReading())
				{
					excluded[block.Name] = $"Memory block '{block.Name}' does not allow reading.";
					continue;
				}
				if (requireWriting && !attachment.AllowsWriting())
				{
					excluded[block.Name] = $"Memory block '{block.Name}' does not allow writing.";
					continue;
				}
				if (requireFacts && !block.FactsEnabled)
				{
					excluded[block.Name] = $"Memory block '{block.Name}' has facts disabled.";
					continue;
				}
				if (requireLogs && !block.LogsEnabled)
				{
					excluded[block.Name] = $"Memory block '{block.Name}' has logs disabled.";
					continue;
				}

				blocks.Add(block);
			}

			if (requested is not null)
			{
				var found = attachments
					.Where(a => a.Reference.Object is { } b && requested.Contains(b.Name))
					.Select(a => a.Reference.Object!.Name)
					.ToHashSet();
				foreach (var name in requested)
				{
					if (!found.Contains(name))
						excluded[name] = $"Memory block '{name}' is not attached to the agent.";
				}
			}

			return new MemoryBlockResolution(blocks, excluded);
		}
	}
}
