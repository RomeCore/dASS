# Out-of-process scripting engines — specification

> Assembled from the grilling session of 2026-10-24, which started from
> `docs/ideas/scripting/out-of-process-scripting-engines.md`. This is the implementable document; the
> decisions carry their rationale so they are not re-litigated by the next session.

## 1. Goal

One family of out-of-process script engines for **every scriptable kind** — scriptable tools, scriptable
slash commands, scriptable prompt contexts, and (later) hook handlers — with one invocation contract:

- **input** is a generated **preamble** (never a file, an environment variable or argv);
- **output** is standard output, optionally carrying a **structured channel** that the kind maps into its own
  result contract;
- **failure** is a non-zero exit code with the message taken from standard error;
- **terminal mode** is a second, explicitly requested run kind with its own result shape.

Python stops being a special case: it becomes one of **four** languages (Python, Bash, Batch, PowerShell),
and the three separate Python engines collapse into the family. Further languages (JavaScript, …) enter by
the same descriptor without touching a parser, a locator or an adapter.

## 2. Placement

Everything that runs a process lives in **`LLMDesktopAssistant.Desktop`** (`Desktop/Scripting/ExternalProcess/`).
Core keeps only what its own parsers and providers need:

- the three kind interfaces (`IScriptableToolEngine`, `IScriptableSlashCommandEngine`,
  `IScriptablePromptContextEngine`) — their implementations are the adapters and live in Desktop;
- `IScriptEngineDescriptor` and the capability/config enums, because core parsers read them;
- `ScriptLanguageType` (already in core);
- the two mode enums and the parsed flags on `ToolInfo` / `SlashCommandInfo` / `PromptContextInfo`;
- `PromptContextExecutionContext` and the provider interfaces it is passed to.

`IProcessLauncher`, the PTY, `TerminalAdditionalViewModel` and every language engine are desktop-only;
mobile builds must stay free of them.

## 3. Shape: four language engines, three kind adapters

| | Knows | Does not know |
|---|---|---|
| **Language engine** (`IExternalProcessScriptEngine`, one per language) | how to build a preamble in its syntax, how to serialise a value as a literal, which file encoding its runtime needs, how to find its interpreter, how to launch and read a process | what a tool, a command or a context is; variable names; what the JSON means |
| **Kind adapter** (`ExternalProcessToolEngine`, `ExternalProcessSlashCommandEngine`, `ExternalProcessPromptContextEngine`) | which variables the kind exposes, the modes and flags of the addon, how the result maps into the kind's contract | any language detail |

The language engine has **exactly two** methods: managed process and terminal.

## 4. Contracts

```csharp
// Desktop/Scripting/ExternalProcess/
public enum ScriptableAddonKind { None, Tool, SlashCommand, PromptContext }   // Hook later

public enum ScriptInputMode { Auto, Json, Structured }
public enum ScriptOutputMode { None, Whole, Pattern }

public class ExternalProcessScriptTerminalParameters
{
    public required ScriptableAddonKind Kind { get; init; }
    public required string Body { get; init; }
    public ImmutableDictionary<string, INodeValue?> Variables { get; init; } = [];
    public ScriptInputMode InputMode { get; init; }      // Auto = by the engine's capabilities
    public string? ProcessName { get; init; }            // for the process dispatcher
    public TimeSpan? TimeOut { get; init; }

    // additive: a detached terminal must return immediately
    public bool Wait { get; init; } = true;
}

public class ExternalProcessScriptParameters : ExternalProcessScriptTerminalParameters
{
    public ScriptOutputMode OutputMode { get; init; }    // None | Whole | Pattern
    public string? OutputPattern { get; init; }          // "<BEGIN>*<END>", only with Pattern
}

public enum ExternalProcessStatus { Succeeded, ProcessFailed, InvalidOutput, TimedOut, Cancelled }

public class ExternalProcessScriptResult
{
    public required ExternalProcessStatus Status { get; init; }
    public int? ExitCode { get; init; }
    public string? Stdout { get; init; }
    public string? Stderr { get; init; }
    public string? Output { get; init; }                 // ProcessDescriptor.Output, chronological merge
    public JsonNode? Json { get; init; }
    public string? Error { get; init; }                  // non-empty means an error
}

public sealed record ExternalProcessScriptTerminalResult
{
    public required ExternalProcessStatus Status { get; init; }
    public int? ExitCode { get; init; }
    public required string Output { get; init; }
    public string? Error { get; init; }
}

/// <summary>Exactly two methods: managed process and terminal. Everything else belongs to the adapters.</summary>
public interface IExternalProcessScriptEngine
{
    ScriptLanguageType Language { get; }
    IScriptEngineDescriptor Descriptor { get; }          // extensions, markers, line prefix, capabilities

    Task<ExternalProcessScriptResult> RunAsync(ExternalProcessScriptParameters parameters,
        IServiceProvider services, CancellationToken ct);

    Task<ExternalProcessScriptTerminalResult> RunInTerminalAsync(ExternalProcessScriptTerminalParameters parameters,
        IChatAttachmentHost host, IServiceProvider services, CancellationToken ct);
}
```

