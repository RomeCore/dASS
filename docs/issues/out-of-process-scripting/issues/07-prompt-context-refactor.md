# Prompt context refactor

Status: open
Type: task
Blocked by: 01

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

## Comments
