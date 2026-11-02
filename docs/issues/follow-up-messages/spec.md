# Follow-up messages - spec

Status: implemented and verified live on 2026-11-02. Every ticket in [`issues/`](issues/) is closed with an answer
that records what actually landed, including the deliberate deviations from this text.

This spec superseded the design notes in [`docs/ideas/chat-execution/follow-up-messages.md`](../../ideas/chat-execution/follow-up-messages.md);
that file is now a short record pointing back here.

## Problem

Sending a message while a generation runs is impossible: the input is disabled, the send command reports
`CanExecute == false`, and `UserInputView.Send` turns Enter into a cancellation. A user who wants to say two
things about one answer must either wait or stop the run and lose the answer's context.

## Shape

A pending-message queue, filled while the chat is busy, drained at an injection barrier. A follow-up is not a
lesser message: when it is injected it goes through the ordinary send path, interrupts the whole agent sequence,
and generation restarts from the agent-sequence entry point - as if the user had typed it into an idle chat.

The queue is **not** a work queue: a barrier takes the whole queue at once, one insertion per entry, one answer
for the batch.

## Terms

| Term | Meaning |
|---|---|
| follow-up | A message the user composed while the chat was busy and that has not yet reached the history |
| follow-up queue | All pending follow-ups of one chat, in composition order, shared by every participant |
| level | The earliest barrier at which this entry may be injected |
| barrier | A point in the execution loops where injection is allowed to happen |
| injection | The act of turning queue entries into real messages |
| drain / flush | Inserting every pending entry, one at a time, in order |
| turn | One `Operation` execution token, plus everything a user action runs under it: insertions, drains, one generation |
| hard stop | Cancel the running turn without injecting anything |

## Data

One chat-level additional-data item, a **singleton** per chat:

```csharp
public class FollowUpChatMessagesData : AdditionalChatData
{
    // State, not a rendered message part.
    // IsVisible = false, Order irrelevant.

    /// <summary>
    /// The pending follow-ups, in the order they were composed.
    /// </summary>
    public RangeObservableCollection<FollowUpMessage> Messages { get; } = [];

    /// <summary>
    /// The level the next enqueued message gets; the last value the user picked.
    /// </summary>
    public FollowUpInjectionLevel DefaultLevel { get; set; } = FollowUpInjectionLevel.Message;
}

public class FollowUpMessage : NotifyPropertyChanged
{
    public required string Content { get; set; }
    public required string SenderLogin { get; set; }
    public AdditionalChatDataCollection Parts { get; } = [];
    public MessageVisibility Visibility { get; set; }
    public ImmutableList<string> VisibleTo { get; set; } = [];
    public bool IsVisibleToWhiteList { get; set; }
    public FollowUpInjectionLevel Level { get; set; }
    public bool GenerateIntent { get; set; }
    public bool CanExecuteCommand { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
```

- The item lives in `Chat.AdditionalData` (`ChatObjectBase`) with `ChatDataParentKind.Chat`; `AdditionalChatDataCollectionSynchronizer`
  persists it item by item and `ChangeTracker` walks the nested collection deeply - the same shape as
  `UserInputState.Parts`, which already round-trips.
- Attachments are supported from the start: `Parts` takes the same `AdditionalChatData` items the draft holds.
- The queue survives a restart. On chat open it is shown as pending and is **never** sent on its own.
- No one reads the item directly: `IFollowUpChatMessageQueue` is the only owner (see Ownership).

## Levels

```csharp
public enum FollowUpInjectionLevel
{
    Immediate = 0,      // no barrier: cancel the run, then drain
    Message = 1,        // the tool batch of one message is closed
    Agent = 2,          // one agent's cycle is over (its last message carries no tool calls)
    AgentSequence = 3,  // the whole agent sequence is over
}
```

- The level is **per entry**; `FollowUpChatMessagesData.DefaultLevel` is only the value the next entry is
  composed with.
- The gate of the queue is the **minimum** level across entries. At the first barrier whose rank reaches that
  minimum, the **entire** queue is drained, including entries with a later level.
