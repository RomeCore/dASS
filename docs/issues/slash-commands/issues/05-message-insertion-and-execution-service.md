# Message-insertion and execution service

Status: resolved
Type: grilling
Blocked by: 01

## Question

Design the intermediate **chat-scoped service** between the UI and `ChatExecutionService` that hosts command dispatch.

Cover:

- how a user message carrying a command is **inserted** into the chat;
- **when** the command executes (after insertion) and who owns the produced message;
- where `GenerationCts` is set and how it cancels a running command;
- how the `bool Generate` **intent** (from the send button) is threaded into the command and the resolved **outcome** comes back;
- failure / rollback semantics (the message is already appended);
- how the service is reached from `UserInputViewModel.SendCurrentUserInputAsync` and from the Lua API;
- how it hands off to `ChatExecutionService` when the outcome allows generation.

## Answer

### Service shape

- A new chat-scoped **`IChatMessageInsertionService`** hosts the *insert → command → generate* flow.
- `ChatOperationService` stays the chat-operations facade (edit / regenerate / branch / delete); for the send path it **delegates** to the new service and no longer owns the send CTS plumbing itself.
- Entry points: `UserInputViewModel.SendCurrentUserInputAsync` → the new service; Lua (`dass.commands.invoke`, ticket 10) → the same service.

### Execution token (shared infrastructure)

`IChatExecutionTokenService` replaces the ad-hoc manual CTS plumbing in `ChatOperationService` and `ChatExecutionService`:

```csharp
public enum ChatExecutionLevel
{
    None = 0, Operation, Command, AgentSequence, Agent, Message
}

public interface IChatExecutionTokenService
{
    CancellationTokenSource ExecutionCancellationToken { get; }   // "all levels", used by the UI
    event Action? ExecutionCancellationTokenChanged;

    IDisposable WithToken(ChatExecutionLevel level, out CancellationToken cancellationToken);
    bool TryCancel(ChatExecutionLevel level);   // cancels that level and everything deeper
}
```

- No active token = level `None` (0).
- Taking level `L` cancels/replaces the token at `L` and **recursively cancels all deeper** levels (`> L`).
- `Dispose` (end of `using`) releases `L` and cancels all deeper levels — a descendant never outlives its parent.
- `CancellationTokenSource.Cancel()` cancels **all** levels at once.
- A deeper level never cancels a shallower one.
- `Operation` (top level) is taken for **any** chat mutation (send / edit / delete / branch) — "any chat change interrupts any generation".
- `ExecutionCancellationToken` is the **CTS itself** representing the whole execution ("all levels"); the UI uses it as its cancel handle. The event fires when it changes.
- The command runs under the **`Command`** level.

### Flow and intent ceiling

```
if the leading token is a command:
    resolve (ISlashCommandResolver)      → unknown ⇒ block, nothing inserted
    parse + validate args                → failure ⇒ block, nothing inserted
build UserMessage (raw Content) → storage.AppendMessage
if a command was resolved:
    executor.ExecuteAsync(ctx)           → mutates the message; returns bool outcome
finalGenerate = GenerateIntent && outcome        // a command may lower the intent, never raise it
if finalGenerate: ChatExecutionService.GenerateResponseAsync(ct)
```

- A command's **own work** (sub-agent run, skill injection, …) is not "generation" and is not gated by the button.
- **Runtime** errors are attached to the message (via `ChatMessage.Error`), not thrown away — the message is already in history.
- Commands run **only** on new-message insertion; `editIndex` edits do **not** re-run commands (v1).

### Validation at send

- A command that does not exist, or whose arguments fail validation, **blocks the send** — nothing is inserted.
- Validation uses `ISlashCommandArgumentFormatProvider.TryValidate` (errors are `LocaleKeyBase`).
- A **parse** failure (an unterminated quote, say) blocks the same way and reports *where* it happened, so blocking the send is never an unexplained refusal.

### Error model

- `Error : string?` **moves from `AssistantMessage` up to `ChatMessage`**, so any message (including the command message) can carry an error.

### Signature

```csharp
Task<UserInputInsertionResult> TryInsertUserInputAsync(
    UserInput input, bool generateIntent, int? editIndex = null, CancellationToken ct = default);

public readonly record struct UserInputInsertionResult(
    bool Success, LocaleKeyBase? Error, int ErrorPosition, ChatMessage? Message);
```

- `ErrorPosition` is the offset into the argument text where a **syntax** error sits (carried over from `SlashCommandBoundArguments.ErrorPosition`), and `-1` when the failure has no position — an unknown command, a missing or an invalid argument. It travels with the error so the refusal can point at the offending character instead of merely naming the problem.
- The command **fingerprint** is **not** a separate field: it lives on the message (`Message.AdditionalData` — ticket 06), so this contract does not depend on ticket 06's concrete type.

## Comments

- Two rounds. Round 1: service shape (a), token ownership, flow, edits, entry points.
- Round 2: `ChatExecutionLevel` enum + `WithToken(level, out ct)`; `TryCancel(level)` added; `ExecutionCancellationToken` is the CTS itself; validation blocks the send; `ChatMessage.Error` move.
- `UserInputInsertionResult` returns the inserted `ChatMessage` only (fingerprint read from its `AdditionalData`) — chose option (A).
- **Amended:** `UserInputInsertionResult` gains `ErrorPosition` — the offset of a syntax error in the argument text (`-1` when the failure has no position), carried over from `SlashCommandBoundArguments.ErrorPosition` (ticket 07), so the UI can point at the offending character.
