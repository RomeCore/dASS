# Skill- and agent-command semantics

Status: resolved
Type: grilling
Blocked by: 01, 03

## Question

What exactly do `/skill:<name> [args]` and `/agent:<name> [args]` do?

## Answer

### Skill command — `/skill:<name> args…`

- The skill body is injected into the target message as a new **`AdditionalMessageContentPart`** — a reusable layer added for this: a part that (a) contributes text to the model-facing message content and (b) carries a UI chip.
- **`AdditionalMessageContentPart : AdditionalMessagePart`**; its `Content` is **appended to `message.Content` inside `ChatMessageQuoteRenderer`** (the mechanic already anticipated by `MessagePartsFacet.Content`). **No context expander** is involved.
- `AdditionalMessagePart` gains **`bool IsRestorable { get; set; } = true`**; command parts set it to **`false`**, so editing the message does **not** clone them into `UserInputState` (the edit path must honour it).
- The UI shows a chip/badge, e.g. *"Used skill 'grilling'"*.
- The command's positional arguments are **substituted into the body** (`$ARGUMENTS` / named placeholders).
- The body is always injected **in full** — `InjectionMode` is **not** applied to commands. If the skill has a `HomeDirectory`, the home-directory note (as in `SkillLoadTool`) is appended.
- Default `Generate = null` — the skill command **passes the user intent through**.

### Agent command — `/agent:<name> input`

- Input = `RawPositionalArguments` → `AgentUserMessage.Content`.
- Launch mirrors `agent-callsub` exactly: build a **fresh** `AgentTaskLaunchParameters`

```csharp
new AgentTaskLaunchParameters
{
    TaskName = subAgent.Name,
    TriggeredChat = chat,
    TriggeredMessage = commandMessage,
    InitialMessages = [],
    AutoApproveBehaviours = …,
    DisallowedBehaviours = …
}
```

then `ISubAgentTaskParamsResolver.Resolve(params, new TaskSubAgentDescriptor { Name, Description }, [new AgentUserMessage { Content = input }], out errors)` → `IAgentTaskExecutor.Execute`.
- `TriggeredMessage` = the command message, so `AgentTaskDispatcher` attaches the sub-agent task to `message.AgentTasks`.
- `AutoApproveBehaviours` / `DisallowedBehaviours` come from the **chat-level `ChatSubAgentSettings`** (new sub-agent tool-policy flags) — see ticket 13; `agent-callsub` will be reworked to use the same source instead of the calling chat agent's settings.
- **Fire-and-forget** by default; a **named argument `wait`** (default `false`) lets the user await the result — i.e. the agent command's argument schema is `Keyed = { wait: bool }` + `HasRestPositional` (ticket 12).
- Default `Generate = null` — the sub-agent runs regardless; the intent is passed through.

### Both

- v1: the agent sees the raw message content (`/skill:… args`, `/agent:… input`) plus the injected content part.

## Comments

- Two rounds. Round 1 corrections: the injected part is an `AdditionalMessagePart` chip (badge), not a context expander; the injection mechanism is `AdditionalMessageContentPart` appended to `Content` in `ChatMessageQuoteRenderer`; `AdditionalMessagePart.IsRestorable` added; skill args use substitution (b → a); body always full + home-dir note; agent launch reuses the `agent-callsub` shape (the params are **not** parent-derived); `wait` named argument (default `false`).
- Spin-off: the sub-agent tool policies move to chat level → ticket 13.
