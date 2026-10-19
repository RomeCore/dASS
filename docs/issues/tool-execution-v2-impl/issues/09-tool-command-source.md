# P9 — Tool-command source

Status: resolved
Type: task
Blocked by: 04

## Scope

A derived slash command per tool (spec §15, ticket 15 of the spec effort).

## Mechanics

- `DerivedSlashCommandProvider<ToolInfo>` sits next to the skill/sub-agent providers and owns
  `/tool:<name>` in the **derived** tier.
- The command's argument schema is generated from the tool's `ArgumentSchema`:
  - the designated main argument maps to the positional slot / rest that the slash-command argument
    grammar already supports;
  - every other property becomes a keyed argument;
  - required/optional and defaults are preserved.
- The executor runs the tool through the `ToolCommandOrchestrator` with `Source = Command`:
  streaming (Once) → validation → sync{ preview → execution } → settle. No policy, no confirmation; the
  hard floor (`AlwaysDisallow`) is honoured.
- The command produces a user message like the other derived commands (fingerprint, model-facing mode).
- The tool must exist in the chat's toolset; a disabled/absent tool yields no command.

## Acceptance

- `/tool:<name>` completes in the input and shows the argument hint.
- Running it executes the tool and appends the result to the chat.
- `AlwaysDisallow` tools are refused with a clear message.
- Unit test: schema mapping for a tool with a main argument, keyed optional arguments and defaults.

## Answer

The source landed as `ToolSlashCommandProvider` + `ToolCommandExecutor` + `ToolArgumentSchemaMapper` +
`JsonArgumentFormatProvider`, registered through the ordinary `[ChatService(typeof(ISlashCommandProvider))]`
path. Nothing in the completion, highlighting or resolution layers needed a change: the derived provider
fills the command, the executor carries the schema, the existing input stack does the rest.

What actually happens on `/tool:fs-read path=README.md`:

1. `ToolArgumentSchemaMapper` turned the tool's JSON Schema into a keyed-only
   `SlashCommandArgumentSchema`; the binder validates and converts the arguments before the send is
   allowed.
2. `ToolCommandExecutor` assembles the `JsonObject` from the bound values, creates a chat `ToolCall`
   (`ToolCallId = dass-<guid>`, `Title = tool.NameKey`, `CanBeCompacted` from the tool) and attaches it to
   the command's own message — `ChatMessage.ToolCalls`, the "even the user can call tools" channel that
   `UserMessageView` already renders.
3. The call goes through `IToolInvocationDispatcher` with `Source = Command` and
   `ConstantConfirmationSink.Approve`, so `ToolCommandOrchestrator` runs the whole pipeline without the
   policy and confirmation stages.
4. The result stays on the card: status, `StatusContent`, attachments (the file tools' read-only diff
   card included), progress.

### Divergences from the ticket as written

1. **No positional slot, no rest.** Every property is a keyed argument (`/tool:fs-read path=…`,
   `limit=50`). The "designated main argument" is gone: the user decided that guessing a main argument
   from a JSON Schema is worth nothing, and property order means nothing.
2. **The hard floor is not checked.** The ticket's "`AlwaysDisallow` tools are refused" contradicted spec
   §13 and plan deviation 17: a command is the user's own action, and `AlwaysDisallow` is a statement
   about what an *agent* may do. `ToolCommandOrchestrator` calls no policy stage at all, and no extra
   check was added.
3. **"The chat's toolset" means the chat's available tools, not an agent's toolset.** The provider reads
   `IAddonSetCollector<ToolInfo>.GetAvailableAddons()` — native modules, the chat's additional tools and
   MCP. Per-agent `ApprovalLevel`, `PolicyMask`, `Specifiers` and the agent-stamped `ContextFactory`
   belong to an agent decision and never apply here; the executor supplies `ChatToolContextFactory` and
   the approving sink itself. A tool no agent has enabled is still runnable by hand.
4. **`generate: false`, `ModelFacingMode.Raw`.** A chain of `/tool:` calls is the user assembling a turn
   by hand; the next message they write is what starts generation. The raw token stays model-facing, and
   the call's arguments/result travel to the model through the message quote's tool-call facts.
5. **Array and object arguments are JSON literals** (`paths=["a","b"]`, `options={"depth":2}`) through
   `JsonArgumentFormatProvider`; `enum` maps onto the existing strict `ChoiceArgumentFormatProvider`;
   `string`/unknown/`anyOf`/`oneOf`/`$ref` degrade to a plain unvalidated string, and a nested object's
   inner `required` is left to the tool's own binding.
6. **A failed tool is a successful command.** The settlement stage writes the error into the call and
   marks it `Error`; the executor still returns `Ok`, because an exhausted tool is a result, not a
   command failure, and the card already shows it in red.
