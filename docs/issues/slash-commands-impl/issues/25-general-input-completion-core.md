# 25: General input-completion core

Status: open
Type: task
Blocked by:

## What to build

The reusable, UI-agnostic core of the input autocomplete. It is deliberately **not** command-specific: slash commands
are its first source; chat-agent mentions (`@Code Reviewer` — names may contain spaces, so the completion engine must
never assume whitespace-delimited tokens) are a later one. Pure C# — no Avalonia, no `SlashCommand*` types here —
consumed by both the popup view model and the ghost rendering.

New `Completion/` folder:

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

- [ ] The types above exist; `Span` is in absolute raw-text coordinates and is defined by the source, not by whitespace.
- [ ] `IInputCompletionService` (chat-scoped) picks the highest-priority source that claims the caret — one at a time.
- [ ] `State` may be non-null with empty `Items` (the popup shows the state only).
- [ ] No Avalonia and no `SlashCommand*` types leak into the core.
- [ ] Unit tests cover priority resolution, no-source (null), and state-without-items.
- [ ] Main builds.

## Answer

<!-- appended on resolution -->

## Comments

<!-- appended conversation -->