- This enum is deliberately not `ChatExecutionLevel`: there `min` means "widest", here `min` means "earliest",
  and reusing it would invert the comparison (`Message` is `5` there and `1` here).
- `Immediate` never participates in a barrier check: it is handled by the enqueue path.

## Barriers and the early exit

| Barrier | Location | Fires when |
|---|---|---|
| `Message` | `GenerateMessageResponseAsync`, after `await Task.WhenAll(toolExecutionTasks)`, before `return` | the tool batch of one message is closed; the message is already `Success` and its hooks have run |
| `Agent` | `GenerateResponseWithAgentAsync`, inside the `response.ToolCalls.Count == 0` branch, after `RunAgentExecutionFinishedHooksAsync` | one agent's cycle is over |
| `AgentSequence` | `GenerateResponseAsync`, at the top of each iteration, before agent selection | the whole sequence is over |

The exit is a **normal return**, never an exception and never a cancellation:

```csharp
// GenerateMessageResponseAsync
finally { try { await Task.WhenAll(toolExecutionTasks); } finally { token.ThrowIfCancellationRequested(); } }
if (queue.HasEligible(FollowUpInjectionLevel.Message))
    return (domainResponseMessage, interrupted: true);
return (domainResponseMessage, interrupted: false);

// GenerateResponseWithAgentAsync
var (response, interrupted) = await GenerateMessageResponseAsync(...);
if (interrupted) return true;
...
if (response.ToolCalls.Count == 0)
{
    await RunAgentExecutionFinishedHooksAsync(...);
    if (queue.HasEligible(FollowUpInjectionLevel.Agent)) return true;
    return false;
}

// GenerateResponseAsync
while (true)
{
    token.ThrowIfCancellationRequested();
    if (queue.HasEligible(FollowUpInjectionLevel.AgentSequence)) return;
    ...select the agent...
    if (await GenerateResponseWithAgentAsync(...)) return;
    cycles++;
}
```

Why not an exception: every level carries `catch (Exception ex) { Log.Error(...); throw; }`, and
`GenerateMessageResponseAsync` also has `catch (OperationCanceledException) { Status = Cancelled; RecordFailedUsage(...); }`.
Any sentinel either logs an error six times or marks an already-successful message as cancelled.

Why not `Cancel()`: the operation token would die with the run, and the drain that must follow needs it alive.

A follow-up therefore does not inherit the run's state: `cycles` restarts at zero, so a follow-up that finds no
agent still toasts "agent selection failed" instead of ending silently, and `GetNextAgentAsync` picks the agent
and stage from scratch (an inserted user message nulls the `lastAssistantMessage` heuristic anyway).

## Ownership

| Concept | Owner | Does | Does not |
|---|---|---|---|
| queue data | `FollowUpChatMessagesData` | holds, serializes | nobody reads it directly |
| queue behaviour | `IFollowUpChatMessageQueue` | enqueue, peek, in-flight marking, remove, clear, count, min level, `HasEligible` | knows nothing about history or generation |
| message insertion | `IChatMessageInsertionService` | pre-flight, append (or replace at the edit index) as one step, run the command on the inserted message as the second step, returns the resulting intent | looks at the queue, generates, depends on `IChatExecutionService` |
| command execution | `IChatCommandInvocationService` | resolve + bind + execute under `Command` | inserts messages |
| loops and barriers | `IChatExecutionService` | the three loops, the barrier checks, `bool interrupted` | drains the queue, decides whether to generate |
| **the turn** | `IChatOperationService` | the `Operation` scope, queue-or-insert, the drain, the order, one generation | writes messages, executes commands |

Invariant: **a turn is an `Operation` execution token.** Whoever takes `Operation` owns the turn; nobody else
takes it for a turn-shaped action. Two turns never interleave: the second `Operation` cancels the first
("newest wins"), and a cancelled turn does not advance the queue because every loop iteration starts with
`token.ThrowIfCancellationRequested()`.

Consequences:

- `ChatMessageInsertionService` loses its `IChatExecutionService` dependency (it only had it to generate), so the
  executor and the insertion service can both depend on the queue without a DI cycle.
