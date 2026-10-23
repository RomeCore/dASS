# Hook registration: one registry, two registrars

Status: open
Type: task
Blocked by:

## Requested

- Two candidate shapes were weighed. Hooks as addons: any script language, generic enable/disable in the settings interface, but no way to create them in bulk or generate them from another source, and a second manager has to be avoided.
- Hooks registered from Lua scripts: cheap to generate, an importer for another agent runtime's hooks becomes possible, but no interface to switch an individual hook off.
- Both shapes together were left open as a third option.
- An importer that converts another runtime's hook definitions is explicitly wanted.

## Proposed design

- **Both, and it is one dispatcher, not two.** An addon hook and a runtime-registered hook produce the same registration record and are resolved, ordered and invoked by the same service. The registration source is a property of the record, never a branch in the dispatcher.
- **Addons are the declaration layer.** A hook becomes a new addon kind with its own folder in a pack, one file per hook, and frontmatter carrying the event, a matcher, priority, whether it blocks, its timeout and its error policy. Everything the other kinds already have comes for free: frozen definitions, diagnostics, a card, enable/disable, pack distribution.
- **Lua is the dynamic layer.** A registration call takes an event, an options table and a callback, and returns a handle. It produces exactly the fields the frontmatter declares, so the importer is a choice of output format rather than a second mechanism.
- **Runtime hooks are visible, read-only.** The Lua registrar publishes its registrations to the registry, and the settings interface lists them with their source script, event and matcher, with an enable/disable switch per hook and one global gate for the runtime source. That removes the only stated disadvantage of the Lua shape without inventing a second manager.
- **A hook is not an action.** It is never callable by the model and never appears as a slash command. At most it appears in a read-only catalogue the model can read, the same lane as slash commands.
- **Blocking and observing are different calls, not a flag inside one.** Observers are fire-and-forget and must not be awaited on the critical path; blockers are awaited, ordered and bounded by a timeout.
- **Execution is serialized per chat.** Tool calls run in parallel while a chat shares one script state; two blocking hooks entering that state at once is a re-entrancy fault, so the dispatcher admits one hook at a time per chat.
- **A hook cannot recurse into itself.** A blocking hook that calls a tool would re-enter the hook through that tool's event; the context carries a depth and blocking registrations are refused below the first level.
- **Failure policy is declared per hook.** Fail-open by default, fail-closed only where the hook author asks for it; a timeout, a crash and a malformed return are the same class of failure.
- **Matchers are globs over identity, not predicates.** Tool name, agent name, command name and invocation source; a hook that needs logic puts it in its body and returns "no opinion" cheaply.
- **An addon hook and a runtime hook differ in lifetime and nothing else.** A runtime hook belongs to the chat that registered it, disappears with that chat and cannot be exported. An addon hook belongs to a pack and survives.

## Alternatives considered

- **Addons only.** Refused: generating hundreds of hooks from another runtime's definition file would mean writing files into a pack, which turns a conversion into a migration of the user's addon folder.
- **Lua only.** Refused: it makes hooks a single-language feature, contradicts the multi-language engine family being built for the other scriptable kinds, and forces a bespoke management interface.
- **A dedicated hook manager service per source.** Refused: two ordering rules, two timeout policies and two failure policies, and the seams would have to ask which one to consult.

## Not in scope

- The event set, its placement and its return semantics.
- The out-of-process engine family that will execute non-Lua hook handlers.
- A bespoke interface for addon hooks beyond the generic addon card.

## Comments

- The reference runtime's event names map unevenly: its session events correspond to a chat scope here, its stop event corresponds to an agent stop, and it has no equivalent of agent selection or request assembly, because it runs one agent per process.
- The Lua script kind already claims hook registration in its own description, so the registrar fits an existing promise rather than adding a new one.
