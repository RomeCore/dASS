# Localization key rules

This document defines how localization keys are named in dASS.
Keys live in `.loc` files grouped by domain (one file per domain, `%namespace: <domain>`).

## Key format

```
domain.screen.section.entity.attribute
```

- All segments are **lowercase**.
- Segments are separated by a **dot** `.`.
- Inside a segment use **snake_case**: letters, digits and `_` only.
- **Hyphens are forbidden** in segments — with one exception: the last segment of
  `tool.name.<tool-name>` / `tool.description.<tool-name>` / `tool.status.<tool-name>.*`
  is the tool id from code and keeps its hyphens/underscores as-is.
- Segments must not be C# keywords (keys may become generated code later).
- Use **singular** entity names: `settings.memory.block`, not `blocks`.
- The last segment is the **attribute**: `title`, `hint`, `placeholder`, etc.

## Domains (first segment)

| Domain | Scope |
|---|---|
| `common` | Shared strings reused across the app: `common.ok`, `common.cancel`, `common.save`, `common.add`, `common.delete`, `common.edit`, `common.reset`, `common.search`, `common.name`, `common.default` |
| `settings` | Settings UI screens and dialogs (chat settings, api keys, providers, stages, ...) |
| `chat` | Chat itself: statuses, toasts, chat list, naming, summarization |
| `message` | Messages: actions, branches, visibility, token cost |
| `agent` | Agents: info, read permissions, exposure modes, memory blocks |
| `prompt` | Prompt manager and prompt settings |
| `memory` | Memory blocks, facts, logs, attachment modes |
| `model` | Models and providers: modalities, capabilities, selector, provider names |
| `db` | Database connections |
| `env` | Environment: working directories, python environments, access rules |
| `attachment` | Attachments manager |
| `skill` | Skills: diagnostics, injection modes |
| `task` | Agent tasks: details, statuses, tool calls |
| `command` | Slash commands: argument errors (`command.error.*`) and argument names (`command.argument.*`) |
| `tool` | Tools: `tool.name.*`, `tool.description.*`, `tool.category.*`, `tool.status.*`, `tool.behaviour.*`, `tool.source.*`, `tool.call.status.*`, `tool.fixed.*` |
| `forms` | HITL forms: choice, confirm, input, file picker |
| `webui` | Blazor web UI |
| `mcp` | MCP servers |
| `status` | Reserved for tool execution statuses (prefer `tool.status.*` instead) |

## Attributes (last segment)

Allowed suffixes:

| Suffix | Meaning |
|---|---|
| `title` | Dialog/section title |
| `description` | Longer description (never `desc`) |
| `hint` | Helper text under a control |
| `tooltip` | Tooltip |
| `placeholder` | Input placeholder |
| `label` | Field label |
| `status` | Status line (live, short) |
| `error` | Error message (never `failed` / `error_message`) |
| `success` | Success message (never `done` / `ok`) |
| `confirm` | Confirmation dialog text |
| `count` | Counter with placeholders, e.g. `found: {0}` |
| `result` | Result string |
| `empty` | Empty state ("no results") |
| `none` | "None selected" |
| `all` | "Select all" |
| `action` | Verb/noun for a button: `add`, `edit`, `delete`, `rename`, `clear`, `duplicate`, `refresh`, `save`, `cancel`, `close`, `connect`, `disconnect`, `enable`, `disable` |

Forbidden: `desc`, `done`, `ok`, `btn`, `no_results`, `failed`, `hint_text`.

## Examples

| Old (legacy) | New |
|---|---|
| `settings_apikey_name` | `settings.api.name.label` |
| `settings_apikey_name_placeholder` | `settings.api.name.placeholder` |
| `settings-memory_facts_search_placeholder` | `settings.memory.facts.search.placeholder` |
| `settings-memory_fact_delete_confirm` | `settings.memory.fact.delete.confirm` |
| `settings-chat_model_hint` | `settings.chat.model.chat.hint` |
| `settings-agentic_router_model_hint` | `settings.chat.model.router.hint` |
| `chat_settings_models` | `settings.chat.models` |
| `chat_toast_generation_failed` | `chat.toast.generation_failed.title` |
| `chat_toast_generation_failed_desc` | `chat.toast.generation_failed.description` |
| `task_status_executing` | `task.status.executing` |
| `tool_call_status_pending` | `tool.call.status.pending` |
| `fs-edit_changes_applied_none` | `tool.status.fs-edit.changes_none` |
| `fs-diff_confirm` | `tool.status.fs-diff.confirm` |
| `approval_level_alwaysask` | `settings.tool.approval_level.alwaysask` |
| `tool_behaviour_fileread` | `tool.behaviour.fileread` |
| `skill_diagnostic_missingyaml` | `skill.diagnostic.missingyaml` |
| `model_provider_openai` | `model.provider.openai` |
| `message_visibility_only_users` | `message.visibility.only_users` |
| `webui_login_btn` | `webui.login.button` |
| `settings-memory_clear_done` | `settings.memory.clear.success` |
| `default` / `save` / `add` | `common.default` / `common.save` / `common.add` |

## Formatting

- Values use positional placeholders `{0}`, `{1}` — the key does not encode format
  details (`"Found {0} files"` not `"Found_0_files"`).
- Keys are case-sensitive; the whole key must be unique across all `.loc` files
  of the same locale (the parser throws on duplicates).

## Dynamic keys

Keys built at runtime from enum/flag names follow the same domains and keep the
enum value verbatim in the last segment:

```csharp
var key = $"tool.behaviour.{flag.ToString().ToLower()}";
var key = $"skill.diagnostic.{flag.ToString().ToLower()}";
var key = $"settings.tool.approval_level.{value.ToString().ToLower()}";
```

## Usage in code

- XAML: `Text="{loc:Loc settings.chat.model.chat.hint}"`, keys with quotes for
  hyphenated ids: `ToolTip.Tip="{loc:Loc 'tool.status.fs-diff.confirm'}"`.
- C#: `Locale.Get("...")` / `Locale.Format("...", args)` / `Locale.GetKey("...")`.
- `%namespace: <domain>` in a `.loc` file prefixes every key with `<domain>.`;
  keys are always referenced by their **full** name, never by the bare segment.
