# Tool-command source (slash command to tool)

Status: resolved
Type: grilling
Blocked by: 01, 03

## Question

The fifth source of slash commands: a command synthesised from a tool, whose argument schema is generated from the tool's argument schema (including the idea of a designated main argument). This ticket settles the tool-command format — how a tool's arguments become a command's arguments; the seam between the slash-command subsystem and the tool-execution subsystem (which side owns the source and which side owns the run); and how this source is ordered and identified among the existing ones. It is the change that triggered the whole effort, and it graduates the "Tools-v2 argument schema" item that was left in the fog of the slash-commands map.

## Answer

The tool-command source is a **derived slash command**, exactly like skills and sub-agents:

- The **slash-command subsystem owns the source**: a `DerivedSlashCommandProvider<ToolInfo>` synthesises one command per tool (`/tool:<name>`), stamping `SlashCommandSource` and an executor, ordered by `SlashCommandOrderTiers.ForSource` (derived tier). No new slash-command mechanism is introduced.
- The command's **argument schema is generated from the tool's `ArgumentSchema`**, with the notion of a **designated main argument** mapped onto the positional slot / rest that the slash-command argument grammar already supports.
- The **run is owned by the tool-execution subsystem**: the executor launches the tool through the **`Command` orchestrator** (ticket 03) — which, per ticket 13, runs as a user action (policy/confirmation skipped, hard floor honoured).

## Comments

<!-- appended conversation -->
