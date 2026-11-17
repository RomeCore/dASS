using System.Text.Json.Nodes;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting.Skills;
using LLMDesktopAssistant.Providers;
using LLMDesktopAssistant.Scripting.Lua.API;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Utils;
using RCLargeLanguageModels;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// The <c>dass.agent.execute_task</c> half of the <c>dass.agent</c> namespace: launching agentic tasks and the
	/// <c>agent_task</c> handle they return.
	/// </summary>
	/// <remarks>
	/// Separate from <see cref="LuaApiAgents"/>, which only inspects chat agents: launching a task drags in the task
	/// executor, the model manager and every addon set, and none of that belongs in the inspection API.
	/// </remarks>
	[LuaApi(chatScoped: true)]
	public class LuaApiAgentsExecution(IChatSettingsService chatSettings, IAgentTaskExecutor agentTaskExecutor,
		IModelManager modelManager, IAgentManagementService agentManager, IAddonSetCollector<SkillInfo> skillsetCollector,
		IAddonSetCollector<SubAgentInfo> subAgentsetCollector, IAddonSetCollector<ToolInfo> toolsetCollector) : LuaApiBase
	{
		public override string? Namespace => "dass.agent";

		public override string? Manuals => """
			--- dass.agent - agentic task API

			Runs an agent - a model with tools, skills, sub-agents and memory - outside the chat flow and hands back
			a handle you control: await it, read its messages at any moment, send follow-ups, cancel it.

			By default the agent uses the model configured as "AgenticToolsModel" in chat settings. No tools,
			skills, sub-agents or memory blocks are available unless you ask for them.

			FUNCTIONS:

			--- dass.agent.execute_task(properties) -> agent_task
			  Launches ONE task and returns its handle immediately. The task starts in the background and keeps
			  running while the script does something else; awaiting is optional and never part of launching.

			  Launching several tasks is several calls: they run concurrently, and awaiting them in turn costs the
			  longest one, not the sum. There is no batch form and no need for one.

			  PARAMETERS (one table):

			    - messages: table (required) - array of message tables, see MESSAGES below.
			      The LAST message MUST be a "user" message.

			    - task_title: string (optional) - title of the task, used for UI display.

				- hidden: boolean (optional) - keep this task out of the message it was triggered by and out of
				  the chat's task list. The application-wide task list still shows it. Use it for internal steps of
				  a longer job - a game loop, a pipeline - that would otherwise flood the chat with dozens of cards.

			    - model: string (optional) - name of the model to use, e.g. "openrouter$google/gemini-3.5-flash".
			      If omitted, the chat's "AgenticToolsModel" is used.

			    - tools: table (optional) - mixed array of tool names (strings) and/or callback tool definitions
			      (tables). If omitted, no tools are available.

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

			      The callback receives a table of arguments matching the schema and returns a string. It may be
			      async and may use the full Lua API (fs, web, dass.*, image, ...).

			      Mixed example: { "web-search", { name = "calc", ... } }

				  A tool that takes no arguments may pass an empty `properties = {}` - it is read as an empty map,
				  not as an empty list, so the model accepts the schema.

			    - skills: table (optional) - mixed array of skill names (strings) and/or ad-hoc skill definitions
			      (tables). If omitted, no skills are available.

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
			        - name: string (required) - skill name
			        - description: string (required) - description shown to the model
			        - path: string (optional) - path to the skill file
			        - home_directory: string (optional) - home directory; if omitted
			          and "path" is set, defaults to the path's directory
			        - body: string or function (required) - the skill body: a string
			          is returned as-is; a Lua function is invoked on demand to produce
			          the body (can be async). An empty string is an error.

			      Mixed example: { "webreaper", { name = "my_skill", ... } }

			    - memory_blocks: table (optional) - array of memory block names to make accessible to the agent
			      through the memory tools (search, record, ...). The blocks are resolved from the executing
			      agent's memory attachments; memory must be enabled for both the chat and the agent. If omitted,
			      no memory tools are available.

			        { "project_notes", "user_preferences" }

			    - sub_agents: table (optional) - array of registered sub-agent names to make available to the
			      agent, which can then delegate sub-tasks to them via the agent-callsub tool. The sub-agents are
			      resolved from the available sub-agent set (chat and profile sources); unknown names are an error.
			      If omitted, no sub-agents are available.

			        { "code-reviewer", "docs-writer" }

			    - timeout: number|false (optional, default 1800) - seconds the task may run before it is cancelled.
			      false or 0 disables the timeout. An expired timeout surfaces as status "cancelled": the executor
			      leaves no mark that would separate it from a Stop or from t:cancel().

			    - expiration: number|false (optional, default 300) - seconds after the task finishes before it is
			      removed from the task lists. false means it is never removed, 0 means immediately. The handle
			      keeps working either way, and a follow-up through it puts the task back into the lists.

			  RETURNS: agent_task - the handle of the launched task (see TASK HANDLE below).

			  THROWS a Lua error for launch-time validation only:
			    - the agentic model is not configured, or the named model is not available
			    - "messages" is missing, is not a table, or its last entry is not a "user" message
			    - any message has an unknown role
			    - a tool, skill, sub-agent or memory block is not available
			    - timeout/expiration is neither a number of seconds nor false

			  A launch refused by a `task.start` hook is NOT an error: it is a task with status "blocked" whose
			  block_reason says why.

			TASK HANDLE ('agent_task'):

			  execute_task hands you a handle, not a transcript. Everything is read from it, at any moment.

			  Properties (read-only; every read builds a fresh value):
			    - id: string - the task's GUID.
			    - status: string - one of:
			      "pending"   - created, the first request has not started yet
			      "executing" - running
			      "success"   - finished, the model produced its final answer
			      "failed"    - finished with an error, see error / error_type
			      "cancelled" - cancelled by t:cancel(), by Stop, by a closed chat or by the timeout
			      "blocked"   - a `task.start` hook refused the launch, see block_reason
			    - completed: boolean - true once the current launch has settled, whatever the outcome.
			    - error: string or nil - the failure message; nil while running, after success and when blocked.
			    - error_type: string or nil - the exception type behind "error".
			    - block_reason: string or nil - why a `task.start` hook refused the launch.
			    - last_content: string or nil - the text of the last generated assistant message, i.e. the answer.
			    - usage, new_usage: table or nil - usage totals for the whole task and for the last launch only,
			      in the same shape as an assistant message's "usage"; nil until the first response.
			    - messages: table - the whole conversation, the messages you passed included.
			    - added_messages: table - every message produced after the ones you passed.
			    - new_messages: table - every message produced by the CURRENT launch. Follow-ups you sent count
			      as part of the launch, so they appear here; on the first launch it equals added_messages.

			  A snapshot is neither live nor cached: each read builds a new table from the running task, which
			  means the last assistant message may be unfinished while a response streams, and a hot loop reading
			  many messages is expensive - poll status/completed instead.

			  Methods:
			    - async t:wait([seconds]) -> t, completed
			      Waits until the task settles, or until the given number of seconds runs out.

			      seconds: a number of seconds; false, 0, a negative number or no argument at all waits as long as
			      it takes. The second return is true when the task stood still and false when the wait ran out of
			      time while the task is still working.

			      NEVER throws for the task's own failure, cancellation or block: those are values on the handle.
				  Only the host token still cancels the script - Stop, a closed chat, the end of the tool call that
				  started the script - and that is a cancellation, not a Lua error either.

				  It IS async, so an invalid argument surfaces on the await: catch it with `pcall_async` or
				  `try/catch`, never with plain `pcall`, which would only hand you the task.

			    - async t:send(...) -> t
			      Adds follow-up messages and returns the handle synchronously, so `await t:send(...)` waits for
			      the continuation, while `t:send(...)` alone merely queues it.

			      Arguments, any number, in order:
			        - a string - one user message;
			        - a message table - the same shape the launch accepts, any role;
			        - an array of message tables.
			      The last message must be a "user" message. An invalid message throws.

			      Allowed while the task is running (queued and consumed right after the current tool batch) and
			      after it finished (the task is relaunched). A follow-up sent in the last instant before a running
			      task stops can be consumed by the next send instead - a known race of the executor, not of the API.

			    - t:cancel([cascade]) -> boolean
			      Cancels the task. cascade defaults to true and also cancels every sub-task it spawned; pass false
			      to stop only this task and leave its sub-tasks running. The boolean says whether THIS task was
			      still cancellable: true when the cancel fired now, false when it had already settled. A sub-task's
			      fate never changes it. Does not wait for the task to actually stop.

			  AWAITING:

			    `await t` is exactly `t:wait()` and hands the handle back, so awaiting never produces a value of
			    its own:

			      await t              -- returns t itself, now settled
			      await t:send("...")  -- waits for the continuation

			    There is nothing to catch around an await: read t.status afterwards.

			MESSAGES:

			  Input messages use the format described below.

			    role = "system":
			      - content: string - system instruction

			    role = "user":
			      - content: string - user message text
			      - attachments: table (optional) - array of attachment objects
			        Currently supports images via `image.load()` / `image.create()`:
			        { image.load("path.png") }

			    role = "assistant":
			      - content: string - assistant response text
			      - reasoning_signature: string (optional) - reasoning/thinking signature
			      - reasoning_content: string (optional) - reasoning/thinking text
			      - tool_calls: table (optional) - array of tool call tables
			        Each tool call (table):
			        - tool_name: string
			        - tool_call_id: string
			        - arguments: table - arguments matching the tool's schema
			        - result_success: boolean (optional) - whether the tool execution succeeded
			        - result_content: string (optional) - tool output text
			        - result_attachments: table (optional) - array of attachment objects
			      - attachments: table (optional) - array of attachment objects produced by the assistant

			  Output messages have the same structure:
			    - "system" messages: { role = "system", content = "..." }
			    - "user" messages: { role = "user", content = "...", attachments = {...} }
			    - "assistant" messages:
			      { role = "assistant", content = "...", reasoning_signature = "...", reasoning_content = "...",
			        tool_calls = { { tool_name, tool_call_id, arguments, result_success, result_content, result_attachments }, ... },
			        attachments = {...},
			        usage = { input_tokens, output_tokens, cache_create_tokens,
			          input_cache_hit_tokens, input_cache_miss_tokens, time_to_first_token (ms),
			          inference_time (ms), execution_time (ms) } }

			  Notes:
			  - tool results are embedded directly in the tool_call table
			    (result_success, result_content, result_attachments), NOT as separate
			    "tool" role messages. This differs from OpenAI's API convention.
			  - assistant messages can include a "usage" table with usage statistics,
			    which is not required for input messages. Each property can be 0 if unknown.

			EXAMPLES:

			  -- Simple greeting
			  local t = dass.agent.execute_task{
			    task_title = "Greeting",
			    messages = {
			      { role = "system", content = "You are a helpful assistant." },
			      { role = "user", content = "Say hello!" }
			    }
			  }
			  await t
			  print(t.last_content)

			  -- With custom model and tools
			  local t = dass.agent.execute_task{
			    task_title = "Calculation",
			    messages = {
			      { role = "system", content = "You can use tools." },
			      { role = "user", content = "What is 2+2?" }
			    },
			    model = "openrouter$google/gemini-3.5-flash",
			    tools = { "math-calculate" }
			  }
			  await t
			  print(t.last_content)

			  -- Multi-turn with tools: the handle IS the conversation
			  local t = dass.agent.execute_task{
			    task_title = "Finding news",
			    messages = {
			      { role = "system", content = "You can use web-search." },
			      { role = "user", content = "Search for latest news about AI" }
			    },
			    tools = { "web-search" }
			  }
			  await t
			  for _, msg in ipairs(t.added_messages) do
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

			  -- Follow-up: no need to rebuild the message list by hand
			  await t:send("Okay, now search for tasty breakfast recipes")
			  print(t.last_content)
			  -- t.new_messages holds what the continuation produced, the follow-up included

			  -- Image attachment
			  local t = dass.agent.execute_task{
			    task_title = "Image description",
			    messages = {
			      { role = "system", content = "You are image description assistant." },
			      { role = "user", content = "Describe this image.", attachments = { image.load("image.png") } }
			    },
			    model = "openrouter$google/gemini-3.5-flash"  -- Use a vision model
			  }
			  await t
			  print(t.last_content)

			  -- Custom callback tool
			  local t = dass.agent.execute_task{
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
			  }
			  await t
			  print(t.last_content)  -- "123 * 456 = 56088"

			  -- With ad-hoc skill
			  local t = dass.agent.execute_task{
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
			  }
			  await t
			  print(t.last_content)

			  -- With sub-agents
			  local t = dass.agent.execute_task{
			    task_title = "Code review",
			    messages = {
			      { role = "system", content = "You can delegate sub-tasks to sub-agents." },
			      { role = "user", content = "Review the code and write documentation for it." }
			    },
			    sub_agents = { "code-reviewer", "docs-writer" }
			  }
			  await t
			  print(t.last_content)

			  -- Several tasks at once: launch all, then await; the wall clock is the longest one
			  local poet = dass.agent.execute_task{
			    task_title = "Poet",
			    messages = {
			      { role = "system", content = "You are a poet." },
			      { role = "user", content = "Write a haiku about coding." }
			    }
			  }
			  local comedian = dass.agent.execute_task{
			    task_title = "Comedian",
			    messages = {
			      { role = "system", content = "You are a comedian." },
			      { role = "user", content = "Tell me a programming joke." }
			    }
			  }
			  local reviewer = dass.agent.execute_task{
			    task_title = "Reviewer",
			    messages = {
			      { role = "system", content = "You are a reviewer." },
			      { role = "user", content = "Review the code you were not given." }
			    }
			  }
			  await poet, comedian, reviewer
			  print("Haiku:", poet.last_content)
			  print("Joke:", comedian.last_content)
			  print("Review:", reviewer.status, reviewer.error)  -- an outcome, not an exception

			  -- Polling instead of blocking: read progress while the task works
			  local t = dass.agent.execute_task{
			    task_title = "Long job",
			    messages = { { role = "user", content = "Do the long thing." } }
			  }
			  while not t.completed do
			    local _, done = t:wait(1)
			    if not done then
			      print("still working, produced:", #t.added_messages)
			    end
			  end
			  print("finished as", t.status)

			  -- Catching a bad argument: wait is async, so pcall_async, not pcall
			  local ok, err = pcall_async(function()
				return await t:wait("soon")
			  end)
			  print(ok, err)

			  -- Outcomes are values: no try/catch around the wait
			  local t = dass.agent.execute_task{
			    task_title = "Math question",
			    messages = {
			      { role = "system", content = "You are an expert." },
			      { role = "user", content = "What is 2+2?" }
			    }
			  }
			  await t
			  if t.status == "success" then
			    print("Answer:", t.last_content)
			  elseif t.status == "blocked" then
			    print("Blocked:", t.block_reason)
			  else
			    print("Failed:", t.status, t.error_type, t.error)
			  end

			  -- Cancel a task and everything it spawned
			  local t = dass.agent.execute_task{
			    task_title = "Delegation",
			    messages = { { role = "user", content = "Review the code and document it." } },
			    sub_agents = { "code-reviewer", "docs-writer" }
			  }
			  await task.delay(5000)
			  local wasRunning = t:cancel()          -- cascade = true by default
			  print("cancel fired:", wasRunning, "status:", t.status)

			  -- Cancel only the task itself, leaving its sub-tasks alone
			  t:cancel(false)

			  -- Detached work: a task that outlives the script and Stop
			  task.without_cancellation(function()
			    dass.agent.execute_task{
			      task_title = "Background job",
			      hidden = true,
			      messages = { { role = "user", content = "Do the long thing." } }
			    }
			  end)

			NOTES:
			  - The task runs under the script's cancellation token: Stop, a closed chat or the end of the tool call
			    that started the script cancels it. `task.without_cancellation` is the way out.
			  - A handle is the only door to a task: there is no lookup by id from Lua.
			  - Use t.last_content for the answer; there is no need for table.last over the response any more.
			  - CALLBACK TOOLS, SKILLS and SUB-AGENTS are resolved when the task launches, so an unknown name fails
			    before anything runs.
			  - Tool call results are embedded directly in the tool_call table (result_success, result_content,
			    result_attachments), not as separate messages.
			  - Assistant messages may include a "usage" table with token counts and timing: input_tokens,
			    output_tokens, cache_create_tokens, input_cache_hit_tokens, input_cache_miss_tokens,
			    time_to_first_token (ms), inference_time (ms), execution_time (ms). The same shape is available as
			    t.usage (whole task) and t.new_usage (last launch).
			""";

		private const string Call = "dass.agent.execute_task(properties)";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["execute_task"] = new LuaCallbackFunction(Execute);
			return null;
		}

		private LuaTuple Execute(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException($"{Call}: a property table is expected.");
			if (args.Length > 1)
				throw new LuaRuntimeException($"{Call}: batch mode was removed - launch each task with its own " +
					"call and await the returned handles.");
			if (args[0] is not LuaTable parameters)
				throw new LuaRuntimeException($"{Call}: the argument must be a table.");
			if (parameters.Length > 0 && parameters.IsArrayTable())
				throw new LuaRuntimeException($"{Call}: batch mode was removed - launch each task with its own " +
					"call and await the returned handles.");

			var messages = ReadMessages(parameters);
			var tools = ReadTools(ctx, parameters);
			var skills = ReadSkills(ctx, parameters);
			var subAgents = ReadSubAgents(parameters);
			var memoryBlocks = ReadMemoryBlocks(parameters);

			var timeout = ReadSeconds(parameters.Get("timeout"), TimeSpan.FromMinutes(30), "timeout");
			var expiration = ReadSeconds(parameters.Get("expiration"), TimeSpan.FromMinutes(5), "expiration");

			var taskTitle = parameters.Get("task_title") is LuaString taskTitleString ? taskTitleString.Value : null;
			var hiddenValue = parameters.Get("hidden");
			var hidden = hiddenValue is not LuaNil && hiddenValue.ToBoolean();

			var (autoApproveBehaviours, disallowedBehaviours) = ResolveToolPolicy();

			var task = agentTaskExecutor.Execute(new AgentTaskLaunchParameters
			{
				TaskName = taskTitle,
				Source = AgentTaskLaunchSource.Script,
				SourceId = "lua-api",
				Hidden = hidden,
				TriggeredChat = LuaExecutionContext.Tool?.Chat,
				TriggeredMessage = (LuaExecutionContext.Tool as ChatToolExecutionContext)?.Message,
				Model = ResolveModel(parameters),
				InitialMessages = [.. messages],
				Tools = [.. tools],
				Skills = [.. skills],
				SubAgents = [.. subAgents],
				MemoryBlocks = [.. memoryBlocks],
				AutoApproveBehaviours = autoApproveBehaviours,
				DisallowedBehaviours = disallowedBehaviours,
				TimeOut = timeout,
				CompletionExpiryTime = expiration
			}, ctx.CancellationToken);

			return new LuaTuple(LuaAgentTask.Wrap(task, timeout));
		}

		/// <summary>
		/// Reads the required message array and enforces the rule the model relies on: the last message is the user's.
		/// </summary>
		private static List<AgentChatMessage> ReadMessages(LuaTable parameters)
		{
			if (parameters.Get("messages") is not LuaTable messagesTable)
				throw new LuaRuntimeException($"{Call}: 'messages' must be a table.");

			List<AgentChatMessage> messages = [];
			for (int i = 1; i <= messagesTable.Length; i++)
			{
				if (messagesTable.Get(i) is not LuaTable messageTable)
					throw new LuaRuntimeException($"{Call}: each message must be a table.");

				messages.Add(LuaAgentMessageConverter.FromLua(messageTable));
			}

			if (messages.Count == 0)
				throw new LuaRuntimeException($"{Call}: 'messages' must contain at least one message.");

			if (messages[^1] is not AgentUserMessage)
				throw new LuaRuntimeException($"{Call}: the last message must be a user message.");

			return messages;
		}

		private LLModel ResolveModel(LuaTable parameters)
		{
			var modelName = (parameters.Get("model") as LuaString)?.Value;
			if (string.IsNullOrEmpty(modelName))
				modelName = chatSettings.Settings.Models.GetEffectiveSelection().AgenticToolsModel;
			if (string.IsNullOrEmpty(modelName))
				throw new LuaRuntimeException($"{Call}: the agentic model is not configured.");

			try
			{
				return modelManager.GetModel(modelName);
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"{Call}: model '{modelName}' is not available: {ex.Message}");
			}
		}

		private List<IToolDescriptor> ReadTools(LuaCallingContext ctx, LuaTable parameters)
		{
			List<IToolDescriptor> tools = [];
			if (parameters.Get("tools") is not LuaTable toolsTable)
				return tools;

			foreach (var toolValue in toolsTable.Values)
			{
				if (toolValue is LuaString toolName)
				{
					var tool = toolsetCollector.GetAvailableAddons().GetByName(toolName.Value)
						?? throw new LuaRuntimeException($"{Call}: tool '{toolName.Value}' is not available.");

					tools.Add(tool);
				}
				else if (toolValue is LuaTable toolTable)
				{
					var name = (toolTable.Get("name") as LuaString)?.Value;
					var displayName = (toolTable.Get("display_name") as LuaString)?.Value;
					var description = (toolTable.Get("description") as LuaString)?.Value;
					var callback = toolTable.Get("callback");

					if (string.IsNullOrEmpty(name))
						throw new LuaRuntimeException($"{Call}: a callback tool needs 'name'.");
					if (string.IsNullOrEmpty(description))
						throw new LuaRuntimeException($"{Call}: callback tool '{name}' needs 'description'.");
					if (callback is not LuaFunction callbackFunction)
						throw new LuaRuntimeException($"{Call}: callback tool '{name}' needs 'callback' as a function.");

					tools.Add(new LuaAdHocAgentTool(name, displayName ?? name, description,
						ReadToolSchema(toolTable, name), ctx, callbackFunction)
					{
						ApprovalLevel = ToolApprovalLevel.PolicyAutoApproveUnlessDisallowed
					});
				}
			}

			return tools;
		}

		/// <summary>
		/// Reads a callback tool's JSON Schema, treating an empty table as an empty object where a schema expects
		/// one: Lua cannot tell <c>{}</c> from an empty list, and <c>properties = {}</c> - the natural way to declare
		/// a tool without arguments - otherwise reaches the model as <c>[]</c>, which it rejects.
		/// </summary>
		private static JsonObject ReadToolSchema(LuaTable toolTable, string name)
		{
			if (toolTable.Get("parameters").ToConstNodeValue(tolerant: true).ToJsonNode() is not JsonObject schema)
				throw new LuaRuntimeException($"{Call}: callback tool '{name}' needs 'parameters' as a schema table.");

			NormalizeEmptySchemaMaps(schema);
			return schema;
		}

		/// <summary>
		/// Replaces the empty arrays a Lua conversion produces where JSON Schema requires an object.
		/// </summary>
		private static void NormalizeEmptySchemaMaps(JsonNode node)
		{
			switch (node)
			{
				case JsonObject schemaObject:
					foreach (var key in schemaObject.Select(pair => pair.Key).ToList())
					{
						var value = schemaObject[key];
						if (value is JsonArray { Count: 0 } && key is "properties" or "patternProperties" or "definitions" or "$defs" or "dependentSchemas")
							schemaObject[key] = new JsonObject();
						else if (value is not null)
							NormalizeEmptySchemaMaps(value);
					}
					break;

				case JsonArray schemaArray:
					foreach (var item in schemaArray)
						if (item is not null)
							NormalizeEmptySchemaMaps(item);
					break;
			}
		}

		private List<AgentSkill> ReadSkills(LuaCallingContext ctx, LuaTable parameters)
		{
			List<AgentSkill> skills = [];
			if (parameters.Get("skills") is not LuaTable skillsTable)
				return skills;

			var skillMap = skillsTable.Values.Any(v => v is LuaString)
				? skillsetCollector.GetAvailableAddons()
				: null;

			foreach (var skillValue in skillsTable.Values)
			{
				if (skillValue is LuaString skillName)
				{
					if (skillMap!.GetByName(skillName.Value) is not { } skill)
						throw new LuaRuntimeException($"{Call}: skill '{skillName.Value}' is not available.");

					skills.Add(new ChatAgentSkill(skill));
				}
				else if (skillValue is LuaTable skillTable)
				{
					var name = (skillTable.Get("name") as LuaString)?.Value;
					var description = (skillTable.Get("description") as LuaString)?.Value;
					var path = (skillTable.Get("path") as LuaString)?.Value;
					var homeDirectory = (skillTable.Get("home_directory") as LuaString)?.Value;
					if (path is not null && homeDirectory is null)
						homeDirectory = Path.GetDirectoryName(path);
					var body = skillTable.Get("body");

					if (string.IsNullOrEmpty(name))
						throw new LuaRuntimeException($"{Call}: an ad-hoc skill needs 'name'.");
					if (string.IsNullOrEmpty(description))
						throw new LuaRuntimeException($"{Call}: ad-hoc skill '{name}' needs 'description'.");

					switch (body)
					{
						case LuaString bodyString when !string.IsNullOrEmpty(bodyString.Value):
							skills.Add(new LuaAdHocAgentSkill(name, description, path, homeDirectory, bodyString.Value));
							break;

						case LuaString:
							throw new LuaRuntimeException($"{Call}: ad-hoc skill '{name}': 'body' cannot be empty.");

						case LuaFunction bodyFunction:
							skills.Add(new LuaAdHocAgentSkill(name, description, path, homeDirectory, ctx, bodyFunction));
							break;

						default:
							throw new LuaRuntimeException($"{Call}: ad-hoc skill '{name}' needs 'body' as a string or a function.");
					}
				}
			}

			return skills;
		}

		private List<TaskSubAgentDescriptor> ReadSubAgents(LuaTable parameters)
		{
			List<TaskSubAgentDescriptor> subAgents = [];
			if (parameters.Get("sub_agents") is not LuaTable subAgentsTable)
				return subAgents;

			var subAgentMap = subAgentsetCollector.GetAvailableAddons();

			foreach (var subAgentValue in subAgentsTable.Values)
			{
				if (subAgentValue is not LuaString subAgentName)
					throw new LuaRuntimeException($"{Call}: 'sub_agents' must be an array of sub-agent names.");

				if (subAgentMap.GetByName(subAgentName.Value) is not { } subAgent)
					throw new LuaRuntimeException($"{Call}: sub-agent '{subAgentName.Value}' is not available.");

				subAgents.Add(new TaskSubAgentDescriptor { Name = subAgent.Name, Description = subAgent.Description });
			}

			return subAgents;
		}

		private List<TaskMemoryBlock> ReadMemoryBlocks(LuaTable parameters)
		{
			List<TaskMemoryBlock> memoryBlocks = [];
			if (parameters.Get("memory_blocks") is not LuaTable memoryBlocksTable)
				return memoryBlocks;

			var chatContext = LuaExecutionContext.Tool as ChatToolExecutionContext;
			var senderAgent = chatContext?.Message is AssistantMessage assistantMessage
				? agentManager.TryGetAgentDescriptor(assistantMessage.SenderAgentId)
				: null;

			var available = chatContext != null && senderAgent != null
				? TaskMemoryBlock.ResolveBlocks(chatContext.Chat, senderAgent)
				: [];
			var availableMap = available.ToImmutableDictionary(block => block.Block.Name);

			foreach (var blockValue in memoryBlocksTable.Values)
			{
				var blockName = (blockValue as LuaString)?.Value;
				if (string.IsNullOrEmpty(blockName))
					throw new LuaRuntimeException($"{Call}: 'memory_blocks' must be an array of memory block names.");

				if (!availableMap.TryGetValue(blockName, out var block))
					throw new LuaRuntimeException($"{Call}: memory block '{blockName}' is not available.");

				memoryBlocks.Add(block);
			}

			return memoryBlocks;
		}

		/// <summary>
		/// Reads a duration in seconds: <c>false</c> means "never/not at all", a number is a duration and a missing
		/// value keeps the default.
		/// </summary>
		/// <exception cref="LuaRuntimeException">The value is neither a number nor <c>false</c>, or is negative.</exception>
		private static TimeSpan? ReadSeconds(LuaValue value, TimeSpan fallback, string name)
		{
			switch (value)
			{
				case LuaNil:
					return fallback;

				case LuaBoolean boolean:
					if (boolean.Value)
						throw new LuaRuntimeException($"{Call}: '{name}' is a number of seconds or false, not true.");
					return null;

				case LuaNumber number when number.Value >= 0 && number.Value <= TimeSpan.FromDays(24).TotalSeconds:
					return TimeSpan.FromSeconds(number.Value);

				case LuaNumber:
					throw new LuaRuntimeException($"{Call}: '{name}' must be a number of seconds from 0 to 2073600.");

				default:
					throw new LuaRuntimeException($"{Call}: '{name}' must be a number of seconds or false.");
			}
		}

		/// <summary>
		/// Reads the tool policy the running agent works under, so an agent-launched task is approved the same way
		/// the agent's own tool calls are.
		/// </summary>
		private (ToolBehaviour AutoApprove, ToolBehaviour Disallowed) ResolveToolPolicy()
		{
			var chatContext = LuaExecutionContext.Tool as ChatToolExecutionContext;
			var senderAgent = chatContext?.Message is AssistantMessage assistantMessage
				? agentManager.TryGetAgentDescriptor(assistantMessage.SenderAgentId)
				: null;

			var policy = senderAgent?.Tools.GetEffectivePolicy(chatSettings.Settings)
				?? chatSettings.Settings.InheritedAgentSettings.Tools.GetEffectivePolicy(chatSettings.Settings);

			return (policy.AutoApproveBehaviours, policy.DisallowedBehaviours);
		}
	}
}