- Generation stops being a side effect of insertion: the intent is born in the input, corrected by the command,
  OR-ed by the flush, consumed once by the turn.

## The turn

```csharp
// IChatOperationService
private async Task RunTurnAsync(bool flushFirst, bool generate, CancellationToken token)
{
    var intent = generate;
    while (true)
    {
        token.ThrowIfCancellationRequested();
        if (queue.HasAny && (flushFirst || !intent))
            intent |= await FlushAsync(token);
        if (!intent) return;
        await executor.GenerateResponseAsync(token);   // may return early on a barrier
        if (!queue.HasAny) return;
        flushFirst = true;
    }
}

private async Task<bool> FlushAsync(CancellationToken token)
{
    var intent = false;
    while (queue.TryBeginNext(out var entry))          // false while another turn owns the head
    {
        UserInputInsertion inserted;
        try
        {
            inserted = await insertion.AppendUserInputAsync(entry.ToUserInput(), editIndex: null, token);
        }
        catch
        {
            queue.Abandon(entry);                      // nothing reached the history: the entry stays pending
            throw;
        }

        queue.Complete(entry);                         // the message is in the history: the entry is eaten
        intent |= entry.GenerateIntent;
        intent |= (await insertion.RunInsertedCommandAsync(inserted, entry.GenerateIntent, token)).Generate;
    }
    return intent;
}
```

- Two orders exist. `flushFirst: true` for a send, an edit commit, a stop-and-flush, an immediate level.
  `flushFirst: false` for regenerate, resend and continue: they truncate the tail and ask for an answer, and a
  queued message inserted before that answer would orphan it.
- Append **precedes** removal, and the command runs after it. Inserting a message is not a blocking step, while a
  command can run for minutes, so the entry is dropped in between: the queue always shows exactly the messages that
  have not reached the history yet, and the removal can never race a long command. A crash eats only what actually
  reached the history. The in-memory in-flight mark makes one entry belong to exactly one turn at a time; a second
  turn skips a busy head instead of appending it twice.
- An error does not trigger a drain: an exception from `executor.GenerateResponseAsync` leaves the loop, and an
  exception inside `FlushAsync` leaves the entry pending and stops the turn.

## Flows

### Send with an empty queue

`SendUserInputAsync` takes `Operation`, calls `insertion.InsertUserInputAsync`, then `RunTurnAsync(flushFirst: true, generate: result.Generate || generate)`.

### Send with a non-empty queue

`SendUserInputAsync` takes `Operation`, enqueues the input **at the tail** (level, generate intent and
`CanExecuteCommand` preserved), then runs the turn flush-first. The user's own message is therefore inserted after
the older entries, and one answer covers the batch.

### Enqueue only

`QueueUserInputAsync` runs the pre-flight, enqueues, clears the draft - and takes **no execution scope**. This is
the single deliberate exception: taking `Operation` would cancel the very run the message is meant to outlive.
When the enqueued entry's level is `Immediate`, it then calls `StopAndFlushAsync`.

### Stop and flush (Enter on an empty input while generating)

`StopAndFlushAsync`: cancel the root token, `await executionStatus.WaitUntilIdleAsync()`, take `Operation`, run the
turn flush-first with `generate: true`. The idle wait is observed on `IChatExecutionStatusService.Status`
(`Idle` when no `WithExecution` scope is alive), because the token service erases the dying plan's level entries
the moment a new `Operation` cancels them - it cannot say when the old run finished unwinding.

### Hard stop (Ctrl+Enter on an empty input, and the Stop button)

Cancel the root token and nothing else.

### Immediate level

The enqueue path cancels the run, waits for idle, and drains as a turn. The wait means a tool that ignores its
token can hold the injection for a moment; the status bar shows the run stopping.

### Edit commit

`storage.EditMessage` builds a new node at the index and makes it the leaf, so an edit truncates the tail and can
never be queued. Commit = take `Operation` (cancels the run), wait for idle, apply the edit through
`InsertUserInputAsync(..., editIndex)`, then run the turn flush-first: queued messages land after the new tail and
one answer covers the batch.

### Regenerate, resend, continue

