# 25: General input-completion core

Status: resolved
Type: task
Blocked by:

## What to build

The reusable, UI-agnostic core of the input autocomplete. It is deliberately **not** command-specific: slash commands
are its first source; chat-agent mentions (`@Code Reviewer` — names may contain spaces, so the completion engine must
never assume whitespace-delimited tokens) are a later one. Pure C# — no Avalonia, no `SlashCommand*` types here —
consumed by both the popup view model and the ghost rendering.

New `InputCompletion/` folder (renamed from `Completion/` — "Completion" alone collides with the LLM chat-completion vocabulary):

```csharp
interface IInputCompletionSource
{
    int Priority { get; }                                          // which source claims the caret
    bool TryCompute(InputCompletionRequest request, out InputCompletionResult result);
}

readonly record struct InputCompletionRequest(string Text, int CaretIndex);
readonly record struct InputCompletionSpan(int Start, int Length);

sealed class InputCompletionResult
{
    InputCompletionSpan Span;          // the region the completion replaces — defined by the source, not by whitespace
    InputCompletionState? State;       // may be non-null with an empty Items list (state-only popup)
    IReadOnlyList<InputCompletionItem> Items;
    int SelectedIndex;
}

sealed class InputCompletionItem { string InsertText; string Display; LocaleKeyBase? Description;
                                   InputCompletionKind Kind; bool IsDefeated; }
sealed class InputCompletionState { LocaleKeyBase? Title; LocaleKeyBase? Description; InputCompletionKind Kind; }
enum InputCompletionKind { None, Command, Argument, Mention }
```

A chat-scoped `IInputCompletionService` resolves **one** active source: iterate the registered
`IInputCompletionSource`s by `Priority` descending and return the first whose `TryCompute` returns `true`; `null`
when none claims the caret.

## Acceptance criteria

- [x] The types above exist; `Span` is in absolute raw-text coordinates and is defined by the source, not by whitespace.
- [x] `IInputCompletionService` (chat-scoped) picks the highest-priority source that claims the caret — one at a time.
- [x] `State` may be non-null with empty `Items` (the popup shows the state only).
- [x] No Avalonia and no `SlashCommand*` types leak into the core.
- [x] Unit tests cover priority resolution, no-source (null), and state-without-items.
- [x] Main builds.

## Answer

Implemented the general input-completion core in `src/LLMDesktopAssistant/InputCompletion/` (the folder was renamed
from `Completion/`, which collided with the LLM "chat completion" vocabulary used throughout the codebase):

- `InputCompletionRequest(Text, CaretIndex)`, `InputCompletionSpan(Start, Length)` (+ `End`, `FromBounds`),
  `InputCompletionItem` (`InsertText`, `Display` → `DisplayText`, `Description`, `Kind`, `IsDefeated`),
  `InputCompletionState` (`Title`, `Description`, `Kind`), `InputCompletionResult` (`Span`, `State`, `Items`,
  `SelectedIndex`) and the `InputCompletionKind` enum.
- `IInputCompletionSource` (`Priority`, `TryCompute(request, [NotNullWhen(true)] out result)`): a source returns
  `false` — not an empty result — when it does not own the caret.
- `IInputCompletionService` + `InputCompletionService` (`[ChatService]`): orders sources by `Priority` descending (ties
  broken by ordinal type name) and returns the first claiming source's result, or `null` when none claims. Harmless
  today — no source is registered yet.

Tests (`InputCompletionServiceTests`, 9 cases): no sources → null; no source claims → null; the highest priority wins
and the lower source is not asked; a non-claiming source is skipped and falls through; ordering is independent of
registration order; a priority tie is deterministic; a state-without-items result is returned; `DisplayText` fallback;
`Span.End` / `FromBounds`. Filtered run: **9 passed**.

## Comments

<!-- appended conversation -->
