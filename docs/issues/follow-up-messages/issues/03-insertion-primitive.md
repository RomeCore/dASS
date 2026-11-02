# 03: The insertion primitive becomes queue-blind, in two steps

Status: resolved
Type: task
Blocked by:

## What to build

`IChatMessageInsertionService` narrowed to what its name says: put a message into the history, and (separately) run
that message's command. It stops generating, stops knowing about the executor, and reports the intent it produced.

Two steps, because they are two different things: appending is a synchronous write that never blocks, a command can
run for minutes. Everything that needs to act "between the message and its command" - the queue drain - depends on
that split.

See [`spec.md`](../spec.md) sections *Ownership*, *The turn*, *Commands and permissions*.

## Contracts

```csharp
/// <summary>
/// The message as it was appended, plus whatever the command path needs to run later. Opaque to callers.
/// </summary>
public sealed record UserInputInsertion(ChatMessage Message, /* captured command data */);

public readonly record struct UserInputInsertionResult(bool Generate);

public interface IChatMessageInsertionService
{
    // Pure: resolve, parse, bind. Unchanged behaviour, unused parameters dropped.
    UserInputInsertionCheckResult CanInsertUserInput(UserInput input);

    // Append (or replace at editIndex). No command, no generation.
    Task<UserInputInsertion> AppendUserInputAsync(UserInput input, int? editIndex = null, CancellationToken ct = default);

    // Run the command of an already-appended message; report the intent. No generation.
    Task<UserInputInsertionResult> RunInsertedCommandAsync(UserInputInsertion insertion, bool generateIntent,
        CancellationToken ct = default);
}
```

## Rules

- The `IChatExecutionService` dependency is deleted; the final `if (generate) await executor.GenerateResponseAsync(ct)`
  goes away with it. This is what removes the DI cycle of trace finding F3.
- `CanExecuteCommand` is read at exactly one place, the message factory inside `AppendUserInputAsync`:
  `isCommand = input.CanExecuteCommand && CommandsEnabled && SlashCommandExtractor.TryExtractToken(...)`.
  With `false` the content is stored verbatim - no token extraction, no `//` unescaping, no execution, and
  `RunInsertedCommandAsync` is a no-op.
- `AppendUserInputAsync` is fully synchronous apart from its `Task` return: the message is in the history when it
  returns, and nothing else has happened yet.
- `RunInsertedCommandAsync` returns the intent `generateIntent` corrected by the command outcome; the caller owns the
  decision to generate.
- The invalid-command guard stays: the message is already appended, the error and the failed fingerprint are attached,
  and the intent becomes `false`.
- `CanInsertUserInput`'s unused `generateIntent` and `editIndex` parameters are dropped; its single caller in
  `UserInputViewModel` is updated.

## Acceptance

- `IChatMessageInsertionService` has no reference to `IChatExecutionService` (no constructor parameter, no call).
- After `AppendUserInputAsync` returns, the message is in `chat.Messages` and no tool, command or model call has run.
- `RunInsertedCommandAsync` on a message appended with `CanExecuteCommand = false` performs nothing and returns the
  incoming intent unchanged.
- Sending `//literal` with `CanExecuteCommand = true` stores `literal`; with `false` stores `//literal`.
- A message with `CanExecuteCommand = false` and a valid command token produces no fingerprint and no executor call.
- No call to `GenerateResponseAsync` remains in the insertion service.

## Not in scope

Who generates instead (ticket 05), command extraction (ticket 04).

## Answer

The primitive is split exactly as written: `AppendUserInputAsync` appends (or replaces at the edit index) and resolves the command without running it; `RunInsertedCommandAsync` runs it and reports the intent. The `IChatExecutionService` dependency is gone with the generation call, which removed the DI cycle the design relied on. `CanInsertUserInput` dropped its two unused parameters. `CanExecuteCommand` is read once, in the message factory, and a message without the permission is stored verbatim.
