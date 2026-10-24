# Out-of-process scripting engines — wayfinder map

`wayfinder:map` (the file name itself is the marker; there is no label system).

## Destination

An implementable, already-decided **spec** for one family of out-of-process script engines covering every
scriptable kind — tools, slash commands, prompt contexts, hooks later — with four languages in the first
release (Python, Bash, Batch, PowerShell), and the three existing Python engines dissolved into it.

The destination spec is [`spec.md`](spec.md). The design was settled in a single grilling session on
2026-10-24; the tickets below are **implementation slices**, not open questions.

## Notes

**Domain / skills to consult**

- `tool-authoring`, `command-authoring`, `context-authoring`, `skill-authoring` — the addon authoring contracts
  that this effort rewrites.
- `AGENTS.md` for the DI, addon and prompt-context conventions; `docs/issues/README.md` for this tracker.

**Standing preferences for this effort**

- **Everything out-of-process lives in the Desktop assembly.** Core keeps only the contracts its own parsers
  and providers touch (see spec §2).
- **No competing agent runtimes are mentioned** anywhere in this effort.
- The approved signatures in spec §4 are fixed: parameters may be **added**, never changed or removed. The
  only additions so far are `ScriptInputMode`, `ScriptOutputMode` and the terminal `Wait` flag.
- A promise in the frontmatter that cannot be honoured is a **fatal addon diagnostic at load**, never a silent
  downgrade at run time.
- Facts about the runtimes (Batch escaping and the 8191-byte line, the PowerShell 5.1 BOM, the launcher's
  default 30-minute timeout and its token linkage) live in the spec and must not be rediscovered by hand.

- **JSON mode reads `Stdout`; raw mode returns `Output`.** The merged stream the launcher already produces is the
  raw result — stderr is not filtered out of a script that has no JSON channel.
- **A handoff never contains a rollback recipe.** A wrong tree is fixed in place; pointing the next session at
  `git reset --hard` is how work gets lost.
- **The prompt-context budget lives at the call site**, in `PromptContextBudget` (`LLM/Services/Prompting/`), not
  in a language base — the pipeline decides how long a context may run, a provider only runs its body.

**Settled premises (grilled with the user)**

- The input channel is a **generated preamble**; there is no input file, environment variable or argv.
- The variable **names belong to the kind**; a language engine only renders `name = value`.
- Structured input is a **capability** of the language, not a promise of the contract.
- `json-output` is the **per-kind result contract** (status, structured result, command part, later hook
  decisions) — not a choice about what text the model reads.
- Scriptable prompt contexts are reshaped to mirror tools and commands, and `PromptContextExecutionContext`
  replaces `EffectiveChatContext` in **every** provider, including anchored sections.

## Decisions so far

<!-- the index: one line per closed ticket, enough to judge relevance, then zoom the link for the detail the ticket holds -->

- The spec is [`spec.md`](spec.md) — all 27 decisions (D1–D27), the per-language preamble facts, the per-kind
  JSON contracts and the prompt-context refactor. The map is **closed**: the effort continues in the tickets
  below, which are implementation slices.
- [Prompt context refactor](issues/07-prompt-context-refactor.md) — the engine is a creator only
  (`CreateProvider`), **the provider is the run**, so the four script providers live next to their engines on one
  base class per language; `PromptContextExecutionContext` is composition (`Context`, `PromptContext`, `Chat`,
  `Services`) and replaced `EffectiveChatContext` in all five provider interfaces; `Kind` is parsed from the
  frontmatter; `EffectiveChatContext.Services` is gone; the helper keeps only the formatting rules. `Descriptors`
  stays with ticket 01. Both projects compile.

## Not yet specified

- The hook decision shape (the JSON a hook handler answers with) — its own effort.
- The per-language runtime settings UI beyond the existing Python venv configuration.
- The JavaScript engine (enters by descriptor, nothing else changes).

## Out of scope

- **Sandboxing, permissions, resource limits** beyond a timeout.
- Long-lived workers, persistent interpreter sessions, REPL-style execution.
- Streaming managed-process stdout into a tool card.
- Scriptable **anchored** prompt contexts (the execution context already accommodates them, the engine does not
  build them yet).
- Migrating Python addons: none exist in the repository or its packs.
