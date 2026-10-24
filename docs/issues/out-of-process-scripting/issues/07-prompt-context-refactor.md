# Prompt context refactor

Status: resolved
Type: task
Blocked by:

## Question

Reshape the scriptable prompt contexts to mirror tools and commands (spec §10):

- `IScriptablePromptContextEngine` becomes `Descriptors` plus
  `IPromptContextProvider CreateProvider(PromptContextInfo context)`;
- `PromptContextInfo.Kind` is parsed from the frontmatter instead of being derived from the provider type (D22);
- add `PromptContextExecutionContext` and replace `EffectiveChatContext` with it in **every** `IPrompt*Provider`
  method — live-tail, supersede, supersede stamp, anchored section and section delta providers; the processors
  receive addons for the scriptable families and pass `Addon = null` for native sections (D16, D23);
- remove `IServiceProvider` from `EffectiveChatContext` and the single assignment in
  `AgentEffectiveMessagesProvider`;
- keep the failure isolation of `ScriptPromptContextHelper`: a timeout, an error and invalid output all mean
  "no context", and the prompt build never throws;
- record the synchronous `CaptureStamp` landmine in the provider documentation (D24).

## Answer

Landed in `e3347bf6` — *refactor(prompting)!: make the prompt context provider the run* — **before** ticket 01.
The dependency was dropped on purpose: the engine keeps `Language` + `Descriptor` as a bridge, and ticket 01
replaces them with `Descriptors` together with the other two kinds. Authority for the result is `spec.md`
§10–§10.1; this answer records only what the code does and what was deliberately left out.

- `IScriptablePromptContextEngine` is **only a creator**: `Language`, `Descriptor`,
  `IPromptContextProvider CreateProvider(PromptContextInfo)`. No runner member, no delegate seam.
- `PromptContextInfo.Kind` is parsed from the frontmatter; `PromptContextNativeProvider.AddContext` defaults a
  native section to `Anchored`.
- **The provider is the run**, so the providers live next to their engines — `Scripting/Lua/` in core,
  `Desktop/Scripting/Python/` in the desktop assembly — on one base class per language
  (`LuaScriptContextProviderBase`, `PythonScriptContextProviderBase`). The base owns the linked-CTS timeout and
  the failure isolation: a timeout, an error and invalid output all mean "no context" plus a warning.
- Four providers: `LuaLiveTailScriptContextProvider`, `LuaSupersedeScriptContextProvider`,
  `PythonLiveTailScriptContextProvider`, `PythonSupersedeScriptContextProvider`.
- `PromptContextExecutionContext` (composition: `Context`, `PromptContext`, `Chat`, `Services`) replaced
  `EffectiveChatContext` in all five `IPrompt*Provider` methods, anchored sections and delta providers included.
  `PromptAnchoredSectionProcessor` and `PromptSupersedeContextProcessor` now take
  `IEnumerable<PromptContextInfo>` and build the execution context themselves with an object initializer, taking
  the services from the chat-scoped `IServiceProvider` in their constructor — the static `From` factory is gone.
- `EffectiveChatContext.Services` is removed (one writer, two readers, both script providers).
- `ScriptPromptContextHelper` is reduced to the formatting rules — `Wrap`, `GetTagName`, `GetDiscriminator`,
  `DefaultTimeoutMs`, `MaxTimeoutMs`; its `RunAsync` is gone with the runner.
- The script-provider unit tests were **deliberately deleted** ("the logic is too simple for tests").
  `ScriptablePromptContextTests` keeps only `Wrap` and the discriminator. Do not restore them unasked.

Verified at the time: core and desktop builds pass; `dotnet test tests/LLMDesktopAssistant.Tests` —
1273 passed, 1 pre-existing skip, 0 failed. Desktop tests were not run (the running app holds the build output).
The scriptable *path* has **not** been exercised in the app — no `.lua`/`.py` context addon exists in the
repository or its packs; smoke-test with a temporary `context/probe.py` (`kind: live-tail`, a `print`) before
trusting the Python provider at run time.

## Comments

- `Blocked by: 01` was dropped on resolution: the ticket was implemented ahead of the contract change, with
  `Language`/`Descriptor` left in place as a documented bridge. Ticket 01 removes them.
- The decision trail for the session is `.llmassist/handoffs/2026-10-24-out-of-process-scripting-p1.md`.
- The planned `Addon = null` for native sections never happened: every collected context is an addon, so the member
  is non-nullable and named `PromptContext` — *refactor(prompting): rename the execution context addon and inject
  the service provider*.
