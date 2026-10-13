---
name: rival-recon
description: Study a rival agent runtime or desktop assistant - its code, addon formats, UX, pricing, roadmap or users' complaints - and turn it into findings dASS can act on. Use when the user points at another runtime or a reference repository ("look at how they do it", "what do they have that we don't", "tear down <product>", "find ideas in this repo"), when a feature is argued for by "they have it", or when a plan needs a benchmark against what the neighbours already shipped.
argument-hint: "<target> [mode:$sweep|deep] [notes...:%Anything to steer the recon]"
allowed-tools: [fs-read_entry, fs-grep, fs-glob, web-search, web-fetch, agent-callsub, addon-search]
---

# Rival recon (dASS)

dASS is not the only agent runtime in the room, and it does not have to rediscover everything from scratch. **Rival recon** looks at what the neighbours built - their code, their addon formats, their prompts, their UX, their pricing, their users' curses - and turns the useful parts into findings dASS can act on.

The output is never code. It is a **teardown**: a document that says, per finding, what they have, what we have, what the user loses by the difference, and whether the thing fits dASS at all. Writing the feature is another session, after the user picked.

Two rules hold the whole thing up:

- **A finding without provenance is a rumour.** Every claim carries a `path:line`, a repo+commit, or a URL with the date it was read.
- **Take the idea, leave the artifact.** No pasted code, no lifted marketing copy, no borrowed names.

## Legal scope

Recon means reading what is public and reasoning about it. That is legal, normal engineering work, and it needs no permission from anyone. Everything else is not worth the risk it puts on the project.

| Fine | Off limits |
|---|---|
| Public product pages, docs, changelogs, release notes, roadmaps, pricing | Reverse engineering or decompiling where the licence forbids it |
| Public repositories - reading code, issues, PRs, discussions, commit history | Bypassing auth, paywalls or rate limits to reach the material |
| Other runtimes' own files on this machine (their skills, sub-agents, tool scripts) | Harvesting personal data - names, e-mails, private chats, private repos |
| Public reviews, forums, Discord, Reddit, HN, app stores | Anything leaked, dumped or obtained under NDA |
| Published job ads, status pages, public SDK and API docs | Committing licence-contaminated code into dASS |

**Leaked material is not a source - it is a liability.** A reference checkout whose own name announces it as a dump or a leak is still not a source: it may be read to understand that a concept exists, but it is never cited as the origin of anything and never mined for code. Provenance in a teardown points at public behaviour, not at the dump. If a target only has leaked material behind it, say so and stop.

**Licence gate.** Read the `LICENSE` of any repository before it informs code:

| Licence | What it permits here |
|---|---|
| MIT / Apache-2.0 / BSD | Idea and code, with the attribution the licence asks for |
| GPL / AGPL / LGPL | Idea only. Never a line of code - reimplement clean-room from observed behaviour |
| Proprietary / EULA | Behaviour observed from the outside only - never their code or assets |

**Copy the idea, not the words.** Names, logos, screenshots, UI strings and marketing copy are theirs. A feature is not protectable; its wording is. When a rival's phrasing is good, that is a reason to write our own better one, not to paste theirs into a `.loc` file.

## Sources

### Local sources

This file is committed and runs on machines that are not this one, so the list below is a **map of where evidence usually is, not a guarantee that anything is there**. Check every path before citing it - a listing is enough - and when a source is absent, say so in one line and keep walking. A teardown that cites a directory the reader does not have is worse than one that cites nothing: it reads like evidence and cannot be re-checked.

Local recon has two layers, and the first one always exists:

| Layer | Source | There when |
|---|---|---|
| Always | The repository itself: `AGENTS.md` (structure), `GLOSSARY.md` (vocabulary), `docs/adr/` (decisions made, with their reasons), `docs/issues/` and `docs/ideas/` (what is already planned), `src/` | always |
| If the host has one | Reference checkouts of rivals - other projects the host's working directories point at, at whatever path and folder name that machine uses | the user configured them; the names are `<working directory>` aliases from the host's config, not real folders |

The second layer travels worst: the working-directory list is a per-machine setting, so a checkout that exists on one machine can be missing, renamed or nested one level deeper on the next. **Discover, do not assume** - enumerate the host's working directories and what they actually hold, then cite the real path. Nothing local outside these two is evidence.

Addon packs under `.llmassist/packs/*` are **ours, not theirs** - that is baseline material for step 2, not a rival source.

Reference checkouts are the only place where the answer to "how did they do it" is a file rather than a guess. When none is configured, the repository plus the public sources below still make a complete recon - a shallower one, and the teardown should say so.

### Public

- Product pages, docs (the actual reference docs, not just the landing page), changelogs, release notes, roadmaps, pricing and limits.
- Public repositories: issues, PRs, discussions, commit history. Feature flags, draft PRs and abandoned branches are a roadmap that was never announced.
- Community: their Discord or forum, Reddit, Hacker News, review sites, app-store reviews. **Complaints are the highest-value source in the whole list** - they arrive pre-scored by user cost.
- Job ads: what a company staffs tells where it is going before it says it.
- API/SDK docs and status pages: a schema describes the product better than its marketing.