`IServiceProvider services` is the **chat scope** and resolves exactly three things: `IChatSettingsService`
(working directory + the language's additional environment configuration, e.g. the Python venv),
`IAddonManagerInvalidator` (D19), and nothing else. It cannot be a constructor dependency: the engines are
app-scoped singletons and `IChatSettingsService` is chat-scoped. `IProcessLauncher` is app-scoped and is a
constructor dependency.

## 5. The preamble

One shape for every language: **[engine + runtime part] → [globals, one assignment each] → [body]**.

```csharp
public interface IScriptEngineDescriptor
{
    ScriptLanguageType Language { get; }
    IReadOnlyList<string> Extensions { get; }
    string FrontmatterStart { get; }
    string FrontmatterEnd { get; }
    string? FrontmatterLinePrefix { get; }               // D17 — shell engines only
    ScriptEngineCapabilities Capabilities { get; }
}

[Flags] public enum ScriptEngineCapabilities { None = 0, StructuredInput = 1, Terminal = 2, StructuredOutput = 4 }
```

`IScriptableToolEngine`, `IScriptableSlashCommandEngine` and `IScriptablePromptContextEngine` lose `Language`
(it duplicated the descriptor) and expose **`IReadOnlyList<IScriptEngineDescriptor> Descriptors`** instead of a
single `Descriptor`. The parsers and file locators build their extension → descriptor map from that list; the
kind adapters answer with the union of their languages.

### 5.1 Per-language literal forms

| Language | `json-input: true` (a JSON **string**) | `json-input: false` (native structure) |
|---|---|---|
| Python | `tool_args = '{"path": "C:\\\\x"}'` | `tool_args = {'path': 'C:\\x'}` |
| PowerShell | `$tool_args = '{"path":"C:\\x"}'` (`'` → `''`) | `$tool_args = '…' \| ConvertFrom-Json` (6+: `-AsHashtable`; 5.1: `PSCustomObject`) |
| Bash | `tool_args='{"path":"C:\\x"}'` (`'` → `'\''`) | not supported |
| Batch | `set "tool_args={"path":"C:\\x"}"` | not supported |

`StructuredInput` is therefore a capability, not a wish: Python yes, PowerShell yes (second class), Bash no,
Batch no. `json-input: auto` (the default) means "native structure where the engine can, JSON otherwise".
An explicit `json-input: false` on an engine without the capability is a **fatal addon diagnostic**.

Batch escaping rules (a hard fact, not a choice):

- inner double quotes survive `set "name=value"` — only the outer pair is stripped;
- `%` must be written `%%`;
- `!` breaks only under `EnableDelayedExpansion`, which we never enable;
- a literal newline cannot exist inside a `.bat` line — and it never has to, because compact JSON encodes
  newlines as the two characters `\n`;
- non-ASCII is avoided entirely by serialising JSON for Batch with the encoder's default ASCII escaping
  (`\uXXXX`), so the value is code-page independent;
- a `.bat` line is limited to ~8191 bytes: an over-long preamble fails fast with a clear error, it is never
  truncated.

### 5.2 Document encodings

| Language | Temp script file | Why |
|---|---|---|
| Python | UTF-8, no BOM | Python 3 source default is UTF-8 |
| Bash | UTF-8, no BOM | bytes are passed through; the pipe is UTF-8 |
| PowerShell | **UTF-8 with BOM** | Windows PowerShell 5.1 reads a BOM-less `.ps1` as ANSI, and Cyrillic becomes mojibake |
| Batch | ASCII-only preamble | the cmd parser reads the file in the console code page |

The PowerShell preamble also sets `[Console]::OutputEncoding = [Text.Encoding]::UTF8`; `ProcessLauncher`
already reads both streams as UTF-8. Invalid UTF-8 bytes are replaced (`U+FFFD`), never turned into a failure.

### 5.3 Working directory and temp files

Scripts are written to a **reserved temp folder**, not into the working directory (a generated file in the
working directory shows up in addon scans and in the user's project). The preamble `chdir`s into the chat's
effective working directory, so a script's relative paths mean what they mean everywhere else. Known cost:
in Python `sys.path[0]` and `__file__` point at the temp copy, so an addon cannot import a sibling module.

## 6. Input

`json-input` is parsed by the kind's parser and stored on the addon (`bool? JsonInput` on the three info
models). `Auto` is the default; `true` passes a JSON string the script parses itself; `false` passes the
engine's native literal.

The variable **names belong to the kind**, never to the engine: `tool_args` (the tool's arguments),
`command_args` (`CommandArgsNode.Create(ctx.Arguments, ctx.RawArguments)`), `previous` (the previously
injected text of a supersede context). The engine only renders `name = value`.

## 7. Output

`json-output` is `bool | string`, parsed by the kind's parser (`string? JsonOutputPattern`, plus a bool flag):

| Value | Meaning |
|---|---|
| absent | no parsing at all; `Json` stays `null` and that is **not** an error |
| `true` | the whole `Stdout` must parse as JSON |
| `"<BEGIN>*<END>"` | extract the capture and parse it |

Pattern grammar: `\*` is a literal asterisk; the **first unescaped** `*` splits the pattern into `BEGIN` and
`END`. Zero or two unescaped asterisks is a **fatal addon diagnostic**. Extraction is a first match, the
capture is non-greedy and DOTALL (`BEGIN{ "a":\n 1 }END` works). Not found and unparseable are both
`InvalidOutput` — a promised structure is never degraded into text.

Rules:

- **JSON mode** (`Whole` or `Pattern`): only `Stdout` is read; `Stderr` never contaminates the parse.
- **Raw mode** (`None`): the result is `Output` — the merged, arrival-ordered stream that
  `ProcessDescriptor.Output` already produces. Nothing is re-spliced, and stderr is deliberately not filtered
  out: a script without a JSON channel is heard exactly as a terminal would show it.
- A non-zero exit with empty `Stderr` still has a message: the exit code plus a truncated command line.

## 8. Per-kind mapping

Unknown top-level keys in the JSON are **ignored** — each adapter reads the keys it knows and leaves the rest to a
newer engine (amended on 2026-10-25; D26 asked for the opposite and was dropped as premature, see the decision row).

### 8.1 Tool

```json
{
  "status":     { "icon": "Check", "title": "Parsed 12 files" },
  "result":     "text for the model (default: stdout)",
  "markdown":   true,
  "structured": { "files": 12 }
}
```

`status` → `IToolCall.StatusIcon` / `StatusTitle` (`VisualIconKind.TryParse`; an unknown icon logs a warning
and yields no icon, it is not an error); `result` / `markdown` → `ResultContent` / `UseMarkdown`;
`structured` → `StructuredResult`. Without `json-output` the result is `Output`; on failure it is
`Output + "\n\n" + Error`.

### 8.2 Slash command

```json
{
  "effect":   "12 files rewritten",
  "generate": false,
  "error":    "no git repository",
  "part":     { "title": "git status", "icon": "SourceBranch", "content": "…" }
}
```

`effect` → `EffectSummary`; `generate` can only lower the intent (`SlashCommandIntent.Resolve`); `error` →
`SlashCommandExecutionResult.Error` via `Locale.GetConstKey` (the script's text is a constant key);
`part` → an `AdditionalMessageContentPart`. `part.content` must be explicit: with JSON present, a missing
`content` is **not** replaced by stdout. Raw `Output` becomes `part.content` only when there is no JSON at all.

### 8.3 Prompt context

`json-output` on a context is a **fatal diagnostic**: a context yields text, nothing consumes a structure.
There is no JSON part in its mapping.

### 8.4 Hook (future)

A hook answers with a decision, not with text, so its `json-output` is **mandatory** and its absence is fatal.
The decision shape is a separate decision; `ExternalProcessScriptResult.Json` is already the container, so
nothing in this contract changes.

## 9. Terminal mode

`run-terminal: bool` in the frontmatter of tools and commands (not contexts). The engine's second method is
used; the result is `ExternalProcessScriptTerminalResult` with a single `Output` — a PTY has no stdout/stderr
split and no JSON, so `run-terminal` + `json-output` is a **fatal diagnostic**.

Process lifetime (three traps, all in the caller, not in `ProcessLauncher`):

1. `ProcessLaunchParameters.TimeOut` defaults to **30 minutes** — a terminal must pass `null`
   (`WithTimeout(null)`), the same way `TerminalBasedToolModule` does.
2. `LaunchTerminal` links the caller's token — a terminal must pass `CancellationToken.None` and register its
   kill manually, otherwise stopping a generation kills a running interactive session.
3. Awaiting the exit forever keeps the call "running" for the whole session. `Wait` decides:
   `Wait = true` awaits the exit; `Wait = false` returns immediately with `Status = Succeeded`,
   `ExitCode = null`, `Output` = a snapshot, and the exit code is written into the card later.

The engine creates `TerminalAdditionalViewModel`, attaches it to the `IChatAttachmentHost` it received (a
`IToolCall` or a chat message — both implement it) and never allows Leave. The launch/await/kill part of
`TerminalBasedToolModule.Run` is extracted into a shared `TerminalRun` instead of being duplicated.

## 10. Scriptable prompt contexts

The context engine is reshaped to mirror tools and commands — it is **only a creator**:

```csharp
public interface IScriptablePromptContextEngine
{
    ScriptLanguageType Language { get; }
    IScriptablePromptContextEngineDescriptor Descriptor { get; }
    IPromptContextProvider CreateProvider(PromptContextInfo context);
}
```

(`Language`/`Descriptor` disappear with the `Descriptors` change of ticket 01, exactly like the other two kinds.)

**The provider is the run.** There is no runner member on the engine and no delegate seam: the engine builds a
provider of its own language and that provider executes the body. The providers therefore live next to their
engine — Lua's in `Scripting/Lua/`, Python's in `Desktop/Scripting/Python/` — and only the formatting rules stay
shared.

- `PromptContextInfo.Kind` is **parsed from the frontmatter** (`kind:`), never derived from the provider type —
  otherwise `CreateProvider` and `Kind` are circular. Native sections declare `Anchored` through
  `PromptContextNativeProvider.AddContext`, which fills the default.
- Four script providers: `LuaLiveTailScriptContextProvider`, `LuaSupersedeScriptContextProvider`,
  `PythonLiveTailScriptContextProvider`, `PythonSupersedeScriptContextProvider`, one base class per language
  (`LuaScriptContextProviderBase`, `PythonScriptContextProviderBase`). A base only runs the body: it arms no
  timeout and catches nothing.
- **The budget and the failure isolation live where a context is used**, never in a language. `PromptContextBudget`
  (`LLM/Services/Prompting/`) is called by `AgentPromptComposer` (live tails), `PromptSupersedeContextProcessor`
  and `PromptAnchoredSectionProcessor`: it clamps `PromptContextInfo.TimeoutMs` to the ceiling, arms the linked
  CTS and turns a timeout, an error and invalid output into "no context" with one warning — one rule for every
  language and every context family.
- The Python provider's context text is `Output` (the merged stream): a context has no JSON channel, so stderr is
  part of what it says, not noise to be filtered.
- `ScriptPromptContextHelper` survives as the **formatting** rules only: `Wrap`, `GetTagName`,
  `GetDiscriminator`, `DefaultTimeoutMs`, `MaxTimeoutMs`. Its `RunAsync` is gone with the runner.
- The supersede comparison stays byte-exact on the wrapped text, inside the provider.

### 10.1 `PromptContextExecutionContext`

```csharp
public sealed class PromptContextExecutionContext
{
    public required EffectiveChatContext Context { get; init; }   // contained, not inherited
    public required PromptContextInfo PromptContext { get; init; }   // never null: every context is an addon
    public required Chat Chat { get; init; }
    public required IServiceProvider Services { get; init; }
}
```

It replaces `EffectiveChatContext` in **every** `IPrompt*Provider` method, with no asymmetry:

- `IPromptLiveTailContextProvider.ProvideAsync`
- `IPromptSupersedeContextProvider.GetNewStampAsync`
- `IPromptSupersedeStampProvider<TStamp>.CaptureStamp`
- `IPromptAnchoredSectionProvider.CalculateDeltaAsync`
- `IPromptSectionDeltaProvider<TState, TDelta>.CalculateDeltaAsync`

Implementations: `PromptLiveTailContextBase`, `PromptSupersedeContextBase<TStamp>`,
`PromptAnchoredSectionBase<TState, TDelta>`, `AddonSectionDeltaProvider<TAddon, TDelta>`, every native delta
provider under `Prompting/Context/Providers/<Name>/`, and the four script providers.

Builders: `PromptAnchoredSectionProcessor`, `PromptSupersedeContextProcessor`, their interfaces, and
`AgentPromptComposer`. They receive **addons** (`IEnumerable<PromptContextInfo>`), pick the providers of their
family and build the execution context themselves with an object initializer (`Chat`, `Context`, `PromptContext`,
`Services`) — there is no `From` factory. `PromptContext` is never null: every context, native or scriptable, is a
`PromptContextInfo` addon.

`EffectiveChatContext.Services` is **removed**: it was written in one place and read in two, both of them script
providers. The builders take the chat-scoped `IServiceProvider` in their constructor and put it into the execution
context, instead of each of them reading `Chat.Services`.

Known latent landmine, recorded not fixed: `IPromptSupersedeStampProvider.CaptureStamp` is synchronous; a
scriptable anchored section will need a `Task` form.


## 11. Frontmatter in shell files

`FrontmatterLinePrefix` is a new member of the descriptor and of `AddonParserDescriptor`, and the parser gains
a second mode:

- **no prefix** (Python `"""`, Lua `--[[ ]]`) — the current behaviour: slice between start and end markers;
- **with a prefix** - the same slice, but the marker lines carry the prefix as well (`#---`) and every line
  between them must start with it; the prefix is stripped per line before YAML parsing. A leading *run* of
  prefixed lines was considered and rejected: it would swallow a shebang and every ordinary comment at the top
  of a shell file.

`#` for Bash and PowerShell, `REM ` for Batch. Every frontmatter line is therefore a real comment, so the file
stays runnable as-is and the block is inert by construction.

## 12. Process lifetime, flags and diagnostics

| Concern | Rule |
|---|---|
| Timeouts (managed) | tool 30 min, command 10 min, context 2 s (frontmatter `timeout`, ceiling 10 s), terminal none |
| Cancellation | the engine's token kills the process tree (`Kill(entireProcessTree: true)` is already in the launcher) |
| Addon invalidation | tools and commands: `Invalidate(AddonKind.All)` in `finally` (after the process ends); contexts: never |
| Temp script | written to the reserved temp folder, deleted in `finally` |
| Interpreter discovery | fixed order plus one optional override; a missing interpreter is a **runtime failure and a settings check** - the parse is app-wide and cannot see a chat's setting (amended 2026-10-25) |

Discovery order (D20):

- **Bash**: `bash` on PATH; on Windows prefer Git Bash over `C:\Windows\System32\bash.exe` (WSL), and WSL only
  when explicitly configured — the two have different filesystem namespaces.
- **PowerShell**: `pwsh` (7+) before `powershell.exe` (5.1).
- **Batch**: `cmd.exe` (always present on Windows).
- **Python**: the configured venv activation script, else `py -3`, else `python`, else `python3`.

Generalising the per-language runtime settings (a settings UI beyond the existing Python venv one) is a
separate effort; this change adds none.

Diagnostics are fatal at addon load: `json-input: false` on a non-capable engine, `json-output` on a context,
`run-terminal` together with `json-output`, a malformed `json-output` pattern. A missing interpreter is **not**
one of them: the addon is parsed once for the whole application while the interpreter path is a per-chat setting,
so the only honest places to report it are the run and the settings check.

## 13. Decisions

| # | Decision | Why |
|---|---|---|
| D1 | Python, Bash, Batch, PowerShell ship in this change | a one-language family is unfalsifiable; a shell immediately exposes Python-shaped assumptions |
| D2 | `execute-python` / `execute-python_venv_shell` stay native tools | they are execution tools, not addon engines; `PythonHelperService` is shared |
| D3 | Input is a preamble only — no input file, no env var, no argv | one mechanism, and it is the only one that can carry a native structure |
| D4 | Variable names belong to the kind | the engine must not learn what a tool is |
| D5 | `json-input: auto \| true \| false`, `auto` by capability | the author writes the intent; Batch pays no ceremonial line |
| D6 | Structured input is a capability, not a promise | Bash and Batch cannot represent nesting; PowerShell only via a JSON round-trip |
| D7 | `json-output: bool \| "<BEGIN>*<END>"`, `\*` escapes | one field covers strict and noisy scripts; no glob dialect, no EOF games |
| D8 | A promised structure is never degraded into text | silent degradation hides the defect inside the script |
| D9 | `Status` + `Error`, with `Output` from the launcher | "the process failed" and "the process broke the contract" are different failures |
| D10 | Terminal is a separate method, result and lifetime | no timeout, no token linkage, optional `Wait` |
| D11 | `run-terminal` + `json-output` is fatal | a PTY has neither a stderr split nor a JSON channel |
| D12 | The JSON is the per-kind result contract | it feeds status/icon/title, structured results and, later, hook decisions |
| D13 | 4 engines + 3 adapters, 2 methods | behaviour must not diverge per kind; 12 classes would |
| D14 | `Descriptor` → `Descriptors`, `Language` leaves the kind interfaces | one adapter serves four languages, so the language is a per-file fact |
| D15 | Contexts mirror tools: `CreateProvider(addon)` | removes the one asymmetry of the engine family |
| D16 | `PromptContextExecutionContext` everywhere, `EffectiveChatContext.Services` removed | no asymmetry between context families; services belong to the chat |
| D17 | Per-line comment prefix for shell frontmatter | the block stays inert and the file stays runnable |
| D18 | Timeouts per kind (30 min / 10 min / 2 s / none) | a 30-minute command button is a UX bug, a context has a budget, a terminal has a human |
| D19 | Invalidate addons in `finally`; never for contexts | a context runs on every prompt build |
| D20 | Interpreter discovery has a fixed order and one optional override; ~~load-time diagnostics~~ **amended 2026-10-25**: a missing interpreter is a runtime failure | a parse is app-wide and a setting is per-chat, so the two cannot meet at load time; a broken addon still looks broken, one call later |
| D21 | Everything out-of-process lives in Desktop | `IProcessLauncher`, PTY and the terminal view model are desktop-only |
| D22 | `PromptContextInfo.Kind` is parsed, not derived | otherwise `CreateProvider` and `Kind` are circular |
| D23 | `PromptContextExecutionContext.PromptContext` is non-nullable | every collected context is a `PromptContextInfo` addon, native and scriptable alike, so a provider that is not addon-backed has no place in the pipeline — the type says so instead of a convention |
| D24 | Synchronous `CaptureStamp` is a recorded landmine | a scriptable anchored section will need `Task` |
| D25 | Batch > 8191 bytes fails fast | cmd cannot hold it; truncation would be silent corruption |
| D26 | ~~Unknown JSON keys are `InvalidOutput`~~ **amended 2026-10-25**: unknown keys are ignored | strictness without a schema to compare against was ceremony; a per-adapter read keeps the contract readable. Revisit if a typo ever costs a session |
| D27 | `part.content` is explicit once JSON exists | stdout is raw output, not a substitute for a declared part |

## 14. Out of scope

- Sandboxing, permission models and resource limits beyond a timeout.
- Long-lived workers, persistent interpreter sessions, REPL-style execution.
- Streaming standard output into a tool card while the managed process runs.
- Hook events themselves and the hook decision shape.
- Scriptable **anchored** prompt contexts (the execution context already accommodates them).
- Generalising the per-language runtime settings into a UI.
- JavaScript and every further language (they enter by descriptor only).
- Migrating existing Python addons: the repository and its packs contain none.

## 15. Verification

1. `python`, `bash`, `cmd` and `powershell` tools/commands/contexts each produce a result through one and the
   same adapter, with only their language engines differing.
2. A Batch addon with `%`, `"` and Cyrillic arguments receives them byte-exact (ASCII-escaped JSON), and an
   over-long preamble fails with a message rather than truncating.
3. A PowerShell addon with Cyrillic in its body reads correctly on 5.1 (BOM) and 7.
4. `json-output: "@@*@@"` extracts a pretty-printed JSON from a noisy stdout; a missing marker and invalid JSON
   both produce `InvalidOutput`, never text.
5. `json-input: false` on a Bash or Batch addon is a load-time diagnostic.
6. `run-terminal: true` with `wait: false` survives a stopped generation and a 30-minute wall clock.
7. A scriptable prompt context still returns "no context" on timeout, error and invalid output, and the prompt
   build never throws.
8. `EffectiveChatContext` no longer carries `IServiceProvider`, and every provider receives the same execution
   context type.
