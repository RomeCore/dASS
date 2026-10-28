# Register addons at runtime from Lua scripts

Status: open
Type: task
Blocked by:

## Requested

- Script addons gain an alternative creation contract: a script registers its addons while running, instead of one
  addon per file on disk.
- A registration returns a handle that revokes it, so the script controls both appearance and removal.
- One script may register any number of tools, slash commands, prompt contexts, and in the future hooks.
- The quantity is dynamic: it follows runtime state, not the number of entries written into a file.
- Scripts execute when a chat is created and re-execute when the script set changes; re-executing only the changed
  scripts is planned for the future.
- The existing practice of generating addon files from a script and letting hot reload pick them up is expected to
  become unnecessary.
- Stated context: the toolset cache is planned for removal in favour of incremental addon-set updates.

## Proposed design

- **Registration is not the difficult part; making a runtime record behave like an addon is.** The collector layer is
  the single seam every consumer goes through, and it already hosts a dynamic-count source, so a registry plugs in as
  one more supplier of additional addons rather than as a new addon kind with a parser, locator and loader.
- **Several addons per file is refused as the answer.** It makes the quantity large but still fixed at load time, and
  it addresses a different complaint. Kept as a separate cheap improvement, because the loading path already stores a
  collection per file; the one-per-file rule lives in the parsers.
- **Two products hide behind the request, and they must be chosen explicitly.** A runtime surface appears in the
  toolset and in the prompt but has no settings entry, no addon card and no persistence. A full runtime addon also
  participates in per-item settings, the addon catalogue and search. The first is cheap and honest; the second is the
  expensive half and should be entered per kind, only where per-item settings are actually needed.
- **Registration data is materialised at registration time, never read lazily from the script state.** The state is
  reset on every refresh, so a record that dereferences it later describes something that no longer exists.
- **A duplicate name is a registration error, not a silent override.** Deduplication otherwise collapses the two
  records with no diagnostic, and the dynamic count stops matching what the script believes it registered.
- **Runtime records are never persisted.** Per-chat settings and consent memorisation are keyed by name and would
  accumulate entries for records that can never appear again.
- **Provenance has to be expressible.** The source vocabulary currently describes files, packs and templates; a record
  that belongs to a chat and has no path is a third case and must say so, or the catalogue and the card surface will
  misrepresent it.
- **The script cleanup contract is the only bound on the count.** A refresh revokes what a script registered before
  re-running it; a revocation handle is ergonomics on top of that contract, not a second mechanism.
- **The refresh path must stop dropping change requests.** It currently refuses to re-enter while a script is running,
  which with a dynamic count leaves the registry describing a set that no longer exists.
- **Prompt cost, not registration cost, is the real constraint.** Additions and removals are announced through the
  section delta machinery. That fits tools, live-tail and supersede contexts, and slash commands, which never enter the
  prompt. It does not fit anchored sections, whose state is captured once per anchor, and a record that appears and
  disappears between requests spends tokens on every change.
- **A registration call must carry behaviour, approval level and synchronization claims, or default to unexpected.**
  Without them a script-registered tool inherits a permissive default and the policy engine approves capability the
  author never declared.
- **Removing the toolset cache becomes a precondition, not an optimisation.** The count has to be recomputed per
  request for the model's tool list to match the registry.
- Accepted as requested: per-chat lifetime, a revocation handle, several registrations per script, hooks as a future
  consumer of the same registry.
- Registering a tool reuses the existing path for building a tool around a delegate, and wrapping a script function as
  a task-scoped ad hoc tool already exists; only the chat-scoped registry is new.

## Alternatives considered

- **Several addons per file as the whole answer** — refused: fixed at load time, so it cannot express a count that
  follows runtime state.
- **Waiting for incremental script updates first** — refused: registration is orthogonal. Incremental update changes
  how often scripts re-run, not where their records live, and registration is what makes the re-run observable.
- **A separate runtime pipeline that bypasses the addon collectors** — refused: it duplicates search, prompt section
  rendering and command resolution, and creates two conflicting notions of what is available to an agent.

## Not in scope

- Anchored prompt sections produced by scripts.
- The incremental addon-set update itself.
- The hook event set and hook semantics.
- Editing or persisting runtime records from the settings interface; a read-only catalogue is the most that is
  implied here.
- Extending the same contract to non-Lua engines.

## Comments

- A dynamic-count supplier is already in use: tools delivered by external tool servers enter through the same
  additional-addon path and change count while the application runs. The feature reuses a proven shape rather than
  introducing one.
- Scripts currently re-run on any addon reload, not only on a script-set change, so registrations must be idempotent
  until incremental updates land.
- The current workaround demonstrates both the demand and its price: a file write and a reload stand between the
  script's intent and the effect.
