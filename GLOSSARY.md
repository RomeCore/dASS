# dASS

dASS (Desktop Assistant) is a cross-platform C#/Avalonia application for universal LLM/agentic interactions.

## Language

**Command**:
A user-initiated invocation token at the very start of a chat message that triggers a runtime action in the chat. It is never exposed to agents.
_Avoid_: slash command, macro, quick action

**Command namespace**:
An optional qualifier that disambiguates a command, derived from the command's type (skill, agent, …) or from its addon pack.
_Avoid_: prefix, scope

**Command fingerprint**:
The machine-readable record of a command invocation, attached to the message that invocation produced.
_Avoid_: trace, log entry

**Disabled message**:
A chat message that stays in the transcript and the UI but is hidden from every agent.
_Avoid_: hidden message, muted message

**Generate intent**:
The user's send-time choice of whether the chat is handed to the agent pipeline after the message is delivered. A command may lower it but never raise it.
_Avoid_: auto-run, generation flag
