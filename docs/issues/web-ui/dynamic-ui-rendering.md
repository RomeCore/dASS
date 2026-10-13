# Render and drive dynamic widget UI in the WebUI

Status: ready-for-agent
Type: task
Blocked by:

## Requested

- The desktop dynamic XAML UI and the way the Lua API applies it (`dass.ui.create_control` plus
  `dass.tool.result.append_data`, the renderer behind them, and the path the widget takes to the message) are the
  starting point.
- The WebUI cannot render Avalonia markup, so a way to present dynamic widget UI in a browser is required.
- Preferred shape: an author-supplied pair of presentations — XAML and HTML, each one optional — where the HTML half
  is filled with data by a template engine (Handlebars-like): lists, text and other structures.
- The pair is expected to be cheap for the authors, because the addons that ship this UI are usually written by
  agents.
- Where an addon ships no HTML half, an agent converts the XAML at runtime, with a cache.
- Two-way bindings — buttons, fields, checkboxes — are named as the open problem.

## Proposed design

The pair is accepted as the authoring model. Corrections and additions:

- **The axis is `data + actions + N presentations`, not `XAML ↔ HTML`.** A widget persists its markup (`Axaml`) and
  its view model (`Data`, a BSON snapshot); Lua functions are dropped by the tolerant conversion. A widget is already
  data-persistent and behaviour-session-only on the desktop: after a reload its buttons render disabled. The persisted
  data is therefore already the exact payload a template engine wants.
- **The WebUI is not a separate data path and needs no RPC layer.** The web host is Blazor Server started from a
  chat-scoped service that lifts the chat's own service instances into the ASP.NET host as singletons, and the chat
  page injects the same `Chat` object. Components run in-process with the Lua interpreter and with the live view
  models on the tool calls, so a click arriving over the circuit can reach the live widget directly.
- **The floor is deterministic: a generic view built from the data tree** — members as label/value rows, containers
  nested, arrays as lists, commands as disabled buttons, dictionary-versus-array distinguished. Third-party addon
  packs discovered from other runtimes' home folders will never ship an HTML half. The template is the ceiling, the
  LLM conversion is sugar, and the generic view is the floor that never depends on a model call.
- **A template is an initial render, not a reactive engine.** Render once, then update only path-addressed nodes.
  Re-rendering the template string on every change loses the caret, drops focus and starts a write/echo cycle with Lua
  `__changed` handlers. A full re-render is reserved for structural change, with focus and caret restoration.
- **Two-way binding is one funnel.** A single chat-scoped interaction service walks the path (nested members and
  array elements included), writes the member and forces a top-level notification, so the desktop view refreshes and
  the snapshot is persisted. Values are coerced by the existing member's type.
- **One marker contract of four attributes:** `data-dass-bind` (reflects a member, and writes it when placed on a
  field), `data-dass-on` (commit on `change` by default, `input` opt-in), `data-dass-action`, `data-dass-payload`.
  Array paths are 0-based, matching the dynamic view model's indexer.
- **Buttons dispatch by member name into the existing asynchronous command surface.** The Lua idiom reads `self`, so a
  payload is optional, and the two-way problem reduces to getting the field values into the view model before the
  click.
- **Unavailable actions need no special machinery:** after a reload the member is not a command, so the desktop's
  `{Binding refresh}` and the template's `{{#unless refresh}}disabled{{/unless}}` degrade from the same rule.
- **The desktop keeps its direct command binding.** The desktop user is the application owner and the shared
  null-command rule already yields parity on the disabled state; routing desktop clicks through the interaction
  service would add a second dispatch path for no behavioural gain. Revisit if the desktop ever needs an
  authorization or policy hook on widget actions.
- **Transport:** JS delegation attached to the container Blazor owns, so listeners survive re-renders, plus surgical
  DOM patches by path. Not a DOM-to-Blazor-render-tree translation.
- **The converter is fed the view model's schema**, not the markup alone: member names with their kinds (scalar,
  container, array, command). Its output is validated against that schema, cached by markup hash plus schema hash plus
  prompt version, falls back to the generic view, and stays off the render path.
- **Security is part of the work, not a follow-up:** rendered HTML is sanitised (script-ish tags, `on*` attributes,
  `javascript:` URLs, raw interpolation), markers never come from interpolated data, and every write and action is
  scoped to the calling chat.

## Prerequisite

The WebUI currently renders no additional data at all — not the widget, not attachment chips, not token costs. The
per-type web view routing (the web analogue of the desktop's view-model to view mapping) has to exist before any of
the above has somewhere to render.

## Not in scope

- Translating XAML into HTML. The feature permits arbitrary Avalonia markup, which no HTML-side representation can
  match, so the two halves stay authored independently.
- A neutral widget vocabulary compiled to both front ends; only worth it if the web becomes a first-class front end.
- Per-user widget state: several web clients on one chat share one view model, last write wins.

## Comments
