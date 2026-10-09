using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.LLM.Services.Tools
{
	/// <summary>
	/// The default implementation of the <see cref="IToolsetCacheService"/> interface.
	/// </summary>
	[ChatService(typeof(IToolsetCacheService))]
	public class ToolsetCacheService : IToolsetCacheService
	{
		private readonly IAddonSetCollector<ToolInfo> _builder;
		private ImmutableDictionary<string, ToolInfo> _availableTools = [], _aliasedTools = [], _validTools = [], _validAliasedTools = [];

		public ImmutableDictionary<string, ToolInfo> AvailableTools => _availableTools;

		public ImmutableDictionary<string, ToolInfo> AliasedTools => _aliasedTools;

		public ImmutableDictionary<string, ToolInfo> ValidTools => _validTools;

		public ImmutableDictionary<string, ToolInfo> ValidAliasedTools => _validAliasedTools;

		public ToolsetCacheService(IAddonSetCollector<ToolInfo> builder)
		{
			_builder = builder;
		}

		public void Invalidate(ChatAgentDescriptor? agent)
		{
			_availableTools = _builder.GetAvailableAddons().ToImmutableDictionary(t => t.Name);
			_validTools = agent is not null ? _builder.GetAddonsForAgent(agent).ToImmutableDictionary(t => t.Name) : _availableTools;
			_aliasedTools = BuildDictionaryWithAliases(_availableTools.Values);
			_validAliasedTools = BuildDictionaryWithAliases(_validTools.Values);
		}

		/// <summary>
		/// Builds an immutable dictionary from a collection of tools, including both the primary name and all aliases as keys.
		/// If multiple tools share the same key (name or alias), the last one wins.
		/// </summary>
		private static ImmutableDictionary<string, ToolInfo> BuildDictionaryWithAliases(IEnumerable<ToolInfo> tools)
		{
			return tools
				.SelectMany(t => new[] { (Key: t.Name, Tool: t) }
					.Concat(t.Aliases.Select(a => (Key: a, Tool: t))))
				.GroupBy(x => x.Key)
				.Select(g => g.Last())
				.ToImmutableDictionary(x => x.Key, x => x.Tool);
		}
	}
}
