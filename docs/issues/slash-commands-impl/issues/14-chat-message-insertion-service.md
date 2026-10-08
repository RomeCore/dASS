# 14: Chat message-insertion service (command host)

Status: ready-for-agent
Type: task
Blocked by: 12, 13

## What to build

The host that turns "the user pressed send" into the resolve → validate → insert → execute → generate pipeline, and the
first user-visible command behaviour: an unknown command (or a bad argument list) is refused, while a resolved command
gets its message into the history and (optionally) hands off to generation. v1 executors are still the stub, so the
observable result is: the message is inserted, the executor runs, and generation happens per the intent ceiling.

### Service

New chat-scoped `IChatMessageInsertionService` with two entry points — a cheap **pre-flight check** the view model calls
before it commits anything, and the **authoritative insert** the chat-operations facade calls:

```csharp
public readonly record struct UserInputInsertionCheckResult(bool Success, LocaleKeyBase? Error, int ErrorPosition);

public interface IChatMessageInsertionService
{
    // Pure, no side effects: resolve → parse → bind. Called by UserInputViewModel directly.
    UserInputInsertionCheckResult CanInsertUserInput(UserInput input, bool generateIntent, int? editIndex = null);

    // Authoritative: re-resolves, inserts the message, runs the command, hands off to generation.
    Task InsertUserInputAsync(UserInput input, bool generateIntent, int? editIndex = null, CancellationToken ct = default);
}
```

`IChatOperationService.SendUserInputAsync` / `SendEditedUserInputAsync` keep their `Task` signature and their
`Operation` execution token, and become thin delegates into `InsertUserInputAsync` (edit path → `editIndex != null`).
The insertion service owns the `Command` execution level (taken around the executor call only) and calls
`IChatExecutionService.GenerateResponseAsync` itself; the `Operation` level stays with `ChatOperationService`. The
`UserMessage` factory moves here from `ChatOperationService`.

### Flow

```
if editing (editIndex != null):
    storage.EditMessage(editIndex, CreateUserMessage(input))     // no command handling, no fingerprint
    if generateIntent: GenerateResponseAsync
    return

if SlashCommandExtractor.TryExtractToken(input.Content, out token, out rawArguments):
    if commands disabled (chat settings):  → treat as literal text (no command handling at all)
    resolution = ISlashCommandResolver.Resolve(token)
    if resolution.Unknown → guard (below)
    schema = command.ArgumentSchema ?? empty schema      // null means "takes no arguments"
    parsed = SlashCommandArgumentParser.TryParse(schema, rawArguments)
    bound  = SlashCommandArgumentBinder.Bind(schema, parsed)
    if parse/bind failed → guard (below)
insert UserMessage (content = the user's text; if the message was "//…"-escaped, the unescaped text)
if a command resolved:
    result = command.Executor.ExecuteAsync(ctx, commandToken)     // under ChatExecutionLevel.Command
    finalGenerate = generateIntent && (command.Generate ?? true) && result.Generate
else:
    finalGenerate = generateIntent
if finalGenerate: GenerateResponseAsync(operationToken)
```

- **Content**: exactly what the user typed, with the leading token kept; if `SlashCommandExtractor.UnescapeLeadingSlash`
  changes the text (`//foo` → `/foo`), the unescaped text is stored.
- **Tokens are slash-free**: `SlashCommandExecutionContext.RawToken` = the as-typed slash-free token, `Token` =
  `command.CanonicalToken`.
- **Guard**: when insertion runs without a passing pre-flight (the chat-operations facade can be called straight, and
  `QuickActionService` does), a resolve/parse/bind failure does **not** throw and does **not** drop the input: the
  message is inserted with its raw content and `Error` set (the same `LocaleKeyBase` the pre-flight produced), the
  executor is skipped, and no generation follows. A fingerprint with `Status = Failed` is still written (ticket 15).
- **Executor failures**: an `OperationCanceledException` becomes status `Cancelled` (no message error); any other
  exception becomes status `Failed` and is attached to the message `Error` — it is never rethrown, because the message
  is already in the history.
- **Errors with a position** carry `ErrorPosition` (the binder's `ErrorPosition`, or `-1` when there is none).
- Commands never run on the edit path.

### View model

`UserInputViewModel` calls `CanInsertUserInput` itself (resolving the service from the chat scope) before sending; on
failure it keeps the draft (text + parts) and surfaces the error (a toast), and does not call the chat-operations
facade. On success the behaviour is unchanged. `InsertUserInputAsync` is not called from the view model.

### Localization

Command errors are formatted keys so the offending token is named — `command.error.unknown` gains a `{0}` argument (the
token); the argument-error keys already exist. Add / extend keys in `iv` and `ru-RU` as needed.

## Acceptance criteria

- [ ] `IChatMessageInsertionService` + `ChatMessageInsertionService` (`[ChatService]`) and
      `UserInputInsertionCheckResult` exist.
- [ ] `CanInsertUserInput` is pure and does the full resolve/parse/bind; `InsertUserInputAsync` re-resolves and is the
      only path that inserts.
- [ ] `IChatOperationService.SendUserInputAsync`/`SendEditedUserInputAsync` delegate to `InsertUserInputAsync`, keep
      `Operation`, and no longer insert messages themselves.
- [ ] The insertion service takes `ChatExecutionLevel.Command` around the executor and calls generation; the
      `Operation` level is owned by `ChatOperationService`.
- [ ] A message starting with `/` and an unknown command is **not** inserted by the `CanInsertUserInput` path (the view
      model refuses and keeps the draft); a guard inserts it with `Error` set when insertion runs without the
      pre-flight.
- [ ] `EnableCommands == false` disables command handling entirely — such a message is sent as literal text.
- [ ] The stored content keeps the raw text; a `//`-escaped message stores the unescaped text.
- [ ] The intent ceiling `generateIntent && (command.Generate ?? true) && result.Generate` is implemented; an executor
      exception maps to `Cancelled`/`Failed` and is attached to the message rather than thrown.
- [ ] Edits never run a command.
- [ ] Tests: pure seams (content/escape, final-generate composition, resolve failure → check result) plus an
      integration test on `ChatStorageTestContext` with fakes — a blocked send inserts nothing, a resolved command
      inserts a `UserMessage` and runs the executor, an edit does not run the command.
- [ ] The solution builds; the full test suite stays green.

## Answer

<!-- appended on resolution -->

## Comments

- Stage 2 grilling: the split into check + insert exists so the view model can refuse *before* clearing the draft; the
  guard exists because the chat-operations facade (and quick actions) may insert without a pre-flight.
- `CanInsertUserInput` takes `generateIntent` for symmetry with the insert path; v1 does not use it in the verdict.
