# Serve addons under a display name and a model-facing name chosen by style

Status: open
Type: task
Blocked by:

## Requested

- Two independent kinds of renaming are wanted: the name shown in the interface, and the name the model sees.
- Naming styles: an addon carries a dictionary of style to name, so one canonical addon can present itself as
  the same tool under each ecosystem's convention - the canonical `fs-read_entry` also being `Read` in the Claude
  style.
- A naming style is declared globally for an addon type, not per pack, so one choice applies to every addon of
  that type at once.
- A manual rename overrides the style for a single addon.
- Lookup precedence: canonical name, then manual rename, then style name, then aliases.
- Model-facing precedence: manual rename, then style name, then canonical name. Aliases are deliberately excluded
  from model-facing names.
- The open question named: whether the mechanism is worth having at all.

## Proposed design

- **The display half already has a seam and needs only an override.** Display names are a separate localized key that
  falls back to the canonical name, and per-chat addon changes are already a record keyed by addon key. A rename
  belongs there, next to enablement and parameterisation, and costs nothing outside the interface.
- **The model-facing half cannot be a rename of the addon name.** The addon key - the value every deduplication
  group, per-chat change record and per-command override is stored under - is currently derived from the name itself,
  and addons are frozen once collected. Changing the name changes identity. What is needed is a served name, a second
  layer, with the canonical name kept as the key forever.
- **Precedence belongs in the existing claim table, not in a second resolver.** Lookup already resolves a token
  against a list of competing claims with a tie-break: canonical name beats another addon's alias. A style name and a
  manual rename are two more ranks in that same ordering.
- **The claim table must hold every declared name permanently, not only the active one.** A style switch otherwise
  leaves historical tool calls and foreign allowlists pointing at a name nothing resolves. Declared names are known
  from the addon itself, so keeping all of them costs no history tracking.
- **Collisions need a diagnostic, not silent precedence.** Under canonical-first, a manual name that equals another
  addon's canonical name quietly redirects the model to the other addon. The same applies to two addons claiming one
  style name. Both must be reported where the name is edited or the pack is loaded.
- **A missing style name falls back to the canonical name and is reported.** A toolset that is half styled and half
  canonical is worse for the model than one that is uniformly canonical.
- **Styles never apply to tools delivered by an external tool server.** Their names identify the server, and
  rewriting them hides provenance.
- **A style is a property of the agent, fixed before the prompt anchor.** Prompt section bookkeeping is by item name,
  so a rename is announced as a removal plus an addition carrying the full definition - name, description, argument
  schema. In the anchored prompt mode a style change must therefore be an explicit rebaseline, never a silent edit.
- **Served names stay out of code-facing surfaces.** Scripts, hooks and sub-agent invocation keep addressing the
  canonical name; the inverse of excluding aliases from model-facing names.
- **Derived slash commands take the served name while their stable key stays canonical.** Per-command overrides then
  survive a style change, and both the served and the canonical token remain typeable.
- Accepted as requested: two independent layers, a style dictionary carried by the addon, styles declared per addon
  type, manual rename over styles, aliases excluded from model-facing names.
- **The value is concentrated in cross-runtime compatibility.** Addons are collected from other runtimes' folders,
  and their skills and sub-agents list tool names in those runtimes' conventions - names that resolve to nothing
  today. Style-aware lookup is what makes those packs work, not the aesthetics of the toolset.
- **Ranking of the three pieces.** Lookup-only compatibility, then the display override, then model-facing styles.
  The first two remove an existing failure and carry no prompt risk; the third is a hypothesis about model behaviour
  and is worth building only as an agent-level choice that can be compared against a canonical agent on one task.

## Alternatives considered

- **Renaming the addon itself and keeping the old name as an alias** - refused: it rewrites identity, so every stored
  change, override and deduplication group keyed by the old name dangles, and it cannot be undone by switching a
  setting.
- **Exposing all names in the model's tool schema** - refused: several names for one tool enlarge the schema and
  invite the model to invent variants. One served name in the schema, the full set in the catalogue.

## Not in scope

- Renaming addon files on disk or rewriting foreign packs.
- Localizing style names; they are model-facing and never translated.
- Style-aware naming for tools delivered by external tool servers.
- Automatic detection of the inbound pack's convention and an implied style choice.

## Comments

- External references make the compatibility argument concrete: tools imported from another runtime list their own
  tool names in frontmatter, and broken references are already detectable there, so the failure is visible rather
  than theoretical.
- A refusal worth keeping for the second reading: the model-facing layer is not a cosmetic feature. It changes the
  prompt prefix for the affected agent, which is exactly why the style has to be stable per agent rather than
  toggled mid-conversation.
