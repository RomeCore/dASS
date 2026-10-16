# Argument editing at approval

Status: resolved
Type: grilling
Blocked by: 08

## Question

The user wants to edit a tool call's arguments at the approval step and have the edit take effect in execution. This ticket settles how an edited argument set is represented, how it flows from approval into execution, what validates the edited set, and what happens when the edited set is invalid or the tool is not designed to be edited. This is feature goal one.

## Answer

Argument editing has **two live channels**, matching the persisted/ephemeral split:

- **Edit `call.Arguments`** (persisted) — the model's intent is substituted; this is the "the model got the arguments wrong" path, performed at the approval step.
- **Set `ctx.ArgumentsOverride`** (ephemeral `JsonNode?`) — a runtime substitution that affects only the tool's behaviour; the **hook channel**.

Both feed the single **effective-arguments** accessor (`Effective = ArgumentsOverride ?? parsed(call.Arguments)`), which every stage reads (ticket 08).

### Validation — both channels are validated

- The **approval edit** is user input and is validated against `ArgumentSchema` through `IToolValidationService` **before** the confirmation can be accepted; an invalid edit blocks the surface (with the error) and the tool never executes an invalid set.
- The **hook override is not inherently trusted** — hooks may be authored in Lua (add-on code) — so it is validated **at the point it is installed**, so the hook fails eagerly with a clear error rather than letting a bad set reach the lock, the specifier check, or the executor.

### "Not designed to be edited"

This is a property of the **approval surface**, not a separate descriptor flag: a tool whose surface offers no editor is simply not editable; the default panel offers one.

## Comments

<!-- appended conversation -->
