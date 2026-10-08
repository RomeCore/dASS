# 20: Disabled-message toggle UI

Status: resolved
Type: task
Blocked by: 19

## What to build

The UI affordance for the disabled-message flag: an eye toggle at the bottom of every message and a dimmed message
while it is hidden from agents.

- A `ToggleButton` in the message action row, placed after "Delete" and before "Render Markdown", bound two-way to the
  flag. Icon `Eye` when enabled, `EyeOff` when disabled.
- The whole message is dimmed (opacity ≈ 0.75) while hidden.
- Both message views (`UserMessageView`, `AssistantMessageView`) get the toggle and the dim.
- Desktop only (spec: Blazor WebUI is a v1 non-goal); the flag and the visibility rule are core.

## Acceptance criteria

- [x] `MessageViewModelBase` exposes `IsDisabledForAgents` (two-way, writes through to the message) and
      `ContentOpacity` (`0.75` / `1.0`), refreshed when the message changes.
- [x] `UserMessageView` and `AssistantMessageView` bind `Opacity="{Binding ContentOpacity}"` on the root and carry the
      eye `ToggleButton` immediately before the render-markdown toggle.
- [x] The icon switches `Eye` → `EyeOff` when disabled (no new converter).
- [x] Localization key `message.disable_for_agents` added to `iv` and `ru-RU`.
- [x] The solution builds (main + desktop); the (filtered) test suite stays green.

## Answer

- **`MessageViewModelBase`** proxies the flag: the setter writes `Message.IsDisabledForAgents` and raises
  `ContentOpacity`; the message's own `PropertyChanged` (from any view) re-syncs the VM. `ContentOpacity` is a plain
  computed property (`0.75` when disabled), so no converter is needed.
- **Views.** The eye toggle sits between the branch selector and the render-markdown toggle in both views; a `Panel`
  with two `VisualIcon`s gated by `{Binding !IsDisabledForAgents}` / `{Binding IsDisabledForAgents}` swaps `Eye`/`EyeOff`.
  The root `StackPanel` of each view binds `Opacity="{Binding ContentOpacity}"`.
- **Localization.** `message.disable_for_agents` — "Disable for agents" / "Отключить для агентов".

Builds: main + desktop green. Localization set: 39 passed.

## Comments

- Out of the original plan was the SCM decoupling (ticket 19) this ultimately required; the plan's Stage 4 listed only
  the flag and the UI.
