# Order, visibility and lifetime of Lua-created parts

Status: needs-triage
Type: task
Blocked by:

## Question

Every part a script creates lands in a fixed shape: `AdditionalMessagePart` / `AdditionalMessageContentPart` /
`NativeAttachmentMessagePart` from `LuaAttachmentParts` always set `IsRestorable = false`, and
`DynamicAxamlControlAdditionalData` / `TerminalAdditionalViewModel` never get an `Order` from Lua.
`AdditionalChatData.Order` is a virtual get-only property, `IsVisible` and `IsTemporary` are settable in C#
but have no Lua path.

The consequence is that a script cannot control where its part appears relative to the parts other
producers created (the collection sorts with `AdditionalViewModelComparer`), cannot hide a part that is
only data, and cannot make an attachment survive a draft restore.

Decisions this ticket must settle:

1. Is `Order` exposed as a per-append argument (`append_part(title, icon, color, order)`), as a field on a
   part handle, or not at all?
2. Does `Order` on the created part mean anything for `DynamicAxamlControlAdditionalData` and
   `TerminalAdditionalViewModel`, or should those two stay orderless?
3. Are `IsVisible` / `IsTemporary` exposed? `IsTemporary` is the only way a script could attach something
   that is *not* persisted, and terminal attachments already default to it in C#.
4. Should any Lua-created part be `IsRestorable = true` (so editing the message restores it into the draft),
   or is "Lua parts are never restorable" a documented invariant?
