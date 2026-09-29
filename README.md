# 🌌 dASS - Desktop Assistant

**dASS (Desktop Assistant)** is a powerful, multi-platform application built with **Avalonia UI** and **.NET 10** that provides an intelligent LLM-powered assistant with a rich and extensible set of tools, multi-agent collaboration, a universal addon system, a cache-friendly prompt engine, semantic memory, MCP (Model Context Protocol) support, and a web-based chat UI for multi-user chatting.

![Simple agent calling example](assets/flip_coin_test.png)

---

## ✨ Features

### 🧠 Multi-agent system
- **Multiple specialized agents** with individual configurations
- **Agent execution strategies**: Sequential, Random, Adaptive, Mention-only, and Round-robin
- **Agent read permissions** - control what each agent can see in the conversation
- **Per-agent generation settings**: reasoning, persona, specialization, behaviour sliders and skills

### 🧩 Addon system

Everything that extends the assistant — skills, sub-agents, tools, prompt contexts, templates and Lua scripts — is an **addon**, and addons are distributed in **packs**.

| Addon | What it gives you |
|---|---|
| **Skills** | step-by-step instructions the assistant loads only when the task calls for them |
| **Sub-agents** | ready-made specialists to delegate work to, without configuring a new agent by hand |
| **Tools** | extra capabilities, from filesystem and web to wrappers around your own services |
| **Prompt contexts** | dynamic context that is injected into the request |
| **Templates** | reusable prompt parts: personas, specializations and components |
| **Lua scripts** | your own automation and hooks inside the app |

- **Bring your own library** - the assistant picks up skills and sub-agents from the home folders of other AI coding tools (Claude Code, Gemini CLI, Codex, GitHub Copilot, Cursor, Windsurf, OpenCode, Cline, Roo Code, LM Studio, JetBrains Junie and more), so an existing skill collection works as-is.
- **Packs and layers** - a pack dropped into your workspace, your user profile or any folder you point at is picked up automatically; when two packs define the same addon, the later one overrides the earlier one.
- **Per-agent choice** - every agent decides which addons it has: enable, hide, override, or attach its own parameter values.
- **On-demand discovery** - agents search the addon library instead of carrying every skill in the prompt, so a large collection costs nothing until it is actually needed.
- **Visual browser** - a card-based panel for inspecting packs and addons, toggling them, setting per-addon parameters and models, and seeing diagnostics for broken files instead of having them fail silently.

### 🛠️ Rich tool system
- **Filesystem operations** - read, write, search, replace, copy, delete files and directories
- **Web requests & search** - fetch URLs, search the web, download files
- **Document reading** - PDF, DOCX, PPTX
- **Image description** - describe images using vision models when main agent cannot read images natively
- **Mathematics** - execute mathematical calculations using built-in evaluator and solver
- **Databases** - query SQLite and PostgreSQL databases via managed connections
- **Random** - dice rolls (for DnD), random numbers, GUIDs, list shuffling
- **Human-in-the-Loop** - file pickers, confirmation dialogs, choice selection
- **Shell execution** - with optional live and interactive terminal in the UI
- **Interactive diff confirmation** - preview file changes with color-coded diffs, accept or decline individual edits directly in the chat
- **Time utilities** - get current time, wait/delays
- **Scripting** - Python execution in configured `.venv`/global environment, Lua via AsyncLua (async/await Lua interpreter with a wide range of API bindings) and C# scripts via Roslyn
- **Scriptable tools** - dynamic tools the assistant creates for itself in Python, Lua or C# when the built-in set is not enough (see below)
- **MCP** - tools from external servers

Configure which tools each agent can use right from the agent settings:

![Agent tools settings](assets/ui_settings_agent_tools.png)

### 🧠 Semantic memory
- **Memory blocks** with configurable access modes (read-only, write, full) attached to chats and agents
- **Facts** with semantic search and **episodic logs** with keyword search
- **Automatic memory recorder and reader** - the assistant remembers important information about you and retrieves it when needed

### 🛡️ Smart tool approval
- **Tool behaviour system** that analyses what tools will really do (when a file deletion tool will not find the target file, then the tool will not require confirmation, because it will do nothing). This also allows to auto-approve tools that just create *new* files and require confirmation when tools try to edit *existing* files
- **Specifier engine** - declarative per-tool policy rules that match tool arguments (including parsed shell commands), with configurable policy aggregation and per-tool overrides
- **Secrets protection** - DetectSecretsSharp prevents leakage of secrets when the LLM reads files

### ⚡ Prompt engine (SCM)

*Sequential Context Management* keeps the system prompt **frozen** and delivers changes to it as explicit events in the conversation, instead of quietly rebuilding the prompt on every message.

