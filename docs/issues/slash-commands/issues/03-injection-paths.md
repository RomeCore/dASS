# Injection paths (skill body, sub-agent launch)

Status: resolved
Type: research
Blocked by:

## Question

Document the exact code paths for (a) loading and injecting a skill body into a prompt, and (b) launching a predefined sub-agent programmatically. These facts ground tickets 04 and 05.

## Answer

### (1) Skill body injection

- `SkillLoadTool` — `Agents/Tasks/SkillLoadTool.cs`: `Name => "skill-load"` (L13); schema = object, required string `name` (L16–31); holds `Skills : ImmutableDictionary<string, AgentSkill>` (L11).
- `ExecuteAsync` (L53–78): `Skills.TryGetValue(name)` (L57); missing → `Success=false` (L59–63); found → `body = await skill.GetBodyAsync(...)` (L66), returns `AgentToolCallResult { Success = true, Content = body }`. If `HomeDirectory` is non-empty, body is suffixed with a `---` note that all paths are relative to the skill home (L70–76).
- `AgentSkill` — `Agents/Tasks/AgentSkill.cs`: abstract `Name`/`Description`/`Path`/`HomeDirectory` (L8–23) + `GetBodyAsync` (L29).
- `ChatAgentSkill` — `Agents/Tasks/ChatAgentSkill.cs`: adapts `SkillInfo`; `GetBodyAsync => ChatSkillInfo.BodyGetter(ChatSkillInfo)` (L25).
- Body origin: `FrontmatterBasedAddonParser.cs:190` sets `result.BodyGetter = _ => document.Body` (the SKILL.md file body); `AddonBase.BodyGetter` at `Addons/AddonBase.cs:88`.
- Wiring in `AgentTaskExecutor.Execute`: adds `SkillLoadTool` when `parameters.Skills.Count > 0`, `ApprovalLevel = AlwaysApprove` (L94–98), and injects an `<available_skills>` system prompt listing only name/description/path (L100–121). Full `InjectionMode.Full` bodies are inlined instead by `SkillsStateProvider.cs:40`.
- Result → prompt: `ExecuteToolAsync` stores `result.Content` into `agentToolCall.Result` (L648–658) and returns a `ToolMessage` (L657) added to `nativeMessages` (L416), sent to `model.ChatStreamingAsync` (L280).

**Minimal sequence**: `parameters.Skills` → `SkillLoadTool` + `<available_skills>` → model calls `skill-load{name}` → `SkillLoadTool.ExecuteAsync` → `AgentSkill.GetBodyAsync` → `SkillInfo.BodyGetter` (file body) → tool-message content → next completion.

### (2) Sub-agent launch

- `AgentSubAgentTools` — `Agents/SubAgents/AgentSubAgentTools.cs`: registers tool `agent-callsub` when `_subAgents.Count > 0` (L44–50); delegate `CallSubAgentAsync(agentName, input, wait = true, ct)` (L59–66).
- `CallSubAgentAsync` (L68–94): resolves descriptor by name → `_paramsResolver.Resolve(_sourceParameters, descriptor, [new AgentUserMessage { Content = input }], out errors)` (L74–75) → `_agentTaskExecutor.Execute(parameters, ct)` (L80); `wait=true` awaits and returns `agentTask.LastGeneratedContent` (L84–86).
- Registered in `AgentTaskExecutor` (L169–177) when `parameters.SubAgents.Count > 0`; requires non-null `TriggeredChat` (L171–172).
- `ISubAgentTaskParamsResolver.Resolve(sourceParameters, descriptor, additionalMessages, out errors)` — `LLM/Services/Agents/ISubAgentTaskParamsResolver.cs:7`.
- `SubAgentTaskParamsResolver` (`LLM/Services/Agents/SubAgentTaskParamsResolver.cs`), normal path (L55–58 looks up `SubAgentInfo`), populates: `TaskName` (L125); `TriggeredChat` / `TriggeredMessage` copied from source (L126–127); `ModelName = info.Model ?? effective AgenticToolsModel` (L129); `Behaviour = Normal` (L130); `InitialMessages = [AgentSystemMessage{Content=info.Body}, ..additionalMessages]` (L131–133); policy/timeouts/maxParallel copied (L135–139); `FeedbackFunc = null` (L140); `Tools` via `SubAgentToolResolver` (L60, L142); `Skills` (L62–78, L143); `MemoryBlocks` (L80–101, L144); `SubAgents` (L103–121, L145). (`Model` is never set — only `ModelName`.)
- Tools applied: `SubAgentToolResolver.ResolveSubAgentTools` (`SubAgentToolResolver.cs:18–58`): `AvailableTools` → `PolicyBased` (L31), `AllowedTools` → `AlwaysApprove` (L38), `DisallowedTools` → `AlwaysDisallow` (L45), wrapping `ChatAgentTool`.
- Skills applied: matched by name via `skillsetBuilder.GetAvailableAddons()` and wrapped as `ChatAgentSkill` (L62–78); missing → error (L75).
- Attachment: `AgentSubAgentTools` attaches nothing itself; `AgentTaskExecutor.Execute` calls `_dispatcher.OnBeginTask(task)` (L218) → `AgentTaskDispatcher.OnBeginTask` appends to `_allTasks`, `task.Parent.SubTasks`, `TriggeredChat.AgentTasks`, `TriggeredMessage.AgentTasks` (L24–25). Since the resolver copies `TriggeredChat`/`TriggeredMessage`, the sub-agent task **is** attached to the same chat's and message's `AgentTasks`; removed on end (L39–40).

**Minimal sequence**: parent's `SubAgents` includes descriptor → `agent-callsub{agentName, input}` → `CallSubAgentAsync` → `Resolve(...)` → `InitialMessages = [SystemMessage(info.Body), UserMessage(input)]` → `_agentTaskExecutor.Execute(parameters, ct)` → dispatcher attaches task to chat/message `AgentTasks` → (wait) returns `LastGeneratedContent`.

## Comments

<!-- appended conversation -->
