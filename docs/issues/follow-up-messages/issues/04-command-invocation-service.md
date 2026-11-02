# 04: Command invocation service

Status: open
Type: task
Blocked by: 03

## What to build

The command machinery moves out of the insertion service into its own chat-scoped service, so "resolve, bind,
execute, report" is one boundary and "put a message into the history" is another.

See [`spec.md`](../spec.md) section *Commands and permissions*.

## Contracts

```csharp
public interface IChatCommandInvocationService
{
    bool IsCommand(string content);                                     // token extraction only
    CommandResolution Resolve(string rawToken, string rawArguments);    // resolve + parse + bind
    Task<CommandExecution> ExecuteAsync(CommandResolution resolution, string rawToken,
        ChatMessage message, bool generateIntent, CancellationToken ct = default);
}
```

`CommandResolution` and `CommandExecution` move with the code (they are `private` records today).

## Rules

- `ExecuteAsync` takes the `ChatExecutionLevel.Command` scope around the executor call and nothing else; the results,
  the status and the error are returned, not logged.
- Cancellation stays "not a failure": a cancelled command returns a `Cancelled` status and no error, so the message
  keeps no error text.
- The service never appends a message and never touches the queue.

## Acceptance

- `ChatMessageInsertionService` no longer contains `Resolve`, `ExecuteCommandAsync` or the empty-schema constant.
- Command behaviour is identical to today for a direct send: an unknown command blocks the send through the
  pre-flight; a command failing at execution attaches its error and suppresses generation.
- Taking the `Command` scope still cancels narrower levels only.

## Not in scope

New command sources, the argument grammar.
