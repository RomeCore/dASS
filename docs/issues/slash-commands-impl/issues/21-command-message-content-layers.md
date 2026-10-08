# 21: Command message-content layers

Status: resolved
Type: task
Blocked by:

## What to build

The reusable layers a slash command needs to write text into its own message and to control how that message faces the
model. Nothing user-visible yet — the commands that use them land in 22 and 23.

- **`AdditionalMessageContentPart : AdditionalMessagePart`** — a text part whose `Content` is appended to the
  model-facing message content by `ChatMessageQuoteRenderer` when `MessagePartsFacet.Content` is requested (the facet
  already documents that UI-collapsed parts are part of `Content`). BSON-serializable, so it survives a reload.
- **`AdditionalMessagePart.ChipTitle` changes type from `string?` to `LocaleKeyBase?`** — a chip title is user-facing
  and must localize (like the message `Error`). `LocaleKeyBase` is BSON-serializable and there are 0 users, so no
  migration is needed. Update the attachment chip (the file name becomes a const key) and the chip view binding
  (`{loc:Loc {Binding ChipTitle}}`).
- **`SlashCommandExecutionResult` gains `ModelFacingMode?`** (`null` = inherit the command's declarative
  `SlashCommandInfo.ModelFacingMode`). The host resolves the effective mode and records it in
  `SlashCommandFingerprint` (whose `RawToken` — the **bare command name**, e.g. `grilling` — was renamed from
  `CommandName`).
- **`ChatMessageQuoteRenderer` owns the model-facing content projection** (the ticket-16 decision put it here, not in
  `MessageVisibilityService`): `Raw` → `message.Content`; `Neutral` → `"/" + fingerprint.RawToken` (the bare name, so
  `/grilling` even when the user typed `/skill:grilling`); `Hidden` → nothing; in every mode the message's
  `AdditionalMessageContentPart`s are appended. `MessageVisibilityService` is **not** touched.

## Acceptance criteria

- [x] `AdditionalMessageContentPart` exists, is BSON-serializable, and its `Content` reaches the rendered message
      content when `MessagePartsFacet.Content` is requested (unit test).
- [x] `AdditionalMessagePart.ChipTitle` is `LocaleKeyBase?`; the attachment chip still shows the file name; the chip view
      binds via `{loc:Loc {Binding ChipTitle}}`; a BSON round-trip test covers a localized chip title.
- [x] `SlashCommandExecutionResult` carries `ModelFacingMode?`; the host writes the effective mode into the fingerprint.
- [x] `ChatMessageQuoteRenderer` renders `Raw` / `Neutral` (`/` + `RawToken`) / `Hidden` correctly, appending content
      parts in every mode; unit tests cover all three, including the parts-visible-while-Hidden case.
- [x] `MessageVisibilityService` unchanged; main + desktop build; the filtered test suite stays green.

## Answer

Implemented. The prefactor both executors write through:

- **`AdditionalMessageContentPart : AdditionalMessagePart`** carries a `Content` string and is persisted with the
  message (BSON round-trip covered).
- **`AdditionalMessagePart.ChipTitle` is now `LocaleKeyBase?`** (the attachment chip uses
  `Locale.GetConstKey(fileName)`; the chip view binds `{loc:Loc {Binding ChipTitle}}`).
- **`SlashCommandExecutionResult.ModelFacingMode?`** — the host folds it into
  `SlashCommandFingerprint.Create(..., modelFacingMode)` (override → the command's declarative mode → `Raw`).
- **`MessageContentProjector`** owns the projection and is called by `ChatMessageQuoteRenderer`: `Raw` → the message
  content, `Neutral` → `"/" + RawToken` (the bare name), `Hidden` → nothing, with the
  `AdditionalMessageContentPart`s appended in every mode. `MessageVisibilityService` is untouched — the ticket-16
  decision put the projection in the renderer after all.

Deviations / notes:

- The projection was extracted into a **pure static `MessageContentProjector`** rather than inlined in the renderer,
  so the three modes and the parts-visible-while-Hidden case are unit-testable without the whole rendering pipeline
  (the renderer needs a template library, a user manager, an agent manager and expanders).
- `RawToken` (the fingerprint field, renamed from `CommandName`) is the **bare command name**, so `Neutral` renders
  `/grilling` even when the user typed `/skill:grilling` — recorded here as the settled semantics.

Builds: main green. Filtered runs: slash-commands **130 passed**, the new projector/part set **8 passed**
(desktop build skipped at the maintainer's request).

## Comments

<!-- appended conversation -->
