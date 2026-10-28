# AR-0029 - Auto-scroll pulls the reader to the bottom on every insertion and at the end of a generation

- **Status:** open
- **Severity:** friction
- **Lane:** user
- **Surface:** UI
- **First seen:** 2026-10-29

## What I expected

The reference is the desktop client: the view follows output while the reader is at the bottom, stays where it is once the reader scrolls up, does not move because someone else wrote a message, and offers a way back to the bottom. Streaming output scrolls smoothly and does not fight itself.

## What happened

The view jumps to the bottom when a message arrives and jumps again when the current generation finishes, even after the reader has scrolled up to read earlier messages. During streaming the motion is uneven, and the reader's place is lost at every turn boundary.

Traced, from the scroll helper and its callers:

- auto-scroll is switched on and off around generation only, and switching it off performs an unconditional scroll to the bottom, which is the jump at the end of a generation;
- a content observer scrolls on every DOM mutation, with smooth behaviour, so a streaming message restarts the same animation repeatedly instead of moving once;
- the "is the reader near the bottom" test compares positions while a smooth animation is still in flight, so the test's answer depends on the animation rather than on the reader;
- there is no latch for "the reader scrolled away", and no control to return to the bottom other than manual scrolling;
- an explicit scroll also follows the user's own send, which is the one case where it is wanted.

## Why it costs

Reading anything above the newest message in an active room is impossible: every inserted node and every turn boundary takes the reader back. The end-of-turn jump is repeated for every turn, so the cost is per message, not per session.

## Repro

Start a generation, scroll up to an earlier message, and wait for the generation to finish. The view returns to the bottom without any action.

## Witnesses

- 2026-10-29 - this session - reported by the user after comparing the WebUI with the desktop client.

## What I did instead

Scroll back up after every message and after every completed turn.

## Proposed fix

Model the state instead of the event:

- keep one "pinned to bottom" flag, set by a user scroll to the bottom and cleared by any deliberate scroll up;
- follow content only while the flag is set, with an instant scroll or a per-frame coalesced scroll that does not restart an animation;
- never force a scroll at the end of a generation; the end of a turn is not a reason to move the reader;
- keep the reading position when content above the viewport grows, which is the streaming case;
- add a jump-to-bottom control that appears when the flag is clear.

## Unverified

Whether the container or its content is observed for size changes, and whether the reader's scroll position survives a markdown re-render of the streaming message. Both were read as code, not measured.
