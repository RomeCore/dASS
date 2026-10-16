# Synchronization model

Status: resolved
Type: grilling
Blocked by: 02

## Question

Synchronization today reduces to one named group per tool, backed by a single lock. This ticket designs the richer model: what keys an invocation emits — ordinary keys plus file-system keys that distinguish files from directories — who emits them (declared statically on the tool, or derived per invocation from the arguments), how the acquire/release scope wraps the inner steps, and how keys are ordered so that tools launched concurrently cannot deadlock each other.

## Answer

Sync is a **keyed higher-order scope** (its bounds relative to the stages are settled in ticket 03). A tool emits a **set of keys**:

- **Static keys** declared on the descriptor (tool-wide locks, e.g. `filesystem`), **plus per-invocation keys derived from the arguments**.
- Derivation and path-resolution belong to a **dedicated capability — the sync-analyzer** — a fifth executor type on the descriptor (`IToolSynchronizationAnalyzer`, `args → keys`), which is the one responsible for converting paths to **absolute**. The synchronizer does **not** resolve paths itself.
- Keys are **typed** — `File` / `Directory` / `Custom` — and **hierarchical**: a held `Directory` key conflicts with any `File` key beneath it (prefix intersection), not only with an exactly-equal key.

The synchronizer **canonicalizes** the analyzer's keys (normalization; collapsing redundant keys — a file under an already-requested directory collapses away). Acquisition is **atomic — the whole key set at once**, so there is no hold-and-wait and therefore no deadlock by construction; global ordering is not relied on for correctness. Conflict detection is prefix-aware: a claim is granted only if no already-held key overlaps (equals, is a prefix of, or is a descendant of) any requested key.

## Comments

<!-- appended conversation -->
