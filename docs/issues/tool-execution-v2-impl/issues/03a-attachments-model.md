# P3.5a — Attachments model

Status: resolved
Type: task
Blocked by: 03

## Scope

One write path for everything a tool call or a message carries, and two explicit model-facing branches
that consumers filter by. Replaces `IToolCall.AddAdditionalData`, `ToolAttachment`, `ToolAttachmentKind`
and `AgentAttachmentType`. No compatibility shims: the old members are deleted in this phase.

## Contracts

```csharp
/// <summary>A marker: something addable to a chat object (a message or a tool call).</summary>
public interface IChatAttachment;

/// <summary>A payload the model receives as an attachment.</summary>
public interface INativeChatAttachment : IChatAttachment
{
    IAttachment? Native { get; }
}

/// <summary>A payload the model receives as text appended to the content.</summary>
public interface IContentChatAttachment : IChatAttachment
{
    string Content { get; }
}

public enum AttachmentKind { Image, Audio, Video }
```

- The interfaces live in `LLMDesktopAssistant.LLM.MVVM.Additional`, next to `AdditionalChatData`.
- `AdditionalChatData : IChatAttachment` — every UI payload is addable to a call; it implements neither
  content branch, so model-facing consumers ignore it by construction.
- `ChatNativeAttachment : INativeChatAttachment` — two constructors: `(IAttachment native)` and
  `(AttachmentKind kind, string url)`; `Native` returns the stored attachment or the one built from the
  kind and url.
- `ChatContentAttachment : IContentChatAttachment` — `(string content)`.
- `AttachmentKind` replaces both `ToolAttachmentKind` and `AgentAttachmentType`. `ToolAttachment` and
  `ToolAttachmentKind` are deleted.

## The single write path

`ChatObjectBase.AddAttachment(IChatAttachment attachment)`:

- `AdditionalChatData` → added to `AdditionalData` as is (UI payloads and message parts).
- `INativeChatAttachment` → an invisible `NativeAttachmentMessagePart` (`IsVisible = false`,
  `IsRestorable = false`) wrapping `Native`.
- `IContentChatAttachment` → an invisible `AdditionalMessageContentPart` (`IsVisible = false`,
  `IsRestorable = false`) carrying `Content`.
- anything else → throws: an unknown attachment is a programming error, not a silent drop.

`IToolCall.AddAttachment(IChatAttachment)` delegates to the call's implementation; `IToolCall.AddAdditionalData`
is deleted. `NativeAttachmentMessagePart : INativeChatAttachment` and
`AdditionalMessageContentPart : IContentChatAttachment` so the wrapped parts and direct inputs are
indistinguishable to consumers.

## Task world

- `abstract AgentAttachment : IChatAttachment` keeps only `Source`.
- `AgentNativeAttachment : AgentAttachment, INativeChatAttachment` carries `AttachmentKind Type` and
  `Url`; `Native` builds the matching `IAttachment` (replaces `ConvertAttachmentFromAgent`).
  `FromBase64` moves here.
- `AgentContentAttachment : AgentAttachment, IContentChatAttachment` carries `Content`.
- `AgentToolCall.AddAttachment` branches in this order: `INativeChatAttachment` → `AgentNativeAttachment`,
  `IContentChatAttachment` → `AgentContentAttachment`; anything else (a chat UI payload or an unknown
  payload) is silently ignored — a task has no representation for it.
- Update `AgentToolCall`, `AgentAssistantMessage`, `AgentUserMessage`, `AgentToolCallResult`,
  `AgentToolCallPreResult`, `AgentTaskExecutor`, `ChatAgentTool` and the Lua API
  (`LuaApiAgents`, `LuaApiToolResult`) to the split types.

## Consumers

- `ChatObjectBase.GetNativeAttachments` → `AdditionalData.OfType<INativeChatAttachment>()`.
- `ChatAgentTool.ReadAttachments` → the same filter.
- `MessageContentProjector` and `ChatMessageQuoteRenderer` → `IContentChatAttachment` /
  `INativeChatAttachment` instead of concrete parts.
- `AgentPromptComposer` appends the tool call's content attachments to the tool result (skipped when
  the call is compacted).

## Answer

### What landed

`Utils/Attachments/` (next to the moved `SerializableImageAttachment`) holds `IChatAttachment`, its two
branches `INativeChatAttachment`/`IContentChatAttachment`, `AttachmentKind`, `ChatNativeAttachment` and
`ChatContentAttachment`. `AttachmentKind` replaces both `ToolAttachmentKind` and `AgentAttachmentType`;
`ToolAttachment` and `AddAdditionalData` are gone.

- `ChatObjectBase.AddAttachment(IChatAttachment)` is the single write path: an `AdditionalChatData` is
  stored as is, a native/content payload becomes an invisible `NativeAttachmentMessagePart` /
  `AdditionalMessageContentPart`, anything else throws. `IToolCall.AddAttachment` and the chat `ToolCall`
  inherit it. `GetNativeAttachments` filters `INativeChatAttachment`.
- `AdditionalChatData` implements the marker; `AdditionalMessageContentPart` implements the content
  branch; `NativeAttachmentMessagePart` implements the native branch.
- `AgentAttachment` is abstract with `Source`; `AgentNativeAttachment` carries `AttachmentKind`/`Url` and
  materializes `Native`, `AgentContentAttachment` carries `Content`. `AgentToolCall.AddAttachment`
  branches native → content and silently ignores everything else (a chat UI payload or an unknown
  payload has no task representation). `AgentTool` hands its attachments to the call directly;
  `ChatAgentTool.ReadAttachments` filters the branch.
- `ToolMethodBinder` wraps the concrete `IAttachment` it already has, so the subtype and `Source` survive
  (the old `ToolAttachment` rebuilt the attachment from kind+url and lost them).
- The Lua surface (`AgentNativeAttachment.FromBase64`, the attachment projection) and the task executor
  use the split types; text contributions are filtered out of the native projection instead of throwing.
- `ToolCallContentProjector` (mirroring `MessageContentProjector`) projects a call's content parts;
  `AgentPromptComposer` uses it, skipped for a compacted call.
- The task-detail XAML renders attachments with two typed `DataTemplate`s
  (`AgentNativeAttachment` → `Url`, `AgentContentAttachment` → `Content`). No display member was added to
  the domain: `LocaleKeyBase` was rejected for a URL, and a domain `DisplayText` would have been a UI
  concern leaking into the model.

### Tests

`ChatAttachmentProjectionTests` covers the chat write path (UI part as is, native wrapper and part,
kind+url materialization, content part + projector, unknown payload throws) and the task projection
(native/content, chat-UI drop, unknown payload ignored).

### Gates

- Core project: builds, 0 errors, 0 warnings.
- Desktop project: builds, 0 errors (only the pre-existing NuGet advisory warnings).
- `LLMDesktopAssistant.Tests`: 1209 total, 1208 passed, 1 skipped, 0 failed.
- `LLMDesktopAssistant.Desktop.Tests`: 44 total, 44 passed, 0 failed.

## Acceptance

- `AddAdditionalData`, `ToolAttachment`, `ToolAttachmentKind` and `AgentAttachmentType` are gone.
- A tool that produces an image and a note reaches the model as an attachment and as appended text in
  both worlds; an agent task keeps the content (no silent drop).
- Unit tests: projection into the chat call (all four input kinds), rejection of an unknown attachment,
  the task projection and the branch order, the composer's content append (normal and compacted),
  the marker filters.
- Core, Desktop and both test projects build and pass.
