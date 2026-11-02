# 01: Follow-up queue model and queue service

Status: open
Type: task
Blocked by:

## What to build

The persisted queue and the only service allowed to touch it. Nothing executes yet: this ticket makes the queue
exist, persist, and answer the question the barriers will ask.

See [`spec.md`](../spec.md) sections *Data*, *Levels* and *Ownership*.

## Contracts

```csharp
public enum FollowUpInjectionLevel { Immediate = 0, Message = 1, Agent = 2, AgentSequence = 3 }

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

public class FollowUpChatMessagesData : AdditionalChatData
{
    public RangeObservableCollection<FollowUpMessage> Messages { get; } = [];
    public FollowUpInjectionLevel DefaultLevel { get; set; } = FollowUpInjectionLevel.Message;
}

public interface IFollowUpChatMessageQueue
{
    int Count { get; }
    bool HasAny { get; }
    FollowUpInjectionLevel DefaultLevel { get; set; }
    bool HasEligible(FollowUpInjectionLevel barrier);   // HasAny && the minimum level reaches the barrier

    FollowUpMessage Enqueue(UserInput input, FollowUpInjectionLevel level, bool generateIntent, bool canExecuteCommand);
    bool TryBeginNext(out FollowUpMessage entry);       // false while another turn owns the head
    void Complete(FollowUpMessage entry);               // remove: the message is in the history
    void Abandon(FollowUpMessage entry);                // keep it pending, release the in-flight mark
    void Remove(FollowUpMessage entry);                 // the dialog's per-entry delete
    void Clear();

    event Action? Changed;
}
```

## Rules

- The item is created lazily on first `Enqueue` and appended to `Chat.AdditionalData`; a chat without pending
  messages carries no item.
- The item is state, not a rendered part: `IsVisible = false`.
- The service resolves the item once the chat scope exists; it must survive the item being replaced on chat load
  (re-resolve when the collection changes, or construct it from `Chat.AdditionalData` on each access).
- The in-flight mark is **in memory only** and covers one entry at a time. `TryBeginNext` returns the head and marks
  it; a second caller gets `false`. `Complete` removes and unmarks, `Abandon` unmarks.
- `HasEligible` compares the minimum level of the entries against the barrier; an empty queue is never eligible.
- Attachments take the same `AdditionalChatData` items the draft holds; `Parts` is a restored copy, not a shared
  reference to the user's draft collections.

## Acceptance

- Enqueue three entries, restart the app, reopen the chat: three entries, same order, same levels, same parts.
- `HasEligible(Message)` is true when the minimum is `Message` or `Immediate` and false when the minimum is `Agent`.
- The item round-trips through `AdditionalChatDataModel` with the concrete `FollowUpChatMessagesData` type on load.
- `TryBeginNext` twice in a row without `Complete`/`Abandon` yields the second call `false`.

## Not in scope

Barrier checks, the drain, any UI.
