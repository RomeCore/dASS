# Spec assembly

Status: resolved
Type: task
Blocked by: 02, 04, 05, 06, 07, 08, 09, 10, 12, 13

## Question

Assemble every resolved decision into `docs/issues/slash-commands/spec.md` — the **destination artifact**.

## Answer

Done — [`spec.md`](../spec.md), assembled from all resolved tickets and prototypes. Sections:

1. Scope (in / non-goals / out of scope)
2. Command model (settled premises)
3. Command kind, registry and sources
4. Resolution and namespacing
5. Command formats and argument schema
6. Message insertion and execution (incl. the execution token)
7. Skill and agent commands
8. Command fingerprint
9. Disabled-message toggle
10. Chat-level sub-agent tool policies
11. Input UX (autocomplete popup, highlighting/argument completion)
12. Lua API
13. Reusable layers surfaced by this effort
14. References

The spec is the destination: the map ends here; implementation is a separate effort.

## Comments

- Assembled after every other ticket resolved.
