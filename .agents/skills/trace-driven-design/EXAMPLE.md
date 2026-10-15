# Worked example: one tool call, traced

> One *instance* of the technique, not the technique itself - the reusable method is in `SKILL.md`. Everything below, the findings included, belongs to this design and is not a rule.

A real design session. Goal: unify two parallel tool-execution paths (a chat path and an
agent-task path) without losing features. The hard part was never the boxes - it was *what
gets passed where*. The trace below is condensed; the point is not the code, it is the
annotations and the findings at the bottom.

## The scenario

One tool call on the richest path (a chat tool, full pipeline): streaming → validation →
synchronization → preview → policy → approval → execution → settle. The other four paths
(a tool restart, a tool command, an agent task, a script call) are diffs at the end.

## The trace (condensed)

```csharp
// ENTRY — one tool call per assistant response
void ProcessToolCall(IFunctionToolCall fnCall)
{
    var descriptor = toolsetCache.ValidAliasedTools[fnCall.ToolName];
    var call = new ToolCall { ToolCallId = fnCall.Id, ToolName = descriptor.Name, Arguments = fnCall.Args };
    message.ToolCalls.Add(call);                       // host = AssistantMessage

    var env = new ToolExecutionContext                 // <<< created ONCE per invocation
    {
        Descriptor = descriptor, Call = call,
        Source = InvocationSource.ChatRun,
        Services = chat.Services,                      // for [Inject]
        Chat = chat, Message = message, RunningInUI = true,
        Confirmation = new ChatConfirmationSink(call), // <<< source-specific sink
    };
    _ = orchestrator.RunChatAsync(env, token);         // one task per call, run in parallel
}

async Task RunChatAsync(ToolExecutionContext env, CancellationToken ct)
{
    var p = env.Agent.Tools.GetEffectivePolicy(chatSettings);
    env.EffectiveBehaviours = (p.AutoApprove, p.Disallow);   // source-specific input, filled by CALLER

    await _streaming.RunAsync(env, StreamingMode.Cyclic, ct);
        // == expand ==
        if (env.Descriptor is not IToolStreamingExecutor s) return;   // absent on the task path
        partial.ArgsPartAdded += (_, _) => {
            env.Call.Arguments = partial.Args;
            var r = s.AnalyzeArgs(TolerantJsonParser.Parse(env.Call.Arguments), env); // tool code gets env
            env.Call.StatusIcon = r.Icon; env.Call.StatusTitle = r.Title;
        };
        try { await partial; } finally { /* detach */ }

    if (!await _validation.ValidateAsync(env, ct)) return;
        // == expand == env.ParsedArgs = Parse(...); SchemaValidator.Validate(env.Descriptor.ArgumentSchema, env.ParsedArgs);

    await _sync.RunAsync(env, async () =>                       // lock lives HERE, not in the orchestrator
    {
        // == expand == keys = SyncKeys.For(env.Descriptor, env.Call, env.ParsedArgs);
        //              await _locks.AcquireAllOrdered(keys, ct); try { await inner(); } finally { _locks.Release(keys); }
    });
    // (the lambda body continues:)
    {
        if (!await _preview.RunAsync(env, ct)) return;          // false = interrupt / structured stop
            // == expand == if (env.Descriptor is not IToolPreviewExecutor p) return true;
            //              var r = await p.PreviewAsync(env, ct);   // may write env.SharedContext
            //              fold r into env.Call; if (r.InterruptingSuccess) { set result; return false; }

        env.Call.ExpectedBehaviour ??= env.Descriptor.DefaultExpectedBehaviour;

        var pol = await _policy.EvaluateAsync(env, ct);
            // == expand == merge env.Descriptor.PolicyMask into env.EffectiveBehaviours;
            //              ApproveTool(approvalLevel, expectedBehaviour, auto, dis);
            //              SpecifierEngine.Evaluate / Combine;
            //              memorized-decision override; env.PolicyDecision = decision;

        if (pol.Stop) { env.Call.Status = Error; env.Call.ResultContent = pol.Message; return; }

        if (pol.Decision == Ask && !env.SelfHandled.HasFlag(Ask))
            if (!await _approval.RequestAsync(env, ct)) { env.Call.Status = Cancelled; return; }
                // == expand == var consent = await env.Confirmation.AskAsync(env, ct);   // <<< the seam
                //              (chat sink writes env.Call.UserConfirmationSource; task sink writes task.ConfirmationRequests)
                //              _memorization.MemorizeConsent(...); if (Always) env.PersistAlways(...);

        await _execution.RunAsync(env, ct);
            // == expand == env.Call.Status = Executing;
            //              await ((IToolMainExecutor)env.Descriptor).ExecuteAsync(env, ct);
            // == expand the chat executor ==
            //              var rich = new ReactiveToolResult(); env.Call.ReactiveToolResult = rich;   // chat-only richness
            //              rich.PropertyChanged += ...copy into env.Call...;
            //              var r = await _toolDelegate(env.ParsedArgs, env, ct);   // <<< the tool's own code
            //              await r.Completion;
            // == settle == set final status from success / cancel; default text; append notes.

    env.Call.CompletionToken.Complete();   // unblock the chat loop's Task.WhenAll
}
```

## The other four paths, as diffs

| Path | streaming | validation | sync | preview | policy | approval | execution |
|---|---|---|---|---|---|---|---|
| chat-run | cyclic | yes | yes | yes | yes | explicit | yes |
| restart | one-shot | yes | yes | yes | - | - | yes |
| agent task | - | yes | yes | yes | yes | implicit | yes |
| `/tool:` | one-shot | yes | yes | yes | - | - | yes |
| script call | - | yes | yes | yes | - | - | yes |

## What the trace exposed

1. **Four contexts, one invocation.** The original rebuilt a `ToolExecutionContext` for
   streaming, preview, specifiers and execution, copying `SharedContext` in and out by hand at
   every hop. The trace makes it obvious: create `env` **once**, mutate it in place.
2. **A fat context mixing five concerns** - invocation identity (`Descriptor`, `Call`, `Source`),
   host (`Chat`, `Message`, `Services`), threading state (`SharedContext`, `ParsedArgs`), the
   policy input (`EffectiveBehaviours`, `SelfHandled`), and the sinks (`Confirmation`).
   Naming them separately is the design move.
3. **The persisted/ephemeral boundary is the crux.** `IToolCall` is persisted and tool-facing;
   `env` is ephemeral and pipeline-facing. The trace shows a tool writing to *both* - so the
   line between them must be drawn explicitly.
4. **The source-specific seam is the confirmation sink.** Everything downstream of
   `env.Confirmation.AskAsync(...)` is identical; only the sink differs. That is where the two
   worlds actually fork, and the trace points at it directly.
5. **The lock belongs to a service, not the orchestrator.** With five callers, a lock written
   into each orchestrator is five copies; the trace (which would repeat the acquire/release
   block every time) makes the duplication visible before it is written.
6. **The tool is handed the whole world.** `_toolDelegate(args, env, ct)` gives the tool
   `Descriptor`, `EffectiveBehaviours`, `Source` - which it does not use. The trace forces the
   question of a narrow tool-facing view.