- **Cache-friendly by design** - the prompt prefix stays byte-identical between changes, so provider-side prompt caching actually hits and local models keep their precomputed state. Long chats stop paying for the same prompt over and over.
- **Changes you can see** - when tools, skills or the persona change, the assistant gets an explicit notice in the conversation, on the same timeline as the messages. It knows what changed and when, rather than silently reading a different prompt.
- **Three modes per agent** - **Dynamic** (rebuild on every request, the classic behaviour), **Hybrid** (frozen prompt plus change notices, the default) and **Static** (fully frozen until you refresh it, for reproducible runs).
- **Never loses track** - when context is trimmed by a shield, a summary or a compaction, the prompt is re-baselined automatically, so nothing from the frozen prompt silently goes missing.
- **Checkpoints under your control** - context shields, summaries, tool-result compaction, forced tool compaction and reasoning compaction, each switched on or off per agent.

### 🔧 Other features
- Built-in **Blazor-based Web UI** that can be hosted on a local endpoint with optional password protection
- **Multiple working directories** - switch between project roots per chat
- **Prompt manager** - personas, specializations, prompt components and behaviour sliders live in LLT templates that can ship inside addon packs; pick and configure them per agent in the UI (a built-in LLT editor is still on the way)
- Zero-dependency **web-search** using an embedded version of SearXNG - **SearXSharp**, that scrapes multiple search engines (Google, Bing, DuckDuckGo and much more) concurrently. **No API key needed!**
- **Localization** - full UI localization with semantic keys and `.loc` files (invariant + `ru-RU`)
- **Built-in help viewer** - localized documentation with GitHub-flavoured alerts rendered right in the app
- **Long conversations** - automatic summarization, context shields and tool-result compaction keep chats inside the model window (see **Prompt engine** above)

---

## 🪄 Scriptable tools

When you want to expand your agent's functionality, you can give it a task - explore the API and create a **scriptable tool** in Lua, Python or C#. In this example, we'd create a tool that gives commit names based on the current git context:

![Scriptable tool creation](assets/metatool_creation.png)

The assistant got a tool that runs `git diff`, feeds the result to an internal agent with a special system prompt, and shows the answer to the main agent. Now try it in another chat:

![Scriptable tool invocation](assets/metatool_invoke.png)

And check the tool in the agent's settings:

![Scriptable tool added to the list in agent settings](assets/metatool_added_to_the_list.png)

If you want to edit, share or create tools by yourself, drop `.lua`, `.py` and `.csx` files into the `tools` folder of an addon pack (for example `%LOCALAPPDATA%/.llmassist/packs/<your-pack>/tools`, or `.agents/tools` in your workspace).

---

## 🗺️ Platforms

| Platform | Project |
|---|---|
| Windows / Linux / macOS | `LLMDesktopAssistant.Desktop` |
| Android | `LLMDesktopAssistant.Android` |
| Browser (WebAssembly) | `LLMDesktopAssistant.Browser` |
| Web chat UI (multi-user) | `LLMDesktopAssistant.Blazor` |

## 🧪 Tests

Unit and integration tests covering the core, tools, specifiers, localization, help and more live in `tests/` (`LLMDesktopAssistant.Tests` and `LLMDesktopAssistant.Desktop.Tests`).

---

## 🧩 The author's developed tech stack

| Technology | Purpose |
|---|---|
| [**RCLLM**](https://github.com/RomeCore/RCLargeLanguageModels) | Lightweight LLM client library |
| [**LLTSharp**](https://github.com/RomeCore/LLTSharp) | Metadata-rich and easy-readable prompt templates for LLM |
| [**AsyncLua**](https://github.com/RomeCore/AsyncLua) | Extended Lua scripting engine with concurrency and async/await support |
| [**RCParsing**](https://github.com/RomeCore/RCParsing) | Lexerless parser used in various utilities, such as math evaluation tool (also used in LLTSharp and AsyncLua) |
| [**SearXSharp**](https://github.com/RomeCore/SearXSharp) | C#-adapted [SearXNG](https://github.com/searxng/searxng) meta-search engine with 118+ engines supported |
| [**DetectSecretsSharp**](https://github.com/RomeCore/DetectSecretsSharp) | C#-adapted [yelp/detect-secrets](https://github.com/yelp/detect-secrets) used for preventing leakage of secrets when LLM is reading files |

Built on top of **Avalonia UI 12**, **.NET 10** and a number of great open-source libraries: model providers via RCLLM (OpenAI, DeepSeek, OpenRouter, Novita, Ollama and any OpenAI-compatible endpoint), LiteDB for storage, Markdig for markdown rendering, ModelContextProtocol for MCP, and much more.

---

## 📜 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

Copyright © 2026 **RomeCore**
