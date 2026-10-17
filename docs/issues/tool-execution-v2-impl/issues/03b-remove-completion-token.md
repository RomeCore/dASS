# P3.5b — Remove `CompletionToken`

Status: resolved
Type: task
Blocked by: 03

## Scope

`CompletionToken` (and `GetAwaiter`) is deleted from `LLMDesktopAssistant.LLM.Domain.ToolCall` and
`LLMDesktopAssistant.LLM.Domain.AssistantMessage`. Both types already carry the truth in their status:
`ToolStatus` and `AssistantMessageStatus` (with `IsTerminal()`), and both statuses are persisted.

The type itself stays in `RCLargeLanguageModels` for `PartialFunctionToolCall` and other library types;
only our domain objects stop using it.

## Replacements

- `AssistantMessage.IsCompleted` → `Status.IsTerminal()`.
- `ToolCall.IsCompleted` → a terminal `ToolStatus` (`Success`, `Error`, `Cancelled`); add
  `ToolStatusExtensions.IsTerminal()`.
- `AssistantMessageViewModel` derives `IsCompleted` from the status at construction and subscribes to
  `Status` changes for a live message.
- `ToolCallViewModel` / `ToolCallFlyoutViewModel` unsubscribe `PropertyChanged` when the status becomes
  terminal instead of on `CompletionToken.OnCompleted`.
- `ChatExecutionService` stops creating a `CompletionSource` per message and per tool call; the wrapper
  around the tool execution no longer completes a token.
- `MessageDatabaseSynchronizer` / `ToolCallDatabaseSynchronizer` keep setting a terminal status on load.
- `ChatAgentTool` sets `Status` instead of a token.

## Answer

### What landed

`CompletionToken` and `GetAwaiter` are gone from `LLMDesktopAssistant.LLM.Domain.ToolCall` and
`LLMDesktopAssistant.LLM.Domain.AssistantMessage`. `IsCompleted` stays as a thin projection over the
terminal status: `AssistantMessageStatus.IsTerminal()` (already existed) and the new
`ToolStatusExtensions.IsTerminal()`, which also treats `ExecutionInterrupted` as terminal so a call
loaded after a crash keeps its completed UI state.

- The message, text-part and reasoning-part view models react to the `Status` property change instead of
  a token callback; the tool call and flyout view models unsubscribe `PropertyChanged` when the status
  becomes terminal.
- `ChatExecutionService` no longer creates `CompletionSource`s and no longer wraps tool execution in a
  completion token; the tool call's status is the completion state.
- Both database synchronizers and `ChatAgentTool` stop setting the token; the persisted status is the
  source of truth.
- The Blazor `AssistantMessageComponent` dropped its `OnCompleted` re-render: it already re-renders on
  `PropertyChanged`, and completion is now a `Status` change.
- `CompletionSource`/`CompletionToken` no longer appear anywhere in the repository's code or tests; the
  type stays in `RCLargeLanguageModels` for its own streaming types.

### Tests

The token initializers were removed from the fixtures; completed fixtures now set
`AssistantMessageStatus.Success` explicitly. No test awaits a message or a tool call.

### Gates

- Core project: builds, 0 errors, 0 warnings.
- Desktop project: builds, 0 errors (only the pre-existing NuGet advisory warnings).
- `LLMDesktopAssistant.Tests`: 1209 total, 1208 passed, 1 skipped (the YamlDotNet colon case), 0 failed.
- `LLMDesktopAssistant.Desktop.Tests`: 44 total, 44 passed, 0 failed.

One `ChatStorageServiceTests` run hit a known LiteDB unique-index flake; a clean rerun passed. It is
unrelated to this change.
