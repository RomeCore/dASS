# 24: Edit-check fix — the pre-flight validates commands on edits

Status: open
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

- [ ] `CanInsertUserInput(input, generate, editIndex)` validates the command for edits: unknown command → blocked,
      invalid arguments → blocked, valid command → ok, non-command text → ok, `EnableCommands == false` → ok.
- [ ] The doc-comments of `IChatMessageInsertionService` and `CanInsertUserInput` no longer claim edits skip commands.
- [ ] `InsertUserInputAsync` behaviour is unchanged (it already runs the command on edits).
- [ ] Unit tests cover the edit cases above (boundary + negative, not happy-path only).
- [ ] Filtered slash-commands suite stays green; main builds.

## Answer

<!-- appended on resolution -->

## Comments

<!-- appended conversation -->
