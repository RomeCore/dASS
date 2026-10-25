# Synchronization model

Status: resolved
Type: grilling
Blocked by: 02

## Question

Synchronization today reduces to one named group per tool, backed by a single lock. This ticket designs the richer model: what keys an invocation emits — ordinary keys plus file-system keys that distinguish files from directories — who emits them (declared statically on the tool, or derived per invocation from the arguments), how the acquire/release scope wraps the inner steps, and how keys are ordered so that tools launched concurrently cannot deadlock each other.

## Answer

Sync is a **keyed higher-order scope** over a **FIFO queue**, not one lock per tool. A tool emits a **set of keys**:

- **Static keys** declared on the descriptor (tool-wide claims, e.g. the `AllFiles` claim of a shell), **plus per-invocation keys derived from the arguments**.
- Derivation and path resolution belong to a **dedicated capability — the sync-analyzer** — a fifth executor type on the descriptor (`IToolSynchronizationAnalyzer`, `args → keys`), which is the one responsible for converting paths to **absolute**. The synchronizer does **not** resolve paths itself.
- Keys are **typed** — `File` / `Directory` / `Custom` / `AllFiles` — **hierarchical** (a held `Directory` conflicts with any `File` beneath it, prefix intersection, not only an exactly-equal key) and **access-bearing**: `Read` or `Write`, defaulting to `Write`.

Access is what makes reads free. Three `fs-read_entry` calls on one file run together, the `fs-edit` that follows waits for all three, and a read arriving **after** the edit waits for the edit — an invocation never overtakes a conflicting one that asked first. That ordering is the point: a model that reads, edits and reads again must not see a file mid-write.

The rule behind it: an invocation is granted when nothing held conflicts with its keys **and** no conflicting invocation is already waiting ahead of it. Both checks happen under one mutex, so a request never merely observes a free key set — it claims it.

The synchronizer **canonicalizes** the analyzer's keys: normalization, dedupe, widening `Read` + `Write` of one target to `Write`, collapsing a key covered by a requested directory (a read directory covers only the reads beneath it, so a file write inside survives as its own claim). Acquisition is **atomic — the whole key set at once**, so nothing holds one key while waiting for another, no lock order exists, and no deadlock is possible by construction. A cancelled waiter is dropped from the queue with a re-examination, so it cannot keep blocking the ones behind it.

A queued call can be released by the user through `IToolCall.SynchronizationWait` (a runtime-only `CancellationTokenSource`): cancelling it does not abort the invocation, it runs it **without claiming any key**, and the cancelled source stays installed so the card can mark the run as unsynchronized. There is no timeout — cancelling the run is the only other way out.

## Comments

<!-- appended conversation -->

- The read/write access and the FIFO queue were designed in the follow-up grilling; the queue and the skip surface landed with spec §7 ("Who claims what" there lists the keys each tool family emits).
