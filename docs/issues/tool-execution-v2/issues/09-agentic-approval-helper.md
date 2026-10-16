# Agentic approval helper

Status: resolved
Type: grilling
Blocked by: 08

## Question

An agentic helper that lives inside the approval interface and can advise on, or decide, a tool call on the user's behalf. Three modes: manual (the user decides, and the panel offers a "consult" button and a "confirm through the helper" button), assistant (it starts on its own, fills notes as the user, hints the user, and highlights a recommended button), and worker (it confirms or rejects outright). This ticket settles the modes and their transitions; the configurable lead delay before it starts and settle delay before an automatic decision; the extra prompt given to the helper; the "not enough context" outcome when it cannot judge; the settings model (shared, plus per-tool overrides); and the two hosting forms — implicit inside the tool-call card, and explicit inside a tool-provided interface. The helper is not a step of the execution pipeline; it lives inside the approval interface only. This is feature goal five.

## Answer

The helper lives **inside `ToolConsentPanel`** — the universal confirmation shell that appears everywhere (chat and agent tasks) and hosts a tool's custom approval content. It is **not a pipeline step**: it runs only inside the approval interface, invoked by the panel.

### Mode and settings

Mode is a **setting**, not an automatic transition: `manual` / `assistant` / `worker`. It is configured **per-tool through the descriptor plus the `ToolChange` override path** — exactly like the other tool metadata (`ApprovalLevel`, `PolicyMask`, …) — with a shared default from settings. The same place holds `LeadDelay`, `SettleDelay` and `ExtraPrompt`.

- `LeadDelay` — how long before the helper starts on its own (`assistant` / `worker`).
- `SettleDelay` — how long after the verdict before an automatic decision is applied (`worker`), giving the user a last chance to intervene.

### Context

The helper's prompt is built from the tool descriptor (name / description / argument schema), the **effective arguments**, the **effective decision and its `ToolPolicyReason`**, the **preview artifact**, the **recent messages** (rendered through `MessageQuoteRenderer`), and the configured `ExtraPrompt`. The model is configurable.

### Output — a structured verdict

```jsonc
{
  "verdict": "approve" | "disallow" | "not_enough_context",
  "notes_for_user":  "…",       // rendered in the panel
  "notes_for_agent": "…",       // travels into the tool result (the model sees it)
  "requested_data":  { … },     // tool-specific, per the tool's declared schema (e.g. enabled hunks)
  "override_agent_intent": true,// true → the edit goes to call.Arguments (persisted); false → ArgumentsOverride
  "new_arguments":   { … }
}
```

- `not_enough_context` is a first-class verdict: **even `worker` does not decide** and escalates to the user.
- The two notes are split — the user and the model want different messages.
- `requested_data` is the "semantic actions" channel and requires the tool's approval provider to expose the **schema** of the data it wants filled.
- `override_agent_intent` is the switch between ticket 07's two argument channels (`call.Arguments` vs `ArgumentsOverride`); the helper sets it, while the surface's toggles decide which channels exist at all.
- `new_arguments` and `requested_data` are **mutually exclusive per tool** — two contradictory edit channels on one tool would leave the tool unsure which to apply; the approval surface declares one.

### Cross-ticket follow-ups

- `IToolApprovalProvider` (ticket 08) must expose the **schema** of `requested_data`.
- `IToolDescriptor` / `ToolChange` gain the helper-config properties through the normal override path.
- The recent-messages context uses `MessageQuoteRenderer` — a prompt-layer concern (the helper is in scope; that rendering is noted on the out-of-scope side).

## Comments

<!-- appended conversation -->
