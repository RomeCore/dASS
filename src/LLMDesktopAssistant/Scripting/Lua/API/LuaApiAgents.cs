using System.Text.Json.Nodes;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.LLM.Services.Tools;
using LLMDesktopAssistant.Prompting.Skills;
using LLMDesktopAssistant.Providers;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Utils;
using RCLargeLanguageModels;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	[LuaApi(chatScoped: true)]
	public class LuaApiAgents : LuaApiBase
	{
		public override string? Namespace => "dass.agents";

		public override string? Manuals => """
			--- dass.agents — agentic LLM execution API

			Provides the ability to execute LLM agents with tools directly from Lua scripts.
			The agent uses the model configured as "AgenticToolsModel" in chat settings
			and has access to all tools registered in the current chat.

			FUNCTIONS:

			--- async dass.agents.execute(properties...)
			  Executes one or more LLM agents with the given conversations and returns their responses.

			  PARAMETERS:
			    Each argument is a property table with the following fields:

			    - task_title: string (optional) — Title of the task used for UI display.

			    - messages: table (required) — Array of message tables.
			      The LAST message MUST be a "user" message.

			      Each message table has a "role" field:

			      role = "system":
			        - content: string — system instruction

			      role = "user":
			        - content: string — user message text
			        - attachments: table (optional) — array of attachment objects
			          Currently supports images via `image.load()` / `image.create()`:
			          { image.load("path.png") }

			      role = "assistant":
			        - content: string — assistant response text
			        - reasoning_content: string (optional) — reasoning/thinking text
			        - tool_calls: table (optional) — array of tool call tables
			          Each tool call (table):
			          - tool_name: string
			          - tool_call_id: string
			          - arguments: table — arguments matching the tool's schema
			          - result_success: boolean (optional) — whether the tool execution succeeded
			          - result_content: string (optional) — tool output text
			          - result_attachments: table (optional) — array of attachment objects
			        - attachments: table (optional) — array of attachment objects produced by the assistant

			    - model: string (optional) — Name of the model to use.
			      If omitted, the chat's "AgenticToolsModel" is used.

			    - tools: table (optional) — Mixed array of tool names (strings) and/or
			      callback tool definitions (tables). If omitted, no tools are available.

			      String entries reference registered tools by name:
			        { "web-search", "fs-read_entry" }

			      Table entries define ad-hoc Lua callback tools:
			        {
			          name = "my_tool",
			          display_name = "My Tool", -- (optional) display name for UI
			          description = "Does something useful.",
			          parameters = {
			            type = "object",
			            properties = {
			              x = { type = "number" },
			              y = { type = "number" }
			            },
			            required = { "x", "y" }
			          },
			          callback = function(args)
			            return "Result: " .. (args.x + args.y)
			          end
			        }

			      Mixed example: { "web-search", { name = "calc", ... } }

			    - skills: table (optional) — Mixed array of skill names (strings) and/or
			      ad-hoc skill definitions (tables). If omitted, no skills are available.

			      String entries reference registered skills by name:
			        { "webreaper", "skill-creator" }

			      Table entries define ad-hoc skills:
			        {
			          name = "my_skill",
			          description = "Does something useful.",
			          path = "C:/skills/my_skill/SKILL.md", -- (optional) path to the skill file
			          home_directory = "C:/skills/my_skill", -- (optional) home directory; defaults to the directory of "path"
			          body = "Skill instructions in Markdown..." -- or a Lua function
			        }

			      Ad-hoc skill fields:
			        - name: string (required) — skill name
			        - description: string (required) — description shown to the model
			        - path: string (optional) — path to the skill file
			        - home_directory: string (optional) — home directory; if omitted
			          and "path" is set, defaults to the path's directory
			        - body: string or function (required) — the skill body: a string
			          is returned as-is; a Lua function is invoked on demand to produce
			          the body (can be async).

			      Mixed example: { "webreaper", { name = "my_skill", ... } }

			    - memory_blocks: table (optional) — Array of memory block names (strings)
			      to make accessible to the agent via memory tools (search, record, etc.).
			      The blocks are resolved from the executing agent's memory attachments;
			      memory must be enabled for both the chat and the agent. If omitted,
			      no memory tools are available to the agent.

			        { "project_notes", "user_preferences" }

			    - sub_agents: table (optional) — Array of registered sub-agent names (strings)
			      to make available to the agent. The agent can then delegate sub-tasks
			      to them via the agent-callsub tool. The sub-agents are resolved from
			      the available sub-agent set (chat and profile sources). If omitted,
			      no sub-agents are available to the agent.

			        { "code-reviewer", "docs-writer" }

			  RETURNS:
			    - If a single property table is passed: table — array of response messages
			      (same format as input messages, excluding the input messages themselves).
			    - If multiple property tables are passed (batch): table — array of response arrays,
			      one per input property table. If an individual agent fails, its entry
			      is an error string instead of a response array.

			  THROWS an error if:
			    - the agentic model is not configured or the specified model is not found
			    - the last message is not a "user" message
			    - any message has an unknown role
			    - any of the property tables is invalid (in batch mode, all errors are collected)

			  Use pcall() / try-catch for safe error handling.

			  BATCH EXECUTION:
			    Pass multiple property tables as separate arguments:
			      dass.agents.execute(batch1, batch2, batch3, ...)

			    Or pass an array of property tables as a single argument:
			      dass.agents.execute({ batch1, batch2, batch3, ... })

			    Each agent executes independently and concurrently. Errors in one
			    do not affect others. The function throws only when there's an error
			    in agent parameters; runtime errors are returned as strings in the result.

			MESSAGES:
			  Input messages use the format described above.

			  Output messages have the same structure:
			    - "system" messages: { role = "system", content = "..." }
			    - "user" messages: { role = "user", content = "...", attachments = {...} }
			    - "assistant" messages:
			      { role = "assistant", content = "...", reasoning_content = "...",
			        tool_calls = { { tool_name, tool_call_id, arguments, result_success, result_content, result_attachments }, ... },
			        attachments = {...},
			        usage = { input_tokens, output_tokens, input_cache_hit_tokens,
			          input_cache_miss_tokens, time_to_first_token (ms),
			          inference_time (ms), execution_time (ms) } }

			  Notes:
			  - tool results are embedded directly in the tool_call table
			    (result_success, result_content, result_attachments), NOT as separate
			    "tool" role messages. This differs from OpenAI's API convention.
			  - assistant messages can include "usage" table with usage statistics,
			    which is not required for input messages.

			EXAMPLES:

			  -- Simple greeting
			  local r = await dass.agents.execute({
			    task_title = "Greeting",
			    messages = {
			      { role = "system", content = "You are a helpful assistant." },
			      { role = "user", content = "Say hello!" }
			    }
			  })
			  print(table.last(r).content)

			  -- With custom model and tools
			  local r = await dass.agents.execute({
			    task_title = "Calculation",
			    messages = {
			      { role = "system", content = "You can use tools." },
			      { role = "user", content = "What is 2+2?" }
			    },
			    model = "openrouter$google/gemini-3.5-flash",
			    tools = { "math-calculate" }
			  })
			  print(table.last(r).content)

			  -- Multi-turn with tools (iterate through all messages)
			  local messages = {
			    { role = "system", content = "You can use web-search." },
			    { role = "user", content = "Search for latest news about AI" }
			  }
			  local r = await dass.agents.execute({
			    task_title = "Finding news",
			    messages = messages,
			    tools = { "web-search" }
			  })
			  for _, msg in ipairs(r) do
			    table.insert(messages, msg)
			    if msg.role == "assistant" then
			      print("AI:", msg.content)
			      if msg.tool_calls then
			        for _, tc in ipairs(msg.tool_calls) do
			          print("  -> tool:", tc.tool_name)
			          print("  -> success:", tc.result_success)
			          print("  -> result:", tc.result_content:sub(1, 100))
			        end
			      end
			    end
			  end
			
			  table.insert(messages, { role = "user", content = "Okay, now search for tasty breakfast recipes" })
			  r = await dass.agents.execute({
			    task_title = "Finding breakfast recipes",
			    messages = messages,
			    tools = { "web-search" }
			  })
			  -- Process messages again...

			  -- Image attachment
			  local r = await dass.agents.execute({
			    task_title = "Image description",
			    messages = {
			      { role = "system", content = "You are image description assistant." },
			      { role = "user", content = "Describe this image.", attachments = { image.load("image.png") } }
			    },
			    model = "openrouter$google/gemini-3.5-flash"  -- Use a vision model
			  })
			  print(table.last(r).content)

			  -- Safe execution with try-catch
			  try
			    local result = await dass.agents.execute({
			      task_title = "Math question",
			      messages = {
			        { role = "system", content = "You are an expert." },
			        { role = "user", content = "What is 2+2?" }
			      }
			    })
			    print("Answer:", table.last(result).content)
			  catch error do
			    print("Failed:", error)
			  end

			  -- Custom callback tool
			  local r = await dass.agents.execute({
			    task_title = "Custom callback tool example",
			    messages = {
			      { role = "system", content = "Use the calculator tool for math." },
			      { role = "user", content = "What is 123 * 456?" }
			    },
			    tools = {
			      {
			        name = "calculate",
			        display_name = "Calculator",  -- Optional, for UI display
			        description = "Multiplies two integers.",
			        parameters = {
			          type = "object",
			          properties = {
			            a = { type = "number", description = "First number" },
			            b = { type = "number", description = "Second number" }
			          },
			          required = { "a", "b" }
			        },
			        callback = function(args)
			          local result = args.a * args.b
			          return tostring(result)
			        end
			      }
			    }
			  })
			  print(table.last(r).content)  -- "123 * 456 = 56088"

			  -- With ad-hoc skill
			  local r = await dass.agents.execute({
			    task_title = "Skill example",
			    messages = {
			      { role = "system", content = "Use the skill to analyze code." },
			      { role = "user", content = "What issues does this code have?" }
			    },
			    skills = {
			      {
			        name = "code-analyzer",
			        description = "Analyzes C# code for issues.",
			        body = "You are a code analyzer. Focus on correctness, performance and style."
			      }
			    }
			  })
			  print(table.last(r).content)

			  -- With sub-agents
			  local r = await dass.agents.execute({
			    task_title = "Code review",
			    messages = {
			      { role = "system", content = "You can delegate sub-tasks to sub-agents." },
			      { role = "user", content = "Review the code and write documentation for it." }
			    },
			    sub_agents = { "code-reviewer", "docs-writer" }
			  })
			  print(table.last(r).content)

			  -- Batch execution: run multiple agents concurrently
			  local results = await dass.agents.execute(
			    {
			      task_title = "Poet",
			      messages = {
			        { role = "system", content = "You are a poet." },
			        { role = "user", content = "Write a haiku about coding." }
			      }
			    },
			    {
			      task_title = "Comedian",
			      messages = {
			        { role = "system", content = "You are a comedian." },
			        { role = "user", content = "Tell me a programming joke." }
			      }
			    },
			    {
			      task_title = "Error test",
			      messages = {
			        { role = "system", content = "You are a helpful assistant." },
			        { role = "user", content = "This one will fail!" }
			      }
			    }
			  )
			  -- results[1] is the poet's response array, results[2] is the comedian's,
			  -- and results[3] contains an error message as a string
			  print("Haiku:", table.last(results[1]).content)
			  print("Joke:", table.last(results[2]).content)
			  print("Failed:", results[3])  -- Error message

			  -- Batch via array
			  local results = await dass.agents.execute({
			    { messages = { ... } },
			    { messages = { ... } }
			  })

			NOTES:
			  - By default, the agent uses the chat's "AgenticToolsModel" setting.
			  - You can override the model by passing a "model" field.
			  - No tools are available by default; you must explicitly pass them using the "tools" field.
			  - Use `table.last(response)` to get the final assistant message, skipping intermediate tool calls.
			  - CALLBACK TOOLS: pass table entries in the "tools" array with:
			    name (string), description (string), parameters (JSON Schema table),
			    and callback (function). The callback receives a table of arguments
			    matching the schema and should return a string. Callbacks can use the full
			    Lua API (fs, web, dass.*, etc.). Callbacks can be async.
			  - SKILLS: pass string entries (registered skill names) and/or table entries
			    (ad-hoc skills) in the "skills" array. Ad-hoc skills require: name (string),
			    description (string), and body (string or function). "path" and
			    "home_directory" are optional; home_directory defaults to the directory
			    of "path". A function body is invoked on demand to produce the skill body
			    and can be async.
			  - SUB-AGENTS: pass string entries (registered sub-agent names) in the
			    "sub_agents" array. Sub-agents are resolved from the available sub-agent
			    set built from the chat and profile sources; unknown names cause an error.
			  - Image attachments: use `image.load(path)` or `image.create(width, height)`
			    to create attachment objects.
			  - Returns the full conversation history produced by the agent AFTER the input
			    messages, including all intermediate assistant messages with tool calls.
			  - Tool call results are embedded directly in the tool_call table
			    (result_success, result_content, result_attachments), not as separate messages.
			  - Assistant messages may include a "usage" table with token counts and timing
			    information: input_tokens, output_tokens, input_cache_hit_tokens,
			    input_cache_miss_tokens, time_to_first_token (ms), inference_time (ms),
			    execution_time (ms). Each property can be 0 if unknown.
			  - BATCH EXECUTION: pass multiple property tables to `execute()` to run
			    multiple agents concurrently. Each call is independent and errors
			    in one do not affect others. The function throws an exception only when
			    there's an error in agent's parameters; runtime errors are returned as strings.
			""";

		private readonly IChatSettingsService _chatSettings;
		private readonly IAgentTaskExecutor _agentTaskExecutor;
		private readonly IModelManager _modelManager;
		private readonly IAgentManagementService _agentManager;
		private readonly IAddonSetCollector<SkillInfo> _skillsetBuilder;
		private readonly IAddonSetCollector<SubAgentInfo> _subAgentsetCollector;
		private readonly IToolsetCacheService _toolsetCache;
		private LuaService _luaService = null!;

		public LuaApiAgents(IChatSettingsService chatSettings, IAgentTaskExecutor agentTaskExecutor, IModelManager modelManager,
			IAgentManagementService agentManager, IAddonSetCollector<SkillInfo> skillsetBuilder,
			IAddonSetCollector<SubAgentInfo> subAgentsetCollector, IToolsetCacheService toolsetCache)
		{
			_chatSettings = chatSettings;
			_agentTaskExecutor = agentTaskExecutor;
			_modelManager = modelManager;
			_agentManager = agentManager;
			_skillsetBuilder = skillsetBuilder;
			_subAgentsetCollector = subAgentsetCollector;
			_toolsetCache = toolsetCache;
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			_luaService = luaService;
			ns["execute"] = new LuaCallbackFunction(ExecuteAsync);
			return null;
		}

		private async Task<LuaTuple> ExecuteAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("dass.agents.execute(properties...): at least 1 argument expected.");
			for (int i = 0; i < args.Length; i++)
				if (args[i].Type != LuaType.Table)
					throw new LuaRuntimeException("dass.agents.execute(): all arguments must be tables.");

			List<Func<Task<LuaTable>>> executionFunctions = [];
			List<Exception?> exceptions = [];

			for (int i = 0; i < args.Length; i++)
			{
				var arg = (LuaTable)args[i];
				if (arg.IsArrayTable())
				{
					foreach (var item in arg.Values)
					{
						try
						{
							var executionFunction = PrepareExecutionFunction(ctx, (LuaTable)item);
							executionFunctions.Add(executionFunction);
							exceptions.Add(null);
						}
						catch (Exception ex)
						{
							exceptions.Add(ex);
						}
					}
				}
				else
				{
					try
					{
						var executionFunction = PrepareExecutionFunction(ctx, arg);
						executionFunctions.Add(executionFunction);
						exceptions.Add(null);
					}
					catch (Exception ex)
					{
						exceptions.Add(ex);
					}
				}
			}

			var nonNullExceptions = exceptions.Where(ex => ex != null).ToList();
			if (nonNullExceptions.Count > 0)
				throw new LuaRuntimeException($"dass.agents.execute(): {string.Join(", ", nonNullExceptions.Select(e => e!.Message))}");

			var tasks = executionFunctions.Select(f => f()).ToList();

			if (tasks.Count == 1)
			{
				try
				{
					var task = tasks[0];
					return new LuaTuple(await task);
				}
				catch (Exception ex)
				{
					throw new LuaRuntimeException($"dass.agents.execute(): {ex.Message}");
				}
			}
			else
			{
				try
				{
					var result = new LuaTable();

					for (int i = 0; i < tasks.Count; i++)
					{
						var task = tasks[i];
						try
						{
							result.Append(await task);
						}
						catch (Exception ex)
						{
							result.Append(new LuaString(ex.Message));
						}
					}

					return new LuaTuple(result);
				}
				catch (Exception ex)
				{
					throw new LuaRuntimeException($"dass.agents.execute(): {ex.Message}");
				}
			}
		}

		private Func<Task<LuaTable>> PrepareExecutionFunction(LuaCallingContext ctx, LuaTable parameters)
		{
			var messagesArg = parameters.Get("messages");
			if (messagesArg is not LuaTable messagesTable)
				throw new Exception("'messages' must be a table.");

			var messages = new List<AgentChatMessage>();

			for (int i = 1; i <= messagesTable.Length; i++)
			{
				var _messageTable = messagesTable.Get(i);
				if (_messageTable is not LuaTable messageTable)
					throw new Exception("each message must be a table.");
				messages.Add(ConvertMessageFromLua(messageTable));
			}

			if (messages.Count == 0 || messages[^1] is not AgentUserMessage)
				throw new Exception("last message must be an user message.");

			// Resolve model name and LLM.
			var modelName = (parameters.Get("model") as LuaString)?.Value;
			if (string.IsNullOrEmpty(modelName))
				modelName = _chatSettings.Settings.Models.GetEffectiveSelection().AgenticToolsModel;
			if (string.IsNullOrEmpty(modelName))
				throw new Exception("agentic model is not selected.");

			LLModel llm;
			try
			{
				llm = _modelManager.GetModel(modelName);
			}
			catch (Exception ex)
			{
				throw new Exception($"model '{modelName}' is not available: {ex.Message}");
			}

			// Resolve tools: mixed array of strings (registered tools) and tables (callback tools)
			var tools = new List<AgentTool>();
			var toolsOption = parameters.Get("tools");
			if (toolsOption is LuaTable toolsOptionTable)
			{
				foreach (var toolValue in toolsOptionTable.Values)
				{
					if (toolValue is LuaString toolValueString)
					{
						if (!_toolsetCache.AvailableTools.TryGetValue(toolValueString.Value, out var tool))
							throw new Exception($"tool '{toolValueString.Value}' is not available.");

						tools.Add(new ChatAgentTool(tool, null));
					}
					else if (toolValue is LuaTable toolValueTable)
					{
						var name = (toolValueTable.Get("name") as LuaString)?.Value;
						var displayName = (toolValueTable.Get("display_name") as LuaString)?.Value;
						var desc = (toolValueTable.Get("description") as LuaString)?.Value;
						var schema = StructuredLuaConverter.LuaValueToJsonNode(toolValueTable.Get("parameters"));
						var callback = toolValueTable.Get("callback");

						if (string.IsNullOrEmpty(name))
							throw new Exception("callback tool definition: 'name' is required.");
						if (string.IsNullOrEmpty(desc))
							throw new Exception($"callback tool definition '{name}': 'description' is required.");
						if (callback is not LuaFunction func)
							throw new Exception($"callback tool definition '{name}': 'callback' must be a function.");

						tools.Add(new LuaAdHocAgentTool(name, displayName ?? name, desc, schema as JsonObject ?? [], ctx, func)
						{
							ApprovalLevel = ToolApprovalLevel.PolicyAutoApproveUnlessDisallowed
						});
					}
				}
			}

			var skills = new List<AgentSkill>();
			var skillsOption = parameters.Get("skills");
			if (skillsOption is LuaTable skillsOptionTable)
			{
				var skillMap = skillsOptionTable.Values.Any(v => v is LuaString) ?
					_skillsetBuilder.GetAvailableAddons().ToImmutableDictionary(s => s.Name) :
					null;

				foreach (var skillValue in skillsOptionTable.Values)
				{
					if (skillValue is LuaString skillValueString)
					{
						if (!skillMap!.TryGetValue(skillValueString.Value, out var skill))
							throw new Exception($"skill '{skillValueString.Value}' is not available.");

						skills.Add(new ChatAgentSkill(skill));
					}
					else if (skillValue is LuaTable skillValueTable)
					{
						var name = (skillValueTable.Get("name") as LuaString)?.Value;
						var desc = (skillValueTable.Get("description") as LuaString)?.Value;
						var path = (skillValueTable.Get("path") as LuaString)?.Value;
						var homeDir = (skillValueTable.Get("home_directory") as LuaString)?.Value;
						if (path is not null && homeDir is null)
							homeDir = Path.GetDirectoryName(path);
						var body = skillValueTable.Get("body");

						if (string.IsNullOrEmpty(name))
							throw new Exception("ad-hoc skill definition: 'name' is required.");
						if (string.IsNullOrEmpty(desc))
							throw new Exception($"ad-hoc skill definition '{name}': 'description' is required.");
						if (body is LuaNil)
							throw new Exception($"ad-hoc skill definition '{name}': 'body' is required.");

						if (body is LuaString bodyStr)
						{
							if (string.IsNullOrEmpty(bodyStr.Value))
								throw new Exception($"ad-hoc skill definition '{name}': 'body' cannot be an empty string.");
							skills.Add(new LuaAdHocAgentSkill(name, desc, path, homeDir, bodyStr.Value));
						}
						else if (body is LuaFunction bodyFunc)
						{
							skills.Add(new LuaAdHocAgentSkill(name, desc, path, homeDir, ctx, bodyFunc));
						}
						else
						{
							throw new Exception($"ad-hoc skill definition '{name}': 'body' must be a string or function.");
						}
					}
				}
			}

			// Resolve sub-agents: explicit array of registered sub-agent names requested by the script.
			var subAgents = new List<TaskSubAgentDescriptor>();
			var subAgentsOption = parameters.Get("sub_agents");
			if (subAgentsOption is LuaTable subAgentsOptionTable)
			{
				var subAgentMap = subAgentsOptionTable.Values.Any(v => v is LuaString) ?
					_subAgentsetCollector.GetAvailableAddons().ToImmutableDictionary(s => s.Name) :
					null;

				foreach (var subAgentValue in subAgentsOptionTable.Values)
				{
					if (subAgentValue is not LuaString subAgentName)
						throw new Exception("sub_agents must be an array of sub-agent name strings.");

					if (!subAgentMap!.TryGetValue(subAgentName.Value, out var subAgentInfo))
						throw new Exception($"sub-agent '{subAgentName.Value}' is not available.");

					subAgents.Add(new TaskSubAgentDescriptor
					{
						Name = subAgentInfo.Name,
						Description = subAgentInfo.Description
					});
				}
			}

			// Resolve memory blocks: explicit array of block names requested by the script.
			var memoryBlocks = new List<TaskMemoryBlock>();
			var memoryBlocksOption = parameters.Get("memory_blocks");
			if (memoryBlocksOption is LuaTable memoryBlocksOptionTable)
			{
				var tec = ctx.TryGetToolExecutionContext();
				var senderAgent = tec != null && tec.Message is AssistantMessage am ?
					_agentManager.TryGetAgentDescriptor(am.SenderAgentId) : null;
				var available = tec != null && senderAgent != null
					? TaskMemoryBlock.ResolveBlocks(tec.Chat, senderAgent)
					: [];
				var availableMap = available.ToImmutableDictionary(b => b.Block.Name);

				foreach (var blockValue in memoryBlocksOptionTable.Values)
				{
					var blockName = (blockValue as LuaString)?.Value;
					if (string.IsNullOrEmpty(blockName))
						throw new Exception("memory_blocks must be an array of block name strings.");

					if (!availableMap.TryGetValue(blockName, out var block))
						throw new Exception($"memory block '{blockName}' is not available.");

					memoryBlocks.Add(block);
				}
			}

			var taskTitle = parameters.Get("task_title") is LuaString taskTitleStr ? taskTitleStr.Value : null;

			async Task<LuaTable> ExecuteAgent()
			{
				var tec = ctx.TryGetToolExecutionContext();
				var senderAgent = tec != null && tec.Message is AssistantMessage am ?
					_agentManager.TryGetAgentDescriptor(am.SenderAgentId) : null;
				var agentToolSettings = senderAgent?.Tools;

				var policy = agentToolSettings?.GetEffectivePolicy(_chatSettings.Settings)
					?? _chatSettings.Settings.InheritedAgentSettings.Tools.GetEffectivePolicy(_chatSettings.Settings);
				ToolBehaviour autoApproveBehaviours = policy.AutoApproveBehaviours,
					disallowedBehaviours = policy.DisallowedBehaviours;

				var agentTask = _agentTaskExecutor.Execute(new AgentTaskLaunchParameters
				{
					TaskName = taskTitle,
					TriggeredChat = tec?.Chat,
					TriggeredMessage = tec?.Message,
					Model = llm,
					InitialMessages = [.. messages],
					Tools = [.. tools],
					Skills = [.. skills],
					SubAgents = [.. subAgents],
					MemoryBlocks = [.. memoryBlocks],
					AutoApproveBehaviours = autoApproveBehaviours,
					DisallowedBehaviours = disallowedBehaviours
				}, ctx.CancellationToken);
				await agentTask;

				var resultTable = new LuaTable();
				foreach (var message in agentTask.Messages.Skip(messages.Count))
					resultTable.Append(ConvertMessageToLua(message));
				return resultTable;
			}

			return ExecuteAgent;
		}

		private static AgentChatMessage ConvertMessageFromLua(LuaTable messageTable)
		{
			var role = messageTable.Get("role").ToString();
			var content = messageTable.Get("content").ToString();

			switch (role)
			{
				case "system":
					return new AgentSystemMessage { Content = content };

				case "user":
					var attachmentsTable = messageTable.Get("attachments");
					var attachments = new List<AgentAttachment>();
					foreach (var attachmentValue in (attachmentsTable as LuaTable)?.Values ?? [])
						attachments.Add(ConvertAttachmentFromLua(attachmentValue));
					return new AgentUserMessage { Content = content, Attachments = [..attachments] };

				case "assistant":
					var reasoningContent = messageTable.Get("reasoning_content").ToString();

					var toolCallsTable = messageTable.Get("tool_calls");
					var toolCalls = new List<AgentToolCall>();
					foreach (var toolCallTable in (toolCallsTable as LuaTable)?.Values ?? [])
						toolCalls.Add(ConvertToolCallFromLua((LuaTable)toolCallTable));

					attachmentsTable = messageTable.Get("attachments");
					attachments = new List<AgentAttachment>();
					foreach (var attachmentValue in (attachmentsTable as LuaTable)?.Values ?? [])
						attachments.Add(ConvertAttachmentFromLua(attachmentValue));

					return new AgentAssistantMessage
					{
						ReasoningContent = reasoningContent,
						Content = content,
						Attachments = [.. attachments],
						ToolCalls = [.. toolCalls]
					};

				default:
					throw new LuaRuntimeException($"dass.agents.execute(): unknown role '{role}'.");
			}
		}

		private static AgentAttachment ConvertAttachmentFromLua(LuaValue attachmentValue)
		{
			if (attachmentValue is LuaUserData userData)
			{
				if (userData.Target is LuaImage image)
				{
					return AgentAttachment.FromBase64(AgentAttachmentType.Image, image.Format, image.ToBase64(), image);
				}
			}
			throw new Exception("Unsupported attachment value: " + attachmentValue.ToString());
		}

		private static LuaValue? TryConvertAttachmentToLua(AgentAttachment attachment)
		{
			switch (attachment.Type)
			{
				case AgentAttachmentType.Image:

					// Convert back
					if (attachment.Source is LuaImage luaImage)
						return LuaValueConverter.ToLuaValue(luaImage);

					break;
			}

			return null;
		}

		private static AgentToolCall ConvertToolCallFromLua(LuaTable toolCallTable)
		{
			var toolName = toolCallTable.Get("tool_name").ToString();
			var toolCallId = toolCallTable.Get("tool_call_id").ToString();
			var arguments = StructuredLuaConverter.LuaValueToJsonNode(toolCallTable.Get("arguments"));

			var resultSuccess = toolCallTable.Get("result_success");
			var resultContent = toolCallTable.Get("result_content").ToString();
			var attachmentsTable = toolCallTable.Get("result_attachments");
			var attachments = new List<AgentAttachment>();
			foreach (var attachmentValue in (attachmentsTable as LuaTable)?.Values ?? [])
				attachments.Add(ConvertAttachmentFromLua(attachmentValue));

			return new AgentToolCall
			{
				ToolCallId = toolCallId,
				ToolName = toolName,
				Arguments = arguments?.ToJsonString() ?? "{}",
				Result = new AgentToolCallResult
				{
					Success = resultSuccess is LuaNil || resultSuccess.ToBoolean(),
					Content = resultContent
				}
			};
		}

		private static LuaTable ConvertMessageToLua(AgentChatMessage message)
		{
			switch (message)
			{
				case AgentSystemMessage systemMessage:
					var systemMessageTable = new LuaTable();
					systemMessageTable.Set("role", "system");
					systemMessageTable.Set("content", systemMessage.Content);
					return systemMessageTable;

				case AgentUserMessage userMessage:
					var userMessageTable = new LuaTable();
					userMessageTable.Set("role", "user");
					userMessageTable.Set("content", userMessage.Content);
					var attachmentsTable = new LuaTable();
					foreach (var attachment in userMessage.Attachments)
						if (TryConvertAttachmentToLua(attachment) is LuaValue attachmentValue)
							attachmentsTable.Append(attachmentValue);
					userMessageTable["attachments"] = attachmentsTable;
					return userMessageTable;

				case AgentAssistantMessage assistantMessage:
					var assistantMessageTable = new LuaTable();
					assistantMessageTable.Set("role", "assistant");
					assistantMessageTable.Set("reasoning_content", assistantMessage.ReasoningContent);
					assistantMessageTable.Set("content", assistantMessage.Content);
					attachmentsTable = new LuaTable();
					foreach (var attachment in assistantMessage.Attachments)
						if (TryConvertAttachmentToLua(attachment) is LuaValue attachmentValue)
							attachmentsTable.Append(attachmentValue);
					assistantMessageTable["attachments"] = attachmentsTable;
					var toolCallsTable = new LuaTable();
					foreach (var toolCall in assistantMessage.ToolCalls)
						toolCallsTable.Append(ConvertToolCallToLua(toolCall));
					assistantMessageTable["tool_calls"] = toolCallsTable;
					if (assistantMessage.UsageStatistics != null)
					{
						var usageTable = new LuaTable();
						usageTable.Set("input_tokens", assistantMessage.UsageStatistics.InputTokens);
						usageTable.Set("output_tokens", assistantMessage.UsageStatistics.OutputTokens);
						usageTable.Set("input_cache_hit_tokens", assistantMessage.UsageStatistics.InputCacheHitTokens);
						usageTable.Set("input_cache_miss_tokens", assistantMessage.UsageStatistics.InputCacheMissTokens);
						usageTable.Set("time_to_first_token", assistantMessage.UsageStatistics.TimeToFirstToken.TotalMilliseconds);
						usageTable.Set("inference_time", assistantMessage.UsageStatistics.InferenceTime.TotalMilliseconds);
						usageTable.Set("execution_time", assistantMessage.UsageStatistics.ExecutionTime.TotalMilliseconds);
						assistantMessageTable["usage"] = usageTable;
					}
					return assistantMessageTable;

				default:
					throw new LuaRuntimeException($"dass.agents.execute(): unknown message '{message}'.");
			}
		}

		private static LuaTable ConvertToolCallToLua(AgentToolCall toolCall)
		{
			var resultTable = new LuaTable();
			resultTable.Set("tool_call_id", toolCall.ToolCallId);
			resultTable.Set("tool_name", toolCall.ToolName);
			resultTable.Set("arguments", StructuredLuaConverter.JsonNodeToLuaValue(TolerantJsonParser.Parse(toolCall.Arguments)));
			resultTable.Set("result_success", toolCall.Result?.Success ?? true);
			resultTable.Set("result_content", toolCall.Result?.Content ?? "");
			var attachmentsTable = new LuaTable();
			foreach (var attachment in toolCall.Result?.Attachments ?? [])
				if (TryConvertAttachmentToLua(attachment) is LuaValue attachmentValue)
					attachmentsTable.Append(attachmentValue);
			resultTable["result_attachments"] = attachmentsTable;
			return resultTable;
		}
	}
}
// 666 строчек епта (уже неактуально, но пусть будет)