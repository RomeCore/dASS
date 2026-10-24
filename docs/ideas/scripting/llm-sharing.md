# Run launched processes on the configured model

Status: open
Type: task
Blocked by:

## Requested

- A launched process receives the model configured in the application through environment variables, so a script or
  a foreign CLI runs on the same model as the chat instead of one hardcoded inside it.
- The proposed namespace carries the provider kind, the endpoint, the key and the model name — one value each, chosen
  per launched process. Provider kinds named: OpenAI, Ollama, Anthropic, Google.
- Claude Code is the motivating consumer: it has to receive the model before it starts, which means the launching
  script edits the environment itself.
- Open question named by the user: which other places would use this, and how much value the feature carries overall.

## Proposed design

- **The API dialect is the contract, not the provider kind.** Claude Code speaks the Messages API only, so a provider
  configured as OpenAI-compatible cannot be handed to it by setting a provider-kind variable; a translating endpoint
  is required in between. Provider kind stays descriptive metadata.
- **A canonical namespace serves scripts written in-house; per-tool adapters serve foreign CLIs.** Variable names
  differ per tool and drift between versions, so the mapping belongs to an adapter selected by a frontmatter field on
  the launching addon — not to the canonical set, and not to the body of every script.
- **A loopback gateway is the stronger form of the same feature.** The child receives a local base URL and a synthetic
  key; the real key never leaves the key store; per-process usage, limits and request logging become possible, and one
  translation layer covers every tool speaking the same dialect. Environment becomes transport rather than credential.
- **Every out-of-process addon kind is a consumer, not only slash commands.** A tool that summarises, a prompt context
  that reads a diff and a command that drives a foreign CLI read the same variables.
- **Injection is opt-in per addon and off by default.** A child inherits what it is given and passes it to its own
  children, and a transcript captured from an interactive terminal can carry an echoed command line into
  model-facing text.
- **Model-backed addons the model can invoke need a policy, not just a flag.** A tool the model may call in a loop,
  whose body spends tokens, multiplies cost without a user request; user-invoked commands carry no such risk.
- **The injection scope is settled before implementation.** A per-agent model makes launching depend on the calling
  agent, which the interactive terminal API does not know about; choosing between application, chat and agent scope
  changes the spawn signatures.
- Accepted as requested: environment variables as the transport for the simple case, the four provider kinds, and
  foreign CLIs as consumers.

## Alternatives considered

- **Exporting the variables application-wide** — refused: every spawned process inherits them, unrelated scripts and
  their children included.
- **Rewriting the foreign tool's own configuration file before launch** — refused for the reason the feature exists:
  it mutates state outside the application and outlives the launch.
- **Passing the real provider key in the environment** — accepted only for a first version, and only where no gateway
  exists yet.

## Not in scope

- Translation between dialects, tool-call and streaming normalisation, caching: the gateway's own effort and the
  largest part of it.
- Usage accounting and a quota interface.
- Changes to where keys are stored; the existing key store stays the authority.
- Sandboxing or resource limits for the launched process.

## Comments

- External-process engines already own process lifetime, environment handling and working-directory resolution, so the
  injection point exists without new plumbing.
- Out-of-process slash commands have no chat API: everything they report they report through their result, which makes
  an injected model their only route to one — for that kind the feature is a capability, not a convenience.
- Three tiers of cost behind the same idea: canonical variables plus adapters (days), the same plus a loopback gateway
  (weeks), and the gateway alone as protocol translation (a separate effort with its own spec).
