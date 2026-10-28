# AR-0030 - The web client renders a different markdown dialect than the desktop, so assistant syntax degrades to plain text

- **Status:** open
- **Severity:** friction
- **Lane:** user
- **Surface:** UI
- **First seen:** 2026-10-29

## What I expected

One message renders the same in both clients, because the model writes one dialect. Quick-action buttons are buttons, inline term explanations reveal their definition on hover, GFM alerts appear as coloured blocks with their custom titles, and tables, task lists and formulas work.

## What happened

In the WebUI the same syntax degrades: quick actions appear as ordinary links, term explanations appear as literal text with their definitions inline, alerts appear as plain block quotes with the marker visible, and formulas stay as raw delimiters. The content is readable but the affordances are gone.

Traced: the web component runs a stock Markdig pipeline with the advanced extensions only. The desktop pipeline registers additional dASS parsers and nodes on top of it - quick actions, quick explanations, an extended alert parser that supports custom titles and colours, and an extended code block that owns preview tabs and actions. None of that reaches the web renderer.

## Why it costs

The prompt can tell an agent to write a pressable action, a hover definition or a coloured warning, and the instruction becomes noise in one of the two clients. A reviewer on the web has to parse the syntax in their head and work around a control that is not there, and the two clients silently disagree about what a message is.

## Repro

Render one assistant message that contains a quick action, an inline term explanation and a GFM alert. Open it in the desktop client and in the WebUI and compare.

## Witnesses

- 2026-10-29 - this session - reported by the user after comparing both clients.

## What I did instead

Read the raw syntax and ignore the broken affordances.

## Proposed fix

Share one pipeline definition instead of two:

- the parsers are already Markdig-based on the desktop, so the web needs the same parser set, not a copy of the logic;
- add HTML renderers only for the nodes the web can express; where a desktop node has no web counterpart, fall back to a readable rendering rather than to the raw syntax;
- keep the CSS contracts of alerts and term explanations aligned, so one stylesheet change does not make the clients diverge again;
- test the dialect with one fixture message rendered in both clients.

## Unverified

Which desktop nodes have no HTML equivalent (the interactive code-block preview is the obvious candidate), and whether formulas are expected to render on the web at all or are deliberately left as text.
