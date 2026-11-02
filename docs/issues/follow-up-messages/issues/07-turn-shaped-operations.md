# 07: Edit commit, regenerate, resend, continue

Status: open
Type: task
Blocked by: 05

## What to build

The remaining operations that put a message into the history or ask for an answer, each wired into the turn with the
right order.

See [`spec.md`](../spec.md) sections *Flows* (edit commit, regenerate, resend, continue).

## Rules

- **Edit commit** (`SendEditedUserInputAsync`): `Operation` -> `await WaitUntilIdleAsync()` -> `InsertUserInputAsync(...,
  editIndex)` -> turn **flush-first**. `ChatStorageService.EditMessage` builds a new node at the index and makes it the
  leaf, so a commit truncates the tail and an edit can never be queued. The current early `WithToken(Operation)` moves:
  the idle wait must precede the scope, or the new operation would cancel the run it is waiting for.
- **Regenerate / resend / continue**: `Operation` (or the idle wait when a run is going) -> the existing storage
  branch/truncation -> turn **generate-first** -> then drain -> then generate again if the batch asks. Inserting queued
  messages before the answer would orphan the message being regenerated.
- `ContinueGenerationAsync` keeps its name for the Blazor `GenerationReadinessService` caller; it is a turn with no
  input and no truncation.
- A command inside an edited message still runs through the same invocation service; the permission flag travels on the
  edited `UserInput`.

## Acceptance

- Committing an edit while a run is going stops it, waits for the history to settle, then applies the edit.
- A non-empty queue after a commit is injected **after** the edited message, and one answer covers the batch.
- Regenerate with a non-empty queue produces the new answer first, then injects the queue, then answers again if an
  entry asked for it.
- No turn-shaped operation calls `executor.GenerateResponseAsync` directly any more (trace finding F4).

## Not in scope

Editing a queued entry, editing in the WebUI (ticket 10).