Every source line carries the date it was read. Rivals move, and a six-month-old teardown is a rumour again.

## The surfaces worth comparing

"Features" is not a unit of comparison. Two desktop assistants differ in a dozen places, and these are the places - the map that turns a vague "tear down *&lt;rival&gt;*" into findings, and the vocabulary the findings get filed under. The `dASS` column is where to look on our side; the last column is where theirs usually is.

| Surface | The question it answers | dASS | Where theirs is |
|---|---|---|---|
| **Memory** | What is remembered, by whom, and when it comes back? Facts vs episodic logs, per-chat vs global, auto-written vs agent-written, user-editable, always injected vs retrieved on demand. | Memory blocks over fact and log stores (`Agents/Memory/`) | Their memory docs and settings; forum threads about "it forgot" |
| **Agents** | How many runtimes run at once, who picks the next one, what hand-off looks like, what runs in parallel. | Chat agents and execution stages, the adaptive router, sub-agents, agentic tasks (`Agents/`) | Their agent and task docs, agent UI, code |
| **Context** | What happens when the window fills: summarise, drop, checkpoint, per-agent visibility, token accounting, prefix stability. | SCM anchors and deltas, `ContextCheckpoint`s, `AgentContextSettings` | Their changelog and issues; complaints about losing the thread |
| **Prompt** | What is assembled on every request, who may edit it, and how much of it the user ever sees. | LLT prompt parts, personas and slots, `AgentPromptComposer` | Their template or prompt files and their editor |
| **Tools** | Which tools ship, which can be scripted, what may run without asking. | Tool modules and Lua-first scriptable tools, the approval policy | Their tool list, permission UI, docs |
| **Models** | Local vs cloud, provider breadth, streaming, reasoning display, cost control. | Capability-based model configuration (`LLM/`) | Pricing pages, provider docs, release notes |
| **Extensibility** | What a third party can add, and how it is distributed and discovered. | Addon kinds, packs, slash commands | Their plugin or skill ecosystem and its docs |
| **The chat surface** | Branching, editing, regeneration, artifacts, search, completion. | Slash commands, input completion, quote rendering, context tabs | Release notes, screenshots, reviews |
| **Files and workspace** | Folder access, indexing, editing, terminal. | Working-directory access, filesystem tools, document reading | Their docs and reviews |
| **Sessions and data** | Where chats live, how they are exported, searched, synced. | Chat storage and settings | Their docs, forum |
| **Trust and safety** | What leaves the machine, what holds secrets, what is sandboxed. | The approval policy and disabled flags | Security pages, incident threads |
| **Pricing and packaging** | Free-tier limits, licensing, distribution channels - and what users say about them. | Our own channels | Pricing page, reviews |

Three rules for using it:

- **Pick the surfaces the question touches - one to three.** Walking all twelve is a survey, and a survey is not a teardown: the value is depth, and depth needs a small number of surfaces.
- **A finding that cannot name its surface is a feature-parity wish in disguise.** If it fits none of the twelve, the question is wrong, not the map.
- **Memory, agents and context are where assistants diverge most and users feel it most.** A bare "tear down *&lt;rival&gt;*" with no question defaults to those three, and that default gets said out loud before the sweep starts.

## Steps

### 1. Fix the target and the question

The arguments the command bound, expanded below - an empty one means the caller did not pass it:

- **Rival to study:** $target
- **Depth:** $mode - empty means `sweep`, a one-pass survey over our own disk plus public sources; `deep` adds verification and scoring of every finding
- **Extra steering:** $notes

One rival, one question: "what does $target do about <area>". Not "tell me everything about $target" - a recon without a question produces a screenshot dump with a table of contents. If no question came with the invocation, propose the highest-value one and get it confirmed before opening a file - naming the surfaces from the map above, never a bare "all of it". If the **Rival to study** line above came out empty, ask which rival before anything else.

*Done when:* the target and the question are in one line, before anything is opened.

### 2. Establish our baseline first

Read what dASS already is, in the area of the question: `AGENTS.md` for the shape, `GLOSSARY.md` for the vocabulary, `docs/adr/` for decisions already made **with their reasons**, `docs/issues/` and `docs/ideas/` for what is planned. Half the ideas a rival inspires are already built here, or already rejected for a reason that is still true.

*Done when:* we can say in one line what dASS does today and which ADR or issue constrains it.

### 3. Sweep and log raw findings

Walk the sources with the question in hand. Per raw finding: **one line of what it is, plus provenance**. No interpretation yet - that is step 5, and mixing the two is how a guess gets laundered into a fact.

Order the sweep cheapest-first: our disk before the web, code before marketing, observed behaviour before claims.

*Done when:* the raw list exists and every line points at something that is actually there.

### 4. Verify before scoring

A landing page is a claim, a running product is behaviour, a repository is code. Verify at the level the finding needs - read the implementation, run the rival and watch, or find a user demonstrating the flow. A finding that cannot be verified is still recorded, **marked unverified**, and is never scored as if it were true.

