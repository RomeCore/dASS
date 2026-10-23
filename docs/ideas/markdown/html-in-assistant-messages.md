# Render HTML tags inside assistant messages

Status: ready-for-agent
Type: task
Blocked by:

## Requested

- Assistant messages must render the HTML their author emits — `span`, `p` and the like — instead of showing it
  stripped or dropping it.
- The target is the message renderer, not the HTML-to-Markdown conversion used for fetched pages; that path is
  explicitly not what this is about.
- The rendering is asked for as "practically full-fledged": the tags a model writes should mean what they mean,
  within a chat message.
- CSS is doubted as necessary. Structure and inline semantics are what is asked for; a styling engine is not.
- The open question, put by the requester: whether Markdig should be extended from the inside, since it is
  designed to be extended and makes it look straightforward.

## Proposed design

The starting point is a defect, not only a missing feature: Markdig already parses raw HTML into its own
Markdown objects, the renderer has no node for them, and the fallback nodes are empty. An HTML block loses its
whole content; inline tags are stripped and their text survives. Anything built here also closes that hole.

The extension-from-inside direction is accepted, and sharpened into a split at the inline/block boundary.

- **Inline HTML becomes real Markdig nodes.** A custom inline parser takes over the opening character currently
  owned by the stock HTML inline parser, the same in-place swap already used for the alert parser. Tag pairing
  and nesting are then the inline processor's job — the mechanism built for links and emphasis — not a
  hand-rolled stack.
- **Tags with a Markdown equivalent emit the stock node.** Bold, italic, strikethrough, links, code, line
  breaks and thematic breaks are already rendered, already searched, already copied; reusing those objects
  leaves no new UI code and no new projection rules for them.
- **Only tags without an equivalent get a custom container node** — underline, mark, subscript, superscript,
  small, span, abbreviation. One node class on the renderer side, styled by tag name.
- **Block-level HTML stays opaque and is parsed on the renderer side.** The stock block parser deliberately
  hands over raw lines, which is the right model: paragraphs, lists, quotes, headings, preformatted blocks,
  tables and disclosure elements are then a fragment parse into Avalonia controls, using the HTML parser
  already referenced by the project. Replacing the block parser with a nesting-aware container is refused for
  now; it only pays off if blank-line splitting inside a disclosure element becomes intolerable.
- **Unclosed containers are left open, not rolled back.** A container still open at the end of a block keeps its
  children; a tag that does not terminate inside the available input stays literal text. Both rules exist for
  streaming, where every intermediate frame is a valid document.
- **Every hand-built node carries a source span, children included.** The renderer's dirty check compares it and
  the text projection slices by it; a missing span degrades into silent non-updates and empty copies.
- **The parser is testable without a window.** The AST is plain data, so the streaming cases — half-written tag,
  unmatched close, four levels of nesting — are unit tests rather than visual checks.
- **CSS is accepted as out of the picture.** `style` attributes and classes are consumed and ignored, so they
  cannot leak into the text. A whitelist of colour, weight and size would be a later decision, not a part of
  this one.
- **Safety is part of the parser, not a later pass.** Raw-text elements and comments are dropped with their
  content; event attributes and non-http, non-relative URLs are dropped; unknown tags keep their text and lose
  the tag.
- **The known debt is the copy projection.** The library's text projection knows only its own Markdown types, so
  text inside a custom container may be missing from a copied selection; this needs a selection processor on our
  side or a touch upstream. It is the only piece of this design that reaches outside our code.

## Alternatives considered

- **Pre-processing the parsed tree** — rewriting the flat HTML inline sequence into native nodes after parsing.
  Rejected: it performs span surgery on a tree that is rebuilt on every streaming frame, and it still leaves
  every inline-owning node (paragraph, list item, table cell, quote) to be patched individually.
- **A replacement container node for paragraphs** that builds inline content itself. Rejected for the same
  reason in reverse: it concentrates the work in the renderer, loses the source-span accuracy the parser gives
  for free, and must be repeated for every container of inlines.
- **A real HTML engine per message** (a web view or an HTML renderer control). Rejected: per-message cost in a
  stream, no cross-message selection, no search highlighting, and a much wider surface to sanitise.

## Not in scope

- A CSS engine: cascade, stylesheets, computed styles, layout, fonts. The message body is a fragment, and
  geometry is Avalonia's.
- Markdown parsed inside block-level HTML. CommonMark keeps those blocks opaque, and mixing the two grammars is
  a different feature.
- Documents rather than fragments: `html`/`head`/`body`, forms, iframes, embedded content.
- The fetch path and tool results. Fetched pages keep their existing HTML-to-Markdown conversion.
- Images inside HTML are an open detail: the tag can be mapped onto the existing image pipeline, but whether a
  model-supplied `<img>` should be fetched at all is a decision of its own.

## Comments
