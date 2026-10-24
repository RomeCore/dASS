# Meta tools

Meta tools are **dynamic tools created by the LLM itself** when the built-in set is
not enough. They are stored as `.lua` or `.py` scripts.

## How they work

1. You ask the assistant to create a tool for a repetitive task.
2. The assistant explores the scripting API and writes a script.
3. The script is registered as a tool and appears in the agent's toolset.

## Where they live

Scripts are stored in `%LOCALAPPDATA%/LLMDesktopAssistant/metatools`.
You can edit them by hand or share them with others.

## Example

> [!TIP]
> A meta tool can wrap a whole workflow: for example, run `git diff`, send it to
> a sub-agent with a special prompt, and return the summary — all in one call.

See also: [Lua scripting](scripting.md), [Tools](../tools.md).