*Done when:* every finding is verified or explicitly marked unverified, together with what verification would take.

### 5. Score the transplant

One row per verified finding:

| Column | What goes in it |
|---|---|
| **Surface** | which surface from the map above - memory, agents, context, prompt, tools, models, extensibility, chat surface, files, sessions, safety, pricing |
| **Finding** | the thing itself, in one clause |
| **Rival** | Y / N / partial - and since which version |
| **dASS** | Y / N / partial - and where |
| **User cost if we lack it** | what a real user loses today. "Nothing, but it would be neat" is the kill line |
| **Fit** | does it survive dASS's own shape - addons over built-ins, chat-scoped DI, SCM's stable prefix, Lua-first tooling, per-agent prompt composition |
| **Effort** | S / M / L |
| **Verdict** | adopt / adapt / note / reject |

**Feature parity is not a goal; a solved user problem is.** A rival's mechanism imported whole usually fights our architecture - say which part is the *idea* and which part is *their accident of history*. "Adapt" is the common verdict: take the outcome they achieve, get it our own way.

Sort the rows by surface before scoring them: two findings about memory want to be judged against each other, not against a finding about pricing.

*Done when:* every row carries a verdict, and every `adopt`/`adapt` names the user problem it solves.

### 6. Say it in one line

Per surviving finding, one sentence of the form "if we do X, the user gets Y". A finding that cannot produce that sentence is a `note`, not an `adopt` - it may be context for a later decision, and that is a perfectly good outcome.

*Done when:* every survivor has its SO WHAT line, and every finding without one was demoted out loud.

### 7. Write the teardown, and promote nothing by yourself

File it as `docs/rival-recon/YYYY-MM-DD_<rival>_<area>.md` - the rival part and the area are both kebab-cased slugs of what was studied (`Claude Code` → `claude-code`), never raw names with spaces - creating the folder when it is not there yet. Then stop. This skill writes no dASS source code and files no issues. Promotion into the issue tracker (`docs/issues/`) or into an idea (`docs/ideas/<scope>/<idea-slug>.md`) is the user's call - offer it, do not do it.

*Done when:* the document is on disk, the user has the path, and the repository is otherwise untouched.

## The teardown template

```markdown
# Rival recon - <rival> / <area>

- **Question:** <one line>
- **Rival / version:** <name> <version or commit, when known>
- **Read on:** YYYY-MM-DD
- **Mode:** sweep | deep
- **Sources:** <provenance list, one per line>
- **Baseline in dASS:** <what we have today, and the ADR/issue that constrains it>

## Findings

| # | Surface | Finding | Rival | dASS | User cost if we lack it | Fit | Effort | Verdict |
|---|---|---|---|---|---|---|---|---|
| F1 | memory | ... | Y | N | ... | ... | M | adapt |

### F1 - <finding in a clause>

- **Surface:** <one of the twelve>
- **What they do:** the behaviour, described as behaviour - not as their implementation
- **Provenance:** `path:line` / repo@commit / URL (read YYYY-MM-DD)
- **Verified:** how (code read / product run / user report) - or **unverified, needs <what>**
- **What we have:** the dASS side, with a path
- **SO WHAT:** one sentence
- **Licence:** MIT / Apache / GPL / proprietary / n-a - and what that permits

## Not taking, and why

- <finding> - <no user cost | does not fit the addon model | licence | contradicts ADR-0007 | already shipped>

## Open questions

- <what could not be checked from where we stood>
```

## What bites

- **Recon that silently becomes implementation.** The session ends with a document, not with a half-written provider. The user picks first; the patch is a different session.
- **A finding with no provenance.** It cannot be re-checked, and a rival's update turns it into a false statement nobody can trace.
- **Marketing read as behaviour.** "AI-powered" and "seamless" are claims. The app, the schema and the changelog are behaviour.
- **Feature parity as the argument.** "They have it" is not a user problem, and a rival's roadmap is not our backlog.
- **Mistaking a format sample for a feature study.** Whatever addon, prompt or config files a rival happens to have left on this machine describe a *format*. Feature questions are answered by their product, their docs and their users - not by a folder someone installed.
- **Judging their architecture without their constraints.** Their scale, their platform, their team and their release cadence are not ours. Steal the outcome, not the shape.
- **Forgetting the date and version.** A teardown that does not say what it read and when cannot be trusted again in a month.
- **Their strings in our UI.** `.loc` files make pasting trivially easy and it is still plagiarism - write our own wording.
- **Closing the tab too early.** A rival's *absence* of a feature is as useful as its presence: it is where the market gap is.

## Not here

- Writing the feature that a teardown recommends - ordinary work, once the user has picked.
- A dASS tool, prompt or API that contradicts its own contract → `dass-report`.
- Turning a repeated manual ritual into an addon → `equip`.
- Naming a thing we decided to build after the recon → `domain-modeling` and `GLOSSARY.md`, or `llt-prompt-parts` when it is a prompt part.
- Remembering that the recon happened and what it changed → `record-achievement`.
