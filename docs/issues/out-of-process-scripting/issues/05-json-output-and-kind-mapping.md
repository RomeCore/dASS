# JSON output extraction and per-kind result mapping

Status: done
Type: task
Blocked by: 04

## Question

Implement the structured-output channel (spec §7, §8):

- `OutputMode` / `OutputPattern`: whole stdout, or the first `BEGIN(.*?)END` match — non-greedy, DOTALL; `\*`
  is a literal asterisk and the first unescaped `*` splits the pattern;
- a missing marker and unparsable JSON are both `InvalidOutput`, never a text fallback (D8);
- the per-kind mapping: tool (`status.icon` / `title` via `VisualIconKind.TryParse`, `result`, `markdown`,
  `structured`), command (`effect`, `generate`, `error` through `Locale.GetConstKey`, `part` with an explicit
  `content`), context (no JSON at all);
- unknown top-level keys are `InvalidOutput` (D26).

## Answer

Landed on 2026-10-25 inside the adapters and `ExternalProcessScriptEngineBase`:

- `Whole` trims `Stdout` and parses it; `Pattern` splits at the first unescaped `*` (`\*` is a literal asterisk,
  zero or two unescaped asterisks are a fatal frontmatter diagnostic) and takes a non-greedy first match; a missing
  marker and unparsable JSON are both `InvalidOutput`, never text (D8).
- Tool: `status.icon` (unknown icon → warning, no icon) / `status.title`, `result` (default stdout), `markdown`,
  `structured`; without JSON the result is the merged `Output`, on failure `Output + "\n\n" + Error`.
- Command: `effect` → `EffectSummary`, `generate` (the host still resolves it through `SlashCommandIntent`),
  `error` → `Locale.GetConstKey`, `part` → `AdditionalMessageContentPart` with an explicit `content` (raw `Output`
  becomes the part only when there is no JSON at all).
- Context: no JSON channel; `json-output` on a context is a load-time fatal via `ScriptFeaturePolicy`.

**D26 is amended**: unknown top-level keys are ignored instead of `InvalidOutput` — there is no schema to compare
against, and a per-adapter read keeps the contract explicit.

## Comments
