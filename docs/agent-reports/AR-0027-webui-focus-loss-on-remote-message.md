# AR-0027 - Any participant's message removes focus from every other participant's input

- **Status:** open
- **Severity:** friction
- **Lane:** user
- **Surface:** UI
- **First seen:** 2026-10-29

## What I expected

A shared room behaves like a shared room: what another participant does never interrupts my typing. My own send clears my own input and nothing else. Focus in the message box survives new messages, streaming output, tool cards and status changes.

## What happened

When any participant sends a message, the caret disappears from the message box of every participant in the room, including those who are mid-sentence. The next keystroke goes nowhere until the box is clicked again.

Traced: the message box is rendered disabled while a generation is running, and that flag is driven by the chat's shared execution state, which the send raises for everyone. The page also re-renders on every change of the message collection, so the input is re-evaluated at the same moment.

## Why it costs

In a room with more than one active participant, every message costs every other participant the caret. The lost keystrokes are invisible: the user keeps typing and the text silently does not appear. In a busy room typing becomes unusable, which defeats the point of a multi-participant client.

## Repro

Open the chat in two browsers as two accounts. Type into the message box in the first browser and, without sending, send a message from the second. Focus in the first browser is gone.

## Witnesses

- 2026-10-29 - this session - reported by the user while testing the WebUI with several participants.

## What I did instead

Click back into the box and retype the lost words.

## Proposed fix

The real fix is the queue: keep the box enabled and accepting text while the chat is busy, and send the text when the chat becomes idle. The follow-up messages idea already specifies a queue filled while an agent run is in progress, with a per-message injection level; if the WebUI adopts it, the disabled flag disappears by construction and this report closes with it.

Cheaper, independent of the queue:

- never disable the message box of a participant who is not the one who started the generation;
- stop re-rendering the whole page on message collection changes, or move the input into a component that does not subscribe to the collection.

## Unverified

Whether the focus loss is caused only by the disabled toggle or also by the page-wide re-render; both must be re-tested with two participants after either change. The queue idea covers the desktop queue and does not today describe the WebUI input, so the WebUI side of it would need to be stated.
