# Remove the C# (.csx) scripting surface

Status: ready-for-agent
Type: task
Blocked by:

## Requested

- The C# scripting surface is to be removed, not repaired and not restricted to a subset of the platforms.
- The stated reason: it does not work cross-platform, because it loads and compiles assemblies at runtime, and that is unavailable on mobile and in the browser build.
- Removal covers every kind that accepts a C# script: scriptable tools, slash commands, prompt contexts, and the model-facing "execute C# script" tool.
- After removal, no Roslyn assembly may remain in the mobile and browser dependency graphs.

## Proposed design

- **The engine family is deleted whole, not guarded.** The failure that prevents the Android build from starting is a static initialiser inside the scripting service. Deleting the type removes the failure; a guard only turns a startup crash into a runtime error and leaves the package in every build.
- **The engine contracts are untouched.** The scriptable tool, command and prompt-context contracts keep their shape and their remaining implementations; one implementation disappears.
- **A `.csx` file becomes an unsupported extension, and that has to be said out loud.** With no engine registered, the file is not discovered at all. A pack that ships one needs an addon diagnostic naming the reason, otherwise the file vanishes silently.
- **The model-facing C# tool is removed in the same change.** It is the weakest of the script execution paths available to the model: a full compilation per call, no state between calls, no introspection comparable to the Lua manuals, and the same reach as the Lua, Python and shell tools it duplicates.
- **Its localization keys are removed with the tool module.** They are embedded resources of the core assembly, but they are consumed by key string only; leaving them behind leaves dead strings.
- **The script-language enumeration loses the C# value** instead of keeping a value no engine reports.
- **The scripting help pages lose the language** in both locales, including the metatool page that lists accepted extensions.
- **Removal is verifiable in three checks.** The core assembly builds without the Roslyn scripting package; the mobile and browser restore graphs do not mention it; a `.csx` file placed in a pack produces the diagnostic above instead of silence.
- Accepted from the request as it stands: total removal, with no desktop-only variant.

## Alternatives considered

- **Relocating the engine into the desktop application**, which is how the Python engine already lives. Refused: it solves the platform failure but keeps a second in-process scripting surface with no known users — the repository, its packs and its agent folders contain no C# script — while adding a per-call compilation cost and a fourth implementation of every future engine contract.
- **Guarding the initialisation in place.** Refused: the type stays in the dependency graph and the package stays in the build; the guard buys a clean error message and nothing else.
- **Keeping it as a personal escape hatch.** Refused: the same reach is available by writing a native tool module, with compile-time checking, tests and no Roslyn in the product.

## Not in scope

- Replacing C# scripts with another in-process language.
- Any change to the Lua engine beyond losing its C# sibling.
- Package references for scripts: the removed engine never supported them, so nothing is being taken away there.

## Comments

- Removal was decided after checking the working tree, the addon packs and the agent folders: no `.csx` file exists anywhere, so no addon needs migrating.
- The surface is nine source files, one tool module and one package reference; the remaining work is localization, documentation and the unsupported-extension diagnostic.
