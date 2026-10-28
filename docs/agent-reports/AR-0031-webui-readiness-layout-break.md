# AR-0031 - Readiness checkboxes break the input row on narrow screens

- **Status:** open
- **Severity:** visible
- **Lane:** user
- **Surface:** UI
- **First seen:** 2026-10-29

## What I expected

The input row keeps its shape at any window width. Secondary controls collapse, wrap or move into a popover before they crowd the message box, and the message box keeps a usable width.

## What happened

On a phone, and in a narrow desktop window, the readiness controls crowd the message box: the row does not wrap, the labels of the two checkboxes collide with the ready counter, and the whole tools row overflows sideways. The message box is squeezed at the same time, which is the part that hurts.

Traced: the tools row is a single non-wrapping flex row that holds a visibility select and the readiness group. The readiness group is a fixed-height flex row with two text labels and a counter, all at fixed font sizes, and no rule lets it shrink, wrap or hide its labels. Nothing in the layout distinguishes a wide viewport from a phone.

## Why it costs

The one control a phone user needs is the one that breaks: the box they type in. The ready counter becomes unreadable at exactly the moment it matters, since coordination on a phone is the reason to look at it.

## Repro

Open the WebUI on a phone, or shrink a desktop browser to a phone width. The tools row overflows and the readiness labels overlap the counter.

## Witnesses

- 2026-10-29 - this session - reported by the user on a phone and in a narrow desktop window.

## What I did instead

Rotate to landscape or zoom out.

## Proposed fix

- let the tools row wrap, and give the message box a minimum width;
- below a width breakpoint, collapse the readiness group to the counter alone and move the two checkboxes into a popover or a sheet;
- keep the counter on one line and out of the labels' flow;
- check the row at a narrow phone width as part of any change to it.

## Unverified

Whether the desktop breakage has the same cause as the mobile one, which is the assumption above; only the phone case was observed directly by the reporter, and the narrow window was described as "broken too, apparently".
