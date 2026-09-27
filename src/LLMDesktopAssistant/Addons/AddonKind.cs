namespace LLMDesktopAssistant.Addons
{
	[Flags]
	public enum AddonKind
	{
		/// <summary>
		/// No addons are selected.
		/// </summary>
		None = 0,

		/// <summary>
		/// All kinds of addons, including packs, skills, sub-agents, tools, memory blocks, prompt contexts, commands, templates, and Lua scripts.
		/// </summary>
		All = Pack | Skill | SubAgent | Tool | MemoryBlock | PromptContext | Command | Template | LuaScript,

		/// <summary>
		/// The addon pack itself, used in the invalidation methods to invalidate packs along with other addon types.
		/// </summary>
		Pack = 1 << 0,

		/// <summary>
		/// Progressive disclosure-based prompt document (usually is a SKILL.md file).
		/// Used by agents to follow provided instructions for doing specific tasks, and/or change the agent behaviour.
		/// </summary>
		Skill = 1 << 1,

		/// <summary>
		/// The document (usually MD with YAML frontmatter) that describes system prompt and configuration for a sub-agent.
		/// Can be run by agents (both chat and task agents) to delegate specific tasks to other agents without
		/// manual configuration on every call.
		/// </summary>
		SubAgent = 1 << 2,

		/// <summary>
		/// The very basic agentic contract that allows agents to interact with the environment and perform actions.
		/// Can be either native tools (defined in this project by tool modules), MCP tools (provided by MCP servers),
		/// Ad-Hoc tools (one-time tools defined in the scripts), and scriptable tools (formely known as meta-tools).
		/// </summary>
		Tool = 1 << 3,

		// TODO: Reserved for future use
		/// <summary>
		/// The document that defines the contracts how to use agentic memory block. Memory blocks can store and retrieve
		/// facts and episodic logs with alternative timeline and hybrid (embedding + bm25) search.
		/// </summary>
		MemoryBlock = 1 << 4,

		/// <summary>
		/// The script that provides context for every request. Injection mode can be either anchored sections (system
		/// prompt snapshot + subsequent deltas), supersedeable stamps (lives in the history, so agent can see the changes)
		/// or live-tail (just for the current request).
		/// </summary>
		PromptContext = 1 << 5,

		// TODO: Reserved for future use
		/// <summary>
		/// The slash command that can be used by users to trigger specific actions within the system, manually calling
		/// the skills, sub-agents, tools, or running scripts.
		/// </summary>
		Command = 1 << 6,

		/// <summary>
		/// The template (usually LLT) that defines other addons, like skills and sub-agents, also provides the way to
		/// define prompt parts, like system prompts, personas, and components.
		/// </summary>
		Template = 1 << 7,

		/// <summary>
		/// The Lua script that can be used to define own Lua namespaces and functions, and register hooks.
		/// </summary>
		LuaScript = 1 << 8
	}
}
