# Give chat-level additional data its own surfaces

Status: open
Type: task
Blocked by:

## Requested

- Chat-level additional data is rendered nowhere today; it needs presentation surfaces.
- Placement catalogue:
  - **Bottom toolbar**: a horizontal list under the input, two-sided (items belong to a left or a right group),
    placed above the native toolbar so the two never mix; item view models stay visible at all times and are
    separated by separators.
  - **Middle toolbar**: the same, above the input, shown above the attachment chips.
  - **Middle dock**: a vertical list above the input up to the separator; every item gets a tab of the kind used by
    message editing and the follow-up queue, attached to the separator between input and chat; two presentations -
    one at a time (selection switches) or several at once (tabs toggle, items stack) - possibly switched by an extra
    tab-like control; a tab is closable only if the item allows it. The usefulness is acknowledged as doubtful.
  - **Overlay**: the item receives the whole space above the input separator, so it sits over the message sequence;
    optionally draggable (a border slightly larger than the control appears on hover, with a corner thumb to drag),
    optionally closable (flag-controlled) and collapsible; all three controls attach to that border.
  - **HUD**: not pinned to one corner - the script author picks one of eight screen positions (corners and edges).
    The placement mechanics are uncertain, especially several items in one slot and slot conflicts.
  - **Top tabs**: an overlay in the top-left of the messages area - a horizontal collapsible tab list; selecting a
    tab shows the item below it, a second click deselects, close is optional; the panel collapses through a small
    button attached to the left edge of the messages area.
  - **Side tabs**: the same on the right with vertical tabs; the agent-tasks button is planned to move there.
  - **Side panel**: splits the chat as a whole, possibly including the input; a splitter between panel and chat
    resizes it; placed on the left; items stack vertically under a scroll viewer; expected to be the default.
  - **Chips**: not inside the chat but in the chat list, per entry, under the title and topic, as extra chat tags;
    optionally also on the chat tab.
- The catalogue may be collapsed: the three tab variants into one, the toolbars likewise.
- Model and view are separated by wrapper view models (an overlay wrapper carries the additional data instance, a
  title, a close handler and draggable/closeable flags).
- Identity is an optional `Key`: adding an item whose key is already present replaces the previous instance.
- Additional data is a universal store for data, display and UI state, to be used as broadly as possible;
  presentation state may live there.
- A wrapper that has to be persisted derives from the additional data type, not from a plain view model base.
- The side panel is the cheapest variant and is the fallback; it is built first.
- Side panel visibility condition: at least one visible additional data item that does not descend from the new
  item base type.
- Filtering goes through the filtered observable collection.
- Layout is a grid with a splitter; the grid length is bound; the setter lives on the side panel view model and not
  on the splitter, whose length is read-only. On becoming invisible the width resets to zero while the previous
  width is kept and restored when content returns.
- The splitter column length is bound as well, otherwise the splitter stays visible and draggable while the panel
  is hidden.
- Future chat view models go under the additional-data chat folder; the side panel's logic and display stay with
  the chat view and its view model.
- Subscriptions are granular: subscribe and unsubscribe from the old and new items of the filtered view, skipping
  moves.

## Proposed design

- **The side panel is the fallback, and the marker base type is how an item opts out of it.** An item that owns a
  surface descends from the base type and never reaches the panel, so a new surface does not require editing the
  panel's predicate.
- **Layout belongs to the host, not to the script author.** For the HUD this replaces eight anchors with an edge
  plus an order; the host stacks items on that edge and resolves collisions itself. A script cannot see its
  neighbours, so a slot it picks is a collision it cannot avoid.
- **Overlay chrome is always present but quiet**, and dragging uses the whole header. A frame that appears on hover
  is unusable without a pointer, undiscoverable, and flickers while the pointer travels across the item; a corner
  thumb is too small a target for an action performed repeatedly.
- **One always-visible rail above the input replaces the bottom toolbar, the middle toolbar and the middle dock.**
  A second permanent row costs vertical space even while empty, and the dock duplicates what the side panel and the
  overlay already do. The rail keeps the two-sided grouping.
- **The three tab variants are one host with parameters** - anchor, orientation, multi-open - rather than three
  components. Multi-open is a property of the container, not of the item.
- **The side panel is not on by default and splits only the messages area**, keeping the input at full width; the
  input is the primary control and the panel should not narrow it.
- **Close hides, it does not detach.** A closed item needs a visible home it can be restored from, and detachment
  is a separate explicit action; otherwise closing loses the item until the chat is reopened.
- **Closed state is persisted per key**, so a widget the user closed does not return on the next chat open.
- **One wrapper family, not one class per surface.** Surface-specific behaviour lives in subclasses; identity,
  title and the close flag live once.
- **The panel's subscription follows the filtered view's membership, not the source collection.** Members entering
  the view are subscribed, members leaving are unsubscribed, moves need no handling because the filtered view
  reports single additions and removals only. Visibility state is recomputed from the same membership changes
  rather than at construction.
- **Presentation state was proposed to live outside the additional data; that was refused.** Additional data is the
  universal store and layout state belongs there.

## Not in scope

- A general window manager: docking groups, snapping between overlays, tab-through of floating items and shared
  chrome. The surfaces stay independent widgets.
- Moving existing message-level data into the new surfaces; the message stream keeps rendering its own items.
- Per-user surface state; one chat instance means one layout.

## Comments

- Open: who chooses the surface - the item's own default, the script request, or the user by moving the item.
- Open: whether one item may live on two surfaces at once. The current assumption is no.
- Open: whether the agent-tasks entry keeps its flyout when it moves into the side tabs, becoming a panel instead.
- The side panel slice exists in the working tree; nothing else from the catalogue is implemented.
