# Out-of-process scripting engines for every scriptable kind

Status: open
Type: task
Blocked by:

## Requested

- A family of out-of-process script engines rather than a single Python special case.
- First languages: Bash, Batch and PowerShell; further languages are added by the same pattern.
- The family applies to every scriptable kind: tools, slash commands, prompt contexts and, later, hook handlers.
- Unified invocation contract: input is either a language-specific structured object or raw JSON, chosen by a frontmatter parameter; output is either a raw string or structured JSON.
- A non-zero exit code is an error, and its message is the process's standard error.

## Proposed design

- **One envelope, not a per-language dialect.** Structured input is a single object carrying the invocation identity, the arguments, the working directory and whatever context the kind provides, plus an optional language-specific payload. A shell script, a Python script and a future hook handler read the same keys.
- **The frontmatter chooses the modes, never the language.** Two parameters — input mode (structured or raw) and output mode (text or structured) — while the extension still selects the engine. This keeps one contract and still lets a trivial command remain a two-line script.
- **Standard output is the only result channel.** Standard error is read only when the exit code is non-zero; on success it is logged and never reaches model-facing text.
- **A non-zero exit with empty standard error still has a message.** It falls back to the exit code and a truncated command line, so an error is never blank.
- **A promised structured output that fails to parse is an error, not text.** Silently degrading malformed output into text hides the defect inside the script.
- **Engines declare whether they suit interactive kinds.** A hook and a prompt context must answer within their timeout; a tool may stream. The capability belongs in the engine descriptor so callers do not guess.
- **Cancellation kills the process tree.** A timed-out or cancelled invocation must not leave the interpreter and its children running.
- **Engines live in the platform assembly, as the Python engine already does.** Bash and PowerShell are platform-bound; registering them from the desktop assembly keeps process execution out of the core and keeps mobile builds honest.
- **The hook contract shapes the envelope now.** A hook answers with a decision, not with text, so structured output must be able to carry a decision object from the first version; retrofitting it later changes every engine descriptor.
- **Bash, Batch and PowerShell are the proof, not the limit.** JavaScript or any interpreter the user already has enters through the same descriptor without touching parsers or locators.

## Not in scope

- Sandboxing, permission models and resource limits beyond a timeout.
- Long-lived workers, persistent interpreter sessions and REPL-style execution.
- Streaming standard output into the tool card while the process runs.
- Hook events themselves; this ticket supplies the engine family they will use.

## Comments

- The script-language enumeration already carries Bash, Batch, PowerShell and JavaScript, so no new vocabulary is required for the first three.
- The existing Python engine is the working reference for the process lifetime, environment handling and working-directory resolution.
