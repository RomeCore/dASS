# 08: Desktop input matrix

Status: resolved
Type: task
Blocked by: 05, 06

## What to build

The keyboard and button behaviour that fills the queue, and the level selector that rides with it.

See [`spec.md`](../spec.md) sections *Input matrix*, *Levels*.

## Contracts

- `UserInputViewModel`: `SelectedLevel { get; set; }` bound to a combo in the input row and persisted through
  `FollowUpChatMessagesData.DefaultLevel`; `HasFollowUps { get; }`; `EnqueueCurrentUserInputAsync(bool generate)`;
  `FlushFollowUpsAsync()`; `IsGenerating` keeps tracking the root token.
- `UserInputView.Send(viewModel, generate)` becomes the matrix:

```csharp
if (viewModel.IsGenerating)
{
    if (!viewModel.IsEmpty) viewModel.EnqueueCurrentUserInputAsync(generate);
    else if (generate) viewModel.StopGenerationAndFlushAsync();
    else viewModel.StopGenerationAsync();
    return;
}
if (!viewModel.IsEmpty) viewModel.SendCurrentUserInputAsync(generate);
else if (viewModel.HasFollowUps) viewModel.FlushFollowUpsAsync();
```

## Rules

- The input is never disabled, and `SendMessageCommand.CanExecute` no longer depends on the root token.
- "Empty" is `IsEmpty` as it already is defined: no text **and** no parts.
- Enqueue runs the same pre-flight as a send (`CanInsertUserInput`) and keeps the text on refusal; the draft is cleared
  only after the entry exists.
- The Stop button (`CancelGenerationCommand.Execute`) becomes `StopGenerationAsync` - a hard stop.
- Enter/Tab still accept an open completion first; the matrix applies only after the completion did not claim the key.
- Ctrl+Enter keeps sending without generation when idle.

## Acceptance

- All eight cells of the matrix table behave as written, with the completion popup closed.
- Typing during a run leaves the text in the box and adds a tab count instead of a message.
- A refused command while running keeps the text in the box and shows the toast.
- The level selector's choice survives a restart and is the level of the next enqueued entry.

## Not in scope

The tab strip and its dialog (ticket 09).

## Answer

The matrix landed in `UserInputView.Send` with `Run` for the fire-and-forget continuations (cancellation is filtered out of the log). The level selector lives in the right toolbar as an icon-only combo - the icon collapsed, the icon and the localized text in the list - and it is visible at all times. `SendMessageCommand.CanExecute` no longer depends on the execution token. Two review fixes: an open edit is committed instead of queued, and the Stop button routes through `StopGenerationAsync` as a hard stop.
