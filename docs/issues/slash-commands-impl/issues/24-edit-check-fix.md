# 24: Edit-check fix — the pre-flight validates commands on edits

Status: resolved
Type: task
Blocked by:

## What to build

Message edits **do** run commands: `ChatMessageInsertionService.InsertUserInputAsync` extracts and executes the
command regardless of `editIndex`, and that was always the intent. The view-model pre-flight disagrees — it
short-circuits `editIndex is not null` to `Ok` without validating the command, so an unknown command or a broken
argument list on an edit is neither refused before the commit nor blocked; it lands as an inserted message with an
error attached (and the user's text is already gone).

Remove the short-circuit so an edit is checked exactly like a new message, and correct the doc-comments that state the
opposite (`IChatMessageInsertionService`'s "edits never run a command" and the `editIndex` parameter doc on
`CanInsertUserInput`).

## Acceptance criteria

- [x] `CanInsertUserInput(input, generate, editIndex)` validates the command for edits: unknown command → blocked,
      invalid arguments → blocked, valid command → ok, non-command text → ok, `EnableCommands == false` → ok.
- [x] The doc-comments of `IChatMessageInsertionService` and `CanInsertUserInput` no longer claim edits skip commands.
- [x] `InsertUserInputAsync` behaviour is unchanged (it already runs the command on edits).
- [x] Unit tests cover the edit cases above (boundary + negative, not happy-path only).
- [x] Filtered slash-commands suite stays green; main builds.

## Answer

Removed the `editIndex is not null` short-circuit in `ChatMessageInsertionService.CanInsertUserInput`: an edit now
resolves and validates its command exactly like a new message (`EnableCommands == false` still disables the whole
command path). `InsertUserInputAsync` is unchanged — it already ran the command on edits.

Doc-comments corrected: the `editIndex` parameter doc on `IChatMessageInsertionService.CanInsertUserInput` (and the
inline comment in the implementation) no longer claim edits skip commands; it now says an edit runs its command too
and is validated identically.

Tests (`ChatMessageInsertionServiceTests`): a plain edit and a valid edit command are accepted; an edit with an
unknown command and an edit with bad arguments are refused; an edit with commands disabled is accepted (the former
`CanInsertUserInput_AnEdit_IsAlwaysAccepted` is replaced by five boundary/negative cases). Filtered slash-commands
suite: **162 passed**. Also reconciled the pre-existing stale slash-command tests left by the maintainer's recent
behaviour commits (`wait` default `true`, the `/agent` result banner, `/skill` back to `Raw`) in a separate commit.

## Comments

<!-- appended conversation -->