Same as an edit commit, but `flushFirst: false`: generate first, then drain, then generate again if the batch asks.

## Input matrix

| Run | Input | Enter | Ctrl+Enter |
|---|---|---|---|
| running | non-empty | enqueue | enqueue |
| running | empty | stop, then flush and generate | hard stop |
| idle | non-empty | enqueue if the queue is non-empty, else insert and run the turn | same, with `generate: false` |
| idle | empty | flush the queue | flush the queue |

- "Empty" means no text **and** no parts.
- The Stop button is a hard stop.
- The command pre-flight runs when the message is enqueued as well as when it is inserted, so an invalid command is
  refused while the user still sees the text.
- Enqueueing clears the draft only after the entry exists; a refused pre-flight keeps the text.

## Commands and permissions

- `UserInput.CanExecuteCommand`, default `true`, copied to the entry and back. Every producer sets it: the desktop
  input and the WebUI both pass `true` today, until a participant-rights model exists.
- `false` means verbatim: no token extraction, no resolution, no execution, and no `//` unescaping.
- The decision is read at exactly one place, the message factory in `IChatMessageInsertionService`, so the direct
  path and the queue path cannot diverge.
- An invalid command found at insertion time is still guarded: the message is kept, the error and the failed
  fingerprint are attached, and generation is skipped.

## Avalonia UI (Desktop and Android)

These live in the core project - `ChatView.axaml`, `UserInputView.axaml` and their view models are shared by Desktop
and Android - so there is no per-platform UI work here, only a Blazor counterpart.

- The input is never disabled while a run is going.
- A row of **chips** sits **above the divider** between the history and the input (`ChatView.axaml`, the `Grid.Row="1"`
  border), as an overlay that takes no layout space, shifted left so it never covers `ScrollToBottomButton`
  (right-aligned, `Margin="32,16"` inside the messages panel).
- A chip is not a tab and carries no selected state: it is a button, and a chip that can be dismissed carries a
  second, nested close button inside it.
- Queue chip: hidden at `N = 0`, otherwise "Messages queued: {N}". Clicking the body opens a dialog listing the
  entries in composition order - preview, sender, level, per-entry delete. The nested close button clears the whole
  queue behind a `ConfirmDialogViewModel` (`IsDanger`): the text is unrecoverable.
- Edit mode becomes a second chip: clicking the body brings the message being edited into view, the nested close
  button cancels the edit.
- No reordering and no text editing of a queued entry in v1.

## WebUI

Mirrored in the same change, message editing last: the box is not disabled, Enter sends with generation and
Ctrl+Enter without, the queue tab and its dialog are shared state (a pending entry is visible to every
participant, with its sender), `CanExecuteCommand` travels on `UserInput`, and a user message can be edited from
the chat page.

## Out of scope

- Merging accumulated messages into one: it would lose per-message sender and visibility.
- Auto-sending a restored queue when a chat is opened.
- Changing stop semantics: a hard stop stays a hard stop and never touches the queue.
- Queueing for agent tasks and sub-agents that run outside the chat turn.

## Appendix A - design log

Decisions settled in the grilling, in order.

