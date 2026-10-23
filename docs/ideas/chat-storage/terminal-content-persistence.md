# A finished terminal keeps its content after the chat is reopened

Status: ready-for-agent
Type: task
Blocked by:

## Requested

- A terminal card in a chat keeps showing what it showed after the chat is reopened, instead of disappearing.
- Persistence covers the full content, colours included, not only the text.
- The packed form was proposed as a row-major, left-to-right walk with a nullable colour per cell.
- The mechanism proposed was to stop marking the terminal card as temporary.
- Whether a colour per cell is the right unit of packing was named as the open question.

## Proposed design

- **A running terminal is not persisted; a snapshot of it is.** The card owns a cancellation source, an exit task, a pty and a live emulator, none of which is serializable. The card therefore stays temporary while the process runs and becomes persistent only once the process has exited and a snapshot exists. The storage layer already handles that transition: flipping the temporary flag inserts or deletes the additional-data row.
- **The snapshot is taken once, when the process completes, before the flag flips**, so the row is inserted with its payload instead of being inserted empty and updated a moment later.
- **Only what is needed to draw survives**: the emulator buffer, the column count it was captured at, and a format version. Process identity, launch parameters and the session itself are not part of the record.
- **A restored terminal is history, not a session.** No connection is attached, so the existing input routing degrades on its own: mouse reporting and alternate-scroll are guarded by the presence of a connection and only local scrollback remains. Nothing may offer typing, cancellation or focus reporting into a dead process.
- **A nullable colour per cell is refused as the unit of packing.** Cost must be proportional to style changes, not to cells: console output is dominated by default-styled cells, and a full buffer is hundreds of thousands of cells. The record is row text plus runs of position, length and style id, with a style table of foreground, background and attributes carried once per snapshot.
- **The snapshot is a single compressed blob and a plain object.** A byte payload is the only shape that stays cheap through storage, and it must not be an observable collection or an observable model, or the change tracker will walk and copy the whole grid on every notification.
- **The cost being bought down is reading, not writing.** Each additional-data part is its own row, so a snapshot is written once. What a naive cell grid costs is at chat open: the row is deserialized in full and held for as long as the chat is open, once per terminal card in the history, plus its share of the database file.
- **The serializer walks the buffer the same way the renderer does**, grouping cells by background, foreground and attributes, so what was drawn and what was saved cannot drift apart.
- **The capture width is part of the record.** A restored grid cannot be re-flowed without misrepresenting a full-screen frame, so the restored surface is laid out at the captured columns rather than at whatever width the card later gets.
- **A full-screen frame is recorded as a note, not as content.** The normal buffer is what is persisted; if the process died inside the alternate buffer, the snapshot says so and keeps whatever the normal buffer held.
- **Staged: text first, colours second.** The text-only snapshot is small and answers most of the value (read it, copy it, see where it failed), while the text of the same run is already carried by the tool call result. Colour is the visual-fidelity half and is worth its format only after the text path exists.
- **A size cap with a text fallback.** Past the cap the snapshot degrades to plain lines and says so, rather than storing an unbounded grid.

## Alternatives considered

- **ANSI replay as the payload** — the text with style codes between runs, restored by feeding a fresh emulator. Not refused: it needs less code to restore, since the emulator is the only reader. Second choice because trailing blanks and wide cells still have to be reconstructed and the captured width becomes implicit.
- **Serializing the live card directly** — refused, the session is not serializable and a half-written card is worse than an absent one.
- **A separate blob store or collection for the payload** — refused, additional chat data already is its own collection with its own row per part.

## Not in scope

- Reattaching or resuming a process after a restart. A restored terminal is a fixed picture.
- A timing-preserving session recorder; the snapshot is a final state, not a tape.
- Migration of existing chats. Terminals from earlier sessions were never stored and stay absent.
- Persisting the buffer while the process is still running.

## Comments

- The open question left by the request is whether colour fidelity justifies the second stage at all, or a text snapshot is sufficient.
- Cost estimate behind the packing refusal: a five-thousand-line buffer at one hundred and twenty columns is six hundred thousand cells.
