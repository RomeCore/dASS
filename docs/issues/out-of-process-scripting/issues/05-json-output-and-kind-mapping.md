# JSON output extraction and per-kind result mapping

Status: open
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

## Comments