| # | Decision |
|---|---|
| D1 | The queue is **one** `FollowUpChatMessagesData` item holding a collection, not one item per message. The idea file's per-item design is discarded. |
| D2 | The default level lives on the queue item, not in `ChatExecutionSettings`; the level is per entry; the queue gate is the minimum. |
| D3 | `Immediate` cancels the whole run, including a running command; it is an action on the enqueue path, not a barrier. |
| D4 | The barrier exit is a normal return with `bool interrupted`; the interrupted agent gets no execution-finished hooks. |
| D5 | The drain belongs to the turn owner, not to the insertion service; insertion stays a queue-blind primitive. |
| D6 | Command resolution and execution move to `IChatCommandInvocationService`, called by insertion. |
| D7 | When an idle send meets a non-empty queue, the new message joins the **tail**, and the queue is drained in order. |
| D8 | "Empty input" means no text and no parts. The Stop button is a hard stop. |
| D9 | The pre-flight runs at enqueue time and is re-checked by the guard at insertion. |
| D10 | A queued message is shown only in the queue, never in the history, until it is inserted. |
| D11 | The batch generates if **any** entry asks for generation; a command lowering its own intent does not lower the batch. |
| D12 | The queue is a chip above the divider: a button showing the count that opens a dialog with per-entry delete, plus a nested close button that clears everything behind a confirmation; the edit mode becomes a second chip whose body brings its message into view and whose close button cancels the edit; the row avoids `ScrollToBottomButton`. Chips are not tabs, and they live in the shared Avalonia UI, so Desktop and Android get them together. |
| D13 | `CanExecuteCommand` travels on `UserInput` and the entry; `false` is verbatim; everything passes `true` for now, WebUI included. |
| D14 | The WebUI is mirrored in this change, message editing included; editing ships last. |
| D15 | Attachments are supported in the queue from the start. |
| D16 | Errors stop the turn and never trigger a drain. |
| D17 | An edit commit is a turn: cancel, wait for idle, apply, then drain after the edit. Regenerate/resend/continue generate first and drain after. |
| D18 | `IChatOperationService` is promoted to own the turn; `Operation` and the turn share one lifetime; "newest wins" replaces a drain mutex. |
| D19 | Insertion is two steps - append, then run the command on the appended message - so the queue drops the entry between them: inserting is not a blocking step, a command is, and the queue must never show an already-inserted message. |

## Appendix B - trace findings

Found while tracing "Enter during a run" through the real bodies. Each is a constraint, not a taste.

**F1 - a sentinel exception cannot carry the exit.** Every execution level logs and rethrows on `catch (Exception)`,
and the message level marks `Cancelled` plus records failed usage on `catch (OperationCanceledException)`. A return
value crosses the two frames without either.

**F2 - the insertion primitive must stay queue-blind.** The flush calls `InsertUserInputAsync` per entry, so a
"queue non-empty means enqueue" rule inside it would enqueue on every insert forever. Two methods: the primitive
and the queue-aware send in the turn owner.

**F3 - the executor cannot depend on the insertion service.** Insertion used to depend on the executor; the barrier
check runs the other way. The queue service is the shared dependency, and it depends on nothing but `Chat`.

**F4 - four call sites reach generation today** (`InsertUserInputAsync`, `ContinueGenerationAsync`,
`RegenerateMessageAsync`, `ResendMessageAsync`). One of them outside the turn loop silently ends a run with a
non-empty queue, so the turn loop is the only way in.

**F5 - idleness is observed on `IChatExecutionStatusService`, not on tokens.** `WithToken(Operation)` removes and
cancels the previous level entry, so `ReleaseLevel` on the dying run finds nothing; `Status` is driven by
`WithExecution`, which wraps the whole run including its `finally` blocks.

**F6 - the level enum must be its own type.** `ChatExecutionLevel`'s numbers run widest-to-narrowest, the opposite
of the earliest-gate order this feature needs.

**F7 - `cycles` and the "no agent" toast fall out for free** once the follow-up re-enters the sequence entry point.

**F8 - the command permission is read at one hop only** (the message factory), which is what keeps the direct and
the queued path identical.

## Appendix C - surfaces touched

- `LLM/Services/`: `ChatExecutionService`, `ChatOperationService`, `ChatMessageInsertionService`, `ChatExecutionStatusService`,
  new `ChatCommandInvocationService`, new `FollowUpChatMessageQueueService`, `ChatExecutionLevel` (untouched),
  new `FollowUpInjectionLevel`, `UserInput`.
- `LLM/MVVM/`: `Additional/FollowUpChatMessagesData`, `UserInputViewModel`, `UserInputView.axaml(.cs)`, `ChatView.axaml(.cs)`,
  the queue dialog view models.
- `LLM/MVVM/Additional/`: the new item participates in `AdditionalChatDataCollection`, `AdditionalChatDataCollectionViewModel`.
- `Localization/Resources/*/chat.loc` (or the closest existing file) for the tab, the dialog and the confirm text.
- `LLMDesktopAssistant.Blazor/`: `ChatPage.razor`, the message component (edit action), a queue component.
