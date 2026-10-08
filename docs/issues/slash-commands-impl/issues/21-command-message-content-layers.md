# 21: Command message-content layers

Status: ready-for-agent
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

- [ ] `AdditionalMessageContentPart` exists, is BSON-serializable, and its `Content` reaches the rendered message
      content when `MessagePartsFacet.Content` is requested (unit test).
- [ ] `AdditionalMessagePart.ChipTitle` is `LocaleKeyBase?`; the attachment chip still shows the file name; the chip view
      binds via `{loc:Loc {Binding ChipTitle}}`; a BSON round-trip test covers a localized chip title.
- [ ] `SlashCommandExecutionResult` carries `ModelFacingMode?`; the host writes the effective mode into the fingerprint.
- [ ] `ChatMessageQuoteRenderer` renders `Raw` / `Neutral` (`/` + `RawToken`) / `Hidden` correctly, appending content
      parts in every mode; unit tests cover all three, including the parts-visible-while-Hidden case.
- [ ] `MessageVisibilityService` unchanged; main + desktop build; the filtered test suite stays green.

## Comments

<!-- appended conversation -->
