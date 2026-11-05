# 0001. The argument descriptor is the single description of a tool's arguments

- **Status:** accepted
- **Date:** 2026-11-06
- **Related:** `docs/issues/glob-patterns/spec.md`, `docs/agent-reports/AR-0025`, `AR-0026`

## Context

Four places knew what a tool accepts, and none of them was authoritative:

- the C# signature - descriptions, defaults, nullability, the JSON type of every parameter;
- the generated `ArgumentSchema`, stored as a mutable `JsonObject` on the tool;
- a separate list of specifier parameter names, declared by hand next to the schema;
- each handwritten specifier analyzer, which re-derived the names, the kind of every value and
  the values themselves from the arguments.

The expected damage is not hypothetical. The shell specifier analyzers declared `bash`, `batch` and
`powershell` while the schema had been renamed to `command`; the binder threw "missing required
parameter" for every rule, `SpecifierEngine` logged it at debug level and continued, so **every shell
specifier rule was inert** and the tests encoded the same wrong name. Nothing in the system compared
the analyzer's view of the arguments with the schema.

Two more problems came from the same place:

- one specifier dialect served every target, so a filesystem path could not be matched as a path;
- the tool card had no way to say that a rule is broken, because the rule is only compiled while a
  call is being evaluated.

## Decision

1. **`ToolArgumentsDescriptor` / `ToolArgumentDescriptor` are the single description of a tool's
   arguments.** One descriptor per argument: JSON name, description, required flag, declaration order,
   `ToolArgumentKind`, the CLR parameter when there is one, the authored schema and the model-facing
   schema. `IToolDescriptor` exposes the aggregate as `Arguments`.
2. **The model-facing schema is a projection, not a stored value.** `ArgumentSchema` is
   `Arguments.ToSchema()`, built on first read and cached; the setter is gone. The schema can no
   longer disagree with the arguments it came from.
3. **dASS metadata lives in the authored schema and is trimmed from the model-facing one.**
   `main: true` marks the main argument, `kind: "file" | "directory" | "fileOrDirectory"` marks a path. Both are
   authored from C# with `[MainArgument]` and `[PathArgument(File, Directory)]`; a conversion from a
   foreign schema reads the same keys. A path declaration that contradicts the JSON type is refused
   at creation.
4. **A converted schema keeps its authored root.** `FromSchema` stores the root verbatim and replaces
   only the properties with their trimmed descriptors, so `required` order, `additionalProperties`,
   `title` and anything else the author wrote reaches the model unchanged. Re-serialising a schema we
   did not author is a change to what the model sees, and the tools section caches it by bytes.
5. **`ModifyArgumentSchema` is a per-argument hook.** It receives the JSON name and that argument's
   authored schema; it may narrow `enum` values and rewrite the description, and it may not change
   `type` (guarded) or the metadata (re-applied afterwards). Reshaping the argument set is no longer
   possible, which is the point: the argument set is the descriptor's.
6. **A specifier literal is compiled in the dialect of the argument it targets.** `SpecifierKind` is
   derived from `ToolArgumentKind`: files and directories are path patterns, scalars are text, arrays
   and objects are not targets at all. Paths are matched by segments through `GlobPathMatcher`, relative
   to the tool's working directory, case-insensitively on Windows. The main argument is deliberately
   **not** in the named set: an unnamed literal already covers it, and a second name for it would be a
   competing view of the same value.
7. **The specifier analyzer is generated from the descriptor.** `GeneratedSpecifierAnalyzer` is
   stateless and shared; it reads `ctx.Descriptor.Arguments`, takes the effective value
   (`args[name] ?? Default`), and matches. A handwritten analyzer remains only where the value a rule
   must see is not the raw argument - the shell splits a compound command into the parts a rule covers.
   A tool whose arguments hold no targetable value gets no analyzer at all.
8. **A tool command's arguments come from the descriptor too.** `ToolArgumentSchemaMapper` builds the
   keyed arguments and the main argument as the rest positional, so `/tool:shell-bash git status --short`
   is one value. The format provider follows `Kind`; a closed `enum` still wins.
9. **A rule is validated while it is written.** `SpecifierPatternValidator` parses the expression with
   the tool's names and compiles every literal exactly as the matcher will, so the card can show the
   problem and underline the literal instead of leaving an inert rule behind.

## Consequences

- The four descriptions are one. A renamed argument, a wrong default or a new path kind is a single
  edit, and the compiler refuses a `[PathArgument]` on a non-string.
- Filesystem tools get specifier rules from attributes alone; no analyzer code per tool.
- The card can validate, highlight and explain a rule, because the same descriptor drives the editor.
- **A rule's argument names are a persistent contract.** Rules live in tool changes and agent packs;
  renaming a JSON argument silently changes what every saved rule means. Metadata cannot be renamed
  independently of the schema, which is a decision, not an oversight.
- `OriginalSchema` and `Schema` are a pair, and only the second one is what the model sees. The
  invariant is "Schema is OriginalSchema without the metadata"; the hook re-trims after it runs.
- `ToolArgumentDescriptor` holds a `ParameterInfo` and a `JsonMemberAccessor`. Neither is serialisable,
  so the descriptor is built at runtime and must not be persisted.
- `main` and `kind` are dASS keywords inside a JSON Schema. A third-party schema validator would
  reject them, and a foreign tool that happens to use a `kind` property of its own gets it trimmed.
- A bare-value tool (`IsObject == false`) is a conversion-only case: the root schema becomes the one
  main argument, `ToSchema()` renders it as the whole argument schema, and the binder binds the whole
  node to the single JSON parameter. Streaming and the partial-argument analyzers still assume an object,
  and a scripted tool cannot express it yet because the frontmatter parser asks for a `JsonObject`.
- The shell keeps a handwritten analyzer, and with it the hazard that its parameter names drift from
  the schema again. The generated analyzer cannot split a compound command; a test now pins the names.

## Alternatives considered

- **Keep a handwritten analyzer per tool.** Rejected: that is exactly the duplication that produced
  the inert shell rules, and it cannot be checked by the compiler.
- **Keep `ModifyArgumentSchema(JsonObject)` over the whole schema.** Rejected: it let a tool rewrite
  the argument set and the `additionalProperties` of a schema the descriptor had already derived, so
  the card and the model could see different arguments.
- **Declare the whole `ToolArgumentKind` with an attribute (`[ToolArgumentKind]`).** Rejected: it is a
  second type classifier that can contradict reflection (`Object` on an `int`). Only what the JSON
  type cannot say - being a path - may be declared.
- **Let a rule address the main argument by name as well.** Rejected: `command:git status:*` duplicates
  what the unnamed literal already matches, and for the shell it would mean a second, contradicting
  view (the whole line versus its split parts). It also forced new handwritten analyzer code.
- **Introduce `Choices` data instead of the hook.** Not now: the hook covers all three existing cases
  (addon kinds, database connectors, fetch levels). If a fourth case needs values without a schema
  shape, `Choices` is the natural replacement.
- **Refuse a bare (non-object) argument root.** The first cut threw `NotSupportedException`, which
  conflated "cannot describe it" with "cannot call it". The descriptor and the binder now handle it;
  only a producer that authors such a schema is left (MCP would have to send one, the frontmatter
  parser would have to accept one).
- **Keep `ToolMethodBinder.Describe` returning the schema plus the parameter metadata.** Replaced by
  `DescribeArguments`: the parameter dictionary was the second description, and the descriptor is
  what every consumer wanted.
