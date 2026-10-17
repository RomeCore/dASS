# P3.5b — Remove `CompletionToken`

Status: open
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

## Acceptance

- `CompletionToken` no longer appears in `LLMDesktopAssistant.LLM.Domain`, the view models, the execution
  service, the synchronizers, `ChatAgentTool` or the tests.
- A streaming message still flips the view model to completed; a tool call still releases its
  `PropertyChanged` subscription exactly once.
- Core, Desktop and both test projects build and pass.
