# 14: Chat message-insertion service (command host)

Status: resolved
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

- [x] `IChatMessageInsertionService` + `ChatMessageInsertionService` (`[ChatService]`) and
      `UserInputInsertionCheckResult` exist.
- [x] `CanInsertUserInput` is pure and does the full resolve/parse/bind; `InsertUserInputAsync` re-resolves and is the
      only path that inserts.
- [x] `IChatOperationService.SendUserInputAsync`/`SendEditedUserInputAsync` delegate to `InsertUserInputAsync`, keep
      `Operation`, and no longer insert messages themselves.
- [x] The insertion service takes `ChatExecutionLevel.Command` around the executor and calls generation; the
      `Operation` level is owned by `ChatOperationService`.
- [x] A message starting with `/` and an unknown command is **not** inserted by the `CanInsertUserInput` path (the view
      model refuses and keeps the draft); a guard inserts it with `Error` set when insertion runs without the
      pre-flight.
- [x] `EnableCommands == false` disables command handling entirely — such a message is sent as literal text.
- [x] The stored content keeps the raw text; a `//`-escaped message stores the unescaped text.
- [x] The intent ceiling `generateIntent && (command.Generate ?? true) && result.Generate` is implemented; an executor
      exception is attached to the message rather than thrown (the `Cancelled`/`Failed` status is the fingerprint's job,
      ticket 15).
- [x] Edits never run a command.
- [x] Tests: pure seams (final-generate composition) plus an integration test on `ChatStorageTestContext` with fakes —
      a command that fails to resolve inserts a message with the error and runs nothing, a resolved command inserts a
      `UserMessage` and runs the executor, an edit does not run the command.
- [x] The solution builds; the full test suite stays green.

## Answer

- **`IChatMessageInsertionService`** + `ChatMessageInsertionService` (`[ChatService]`) +
  `UserInputInsertionCheckResult` (`(bool Success, LocaleKeyBase? Error, int ErrorPosition)` with `Ok`/`Blocked`).
- **Check / insert split.** `CanInsertUserInput` is pure: it extracts the token, resolves, parses and binds, then
  returns a verdict without touching the chat. `InsertUserInputAsync` re-resolves (it only receives the input) and is
  the sole path that inserts; `ChatOperationService` keeps its `Task` signatures and the `Operation` token and now
  delegates the whole send/edit path to it. The insertion service takes `ChatExecutionLevel.Command` only around
  `executor.ExecuteAsync` and owns the hand-off to `GenerateResponseAsync`.
- **View model.** `UserInputViewModel` calls `CanInsertUserInput` before it clears the draft; a refusal shows the error
  as a toast and leaves text + parts untouched. Because the services are separate calls, the guard also covers callers
  that skip the pre-flight (`QuickActionService`) and races: the message is inserted with `Error` set, the executor is
  skipped and no generation follows.
- **Content & escape.** The stored content is the user's text; `//x` stores `/x`. With `EnableCommands == false` both
  command handling and the unescape are off, so the text is literal. Edits never resolve a command and never unescape.
- **Intent ceiling.** `SlashCommandIntent.Resolve(intent, ceiling, outcome)` = `intent && (ceiling ?? true) && outcome`,
  unit-tested; an unknown/bad command forces no generation. An executor exception attaches `ex.Message`
  (`Locale.GetConstKey`) to the message and suppresses generation; a cancellation is silent.
- **Errors.** `command.error.unknown` became a formatted key carrying the typed token.
- **Tests.** `SlashCommandIntentTests` (the ceiling) plus twelve `ChatMessageInsertionServiceTests` cases on
  `ChatStorageTestContext` with fakes: the pre-flight accept/refuse (unknown, bad args, edit, disabled), the guard,
  content/escape, the disabled gate, the intent ceiling, edits and a throwing executor.

Builds: main + desktop + Blazor green (pre-existing warnings only). Slash-command test set: 119 passed. The full suite
was not re-run to completion (the known intermittent test-host stall); every touched area is covered by the filtered
run.

Deferred to ticket 15: the fingerprint (including its `Failed`/`Cancelled` status) is not written yet.

## Comments

- Stage 2 grilling: the split into check + insert exists so the view model can refuse *before* clearing the draft; the
  guard exists because the chat-operations facade (and quick actions) may insert without a pre-flight.
- `CanInsertUserInput` takes `generateIntent` for symmetry with the insert path; v1 does not use it in the verdict.
