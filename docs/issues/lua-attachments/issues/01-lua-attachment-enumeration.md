# Reading attachments from Lua

Status: needs-triage
Type: task
Blocked by:

## Question

Lua can now attach much more than it can read. `IChatAttachmentHost.GetAttachments()` exists on
every host (chat, message, tool call), `AdditionalChatDataCollection` has `Has<T>` / `TryGet<T>` /
`GetAll<T>` / `TryReplace<T>`, but **none of it is reachable from a script**: a Lua command cannot
ask "did I already attach a terminal here?" or "is there a uicontrol part on this message?".

The consequence is that every `append_*` is blind and additive: running the same command twice on
the same message stacks two chips, two terminals, two controls, and there is no way to update the
existing one.

Decisions this ticket must settle:

1. Does the surface enumerate **host attachments** (`msg:parts()`, `dass.tool.result.parts()`,
   `dass.chat.parts()`) or only the handles the script itself created?
2. What does an enumerated part look like to Lua - a type name and a chip title, or a live handle
   that can be removed/updated (`msg:remove_part(part)`)?
3. Is `RemoveAttachment` exposed directly (`msg:remove_data(handle)`), given that removal **disposes**
   the part and therefore kills a live terminal?
4. Does the update path go through `TryReplace<T>` (replace-in-place, preserving position) or through
   remove+append?

Related: `..\02-lua-attachment-order-and-visibility.md` (the `Order` half of "where does my part land").
