using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Settings;

namespace LLMDesktopAssistant.LLM.Settings
{
	/// <summary>
	/// Class representing the settings for a chat session.
	/// </summary>
	[SettingsObject("chat")]
	public class ChatSettings : SettingsObject
	{
		/// <summary>
		/// Gets or sets the agent settings that will be used for inherited agent settings.
		/// Example: agent inherits chat's tool settings.
		/// </summary>
		public ChatAgentDescriptor InheritedAgentSettings
		{
			get => field ??= new();
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Settings for the users interacting with the chat.
		/// </summary>
		public ChatUserSettings Users
		{
			get => field ??= new();
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Settings related to language models used in chat.
		/// </summary>
		public ChatModelSettings Models
		{
			get => field ??= new();
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Settings related to chat agents.
		/// </summary>
		public ChatAgentSettings Agents
		{
			get => field ??= new();
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Environment and working directory settings.
		/// </summary>
		public ChatEnvironmentSettings Environment
		{
			get => field ??= new();
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Settings related to database connections.
		/// </summary>
		public ChatDatabaseSettings Databases
		{
			get => field ??= new();
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Settings for MCP (Model Context Protocol) servers.
		/// </summary>
		public ChatMcpSettings Mcp
		{
			get => field ??= new();
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Addons and their packs loading settings.
		/// </summary>
		public ChatAddonSettings Addons
		{
			get => field ??= new();
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Settings for tools and plugins used in the chat.
		/// </summary>
		public ChatToolSettings Tools
		{
			get => field ??= new();
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Settings related to skills.
		/// </summary>
		public ChatSkillSettings Skills
		{
			get => field ??= new();
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Settings related to sub-agents.
		/// </summary>
		public ChatSubAgentSettings SubAgents
		{
			get => field ??= new();
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Settings related to slash commands.
		/// </summary>
		public ChatCommandSettings Commands
		{
			get => field ??= new();
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Settings related to scripts (Lua, for example).
		/// </summary>
		public ChatScriptSettings Scripts
		{
			get => field ??= new();
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Settings for memory management in chat sessions.
		/// </summary>
		public ChatMemorySettings Memory
		{
			get => field ??= new();
			set => SetProperty(ref field, value);
		}
	}
}