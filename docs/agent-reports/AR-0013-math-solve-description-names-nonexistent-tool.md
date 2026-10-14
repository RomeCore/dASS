# AR-0013 - math-solve's description sends the caller to a tool that does not exist

- **Status:** open
- **Severity:** friction
- **Lane:** agent
- **Surface:** tool `math-solve` (description)
- **First seen:** 2026-10-14

## What I expected

The description of `math-solve` ends with a pointer to its sibling:

```
For complex roots, use 'math-solve-complex'.
```

Read as a contract, `math-solve-complex` is the exact name of a tool I can call.

## What happened

There is no tool with that name. Calling it returns:

```
Tool was not found: math-solve-complex
Valid tool names: ... math-calculate, math-solve, math-solve_complex, ...
```

The registered name is `math-solve_complex` - with an **underscore**, not a hyphen. The environment answers helpfully by listing the real names, which is the only reason the mistake is recoverable at all.

The sibling's own description is imprecise in the other direction: it demonstrates the call as `solve_complex(x^2 + 1 = 0, z)`, dropping the `math-` prefix entirely, so neither description states the name the tool is actually registered under.

## Why it costs

- **A call is burned on a name that cannot work.** The description is the only place a caller learns this tool exists, and what it teaches fails on the first attempt.
- **The error reads as a capability gap, not a typo.** `Tool was not found` is the same message a genuinely absent tool produces. A caller that does not scroll the valid-name list can conclude the environment has no complex root solver and report "not supported" - a wrong answer caused by a documentation defect.
- **It arrives at the worst moment.** The pointer is read *because* a real solve came back empty ("No real roots found in the specified range"), so the caller is already following a fallback path when the fallback turns out to be advertised under a wrong name.
- **The wrong spelling is systematically plausible.** The tool family mixes separators (`math-calculate`, `math-solve`, `math-solve_complex`), so a hyphen is exactly what a caller would guess; nothing in the failing description contradicts it.

## Repro

1. Call the tool named `math-solve-complex` with `equation` = `x^2 + 1 = 0`.
   Observed `Tool was not found: math-solve-complex`, followed by a list of valid names that contains `math-solve_complex`.
2. Compare against the description text of `math-solve`, which spells it `math-solve-complex`.

## Witnesses

- **2026-10-14** - session that inventoried the mathematics tools - an agent restricted to the single tool `math-solve-complex` (spelled as the description spells it) was unable to call anything; the runtime answered `Tool was not found`, listing `math-solve_complex` among the valid names.

## What I did instead

Used the underscore spelling, taken from the `Valid tool names` list in the error itself. The recovery cost is one call *because* the error enumerates the registered names - without that list the same moment would cost the whole feature.

## Proposed fix

1. **Correct the spelling in the description** (`math-solve-complex` -> `math-solve_complex`). Traced to `src/LLMDesktopAssistant/Tools/Implementations/MathematicsToolModule.cs:72`, inside the `math-solve` description block.
2. **Consider renaming the tool instead**, so the family is consistent at every level: `math-calculate`, `math-solve`, `math-solve-complex`. Either direction removes the mismatch; today the underscore is the odd one out and there is nothing in the UI to signal why.
3. **State the literal registered name in the sibling's own description.** It currently shows only the call form `solve_complex(...)` (line 89), so neither description contains the string a caller must type.

Worth noting for whoever fixes this: the localization keys are already correct - `name.math-solve_complex` and `description.math-solve_complex` in both `iv/tools.loc` and `ru-RU/tools.loc` - so the typo is confined to the raw `Description` string in code, not duplicated across locale files.

## Unverified

- No other tool descriptions were audited for this class of typo, so this may not be the only instance in the toolset.
- Whether the raw `Description` string or the localized `description.math-solve` key takes precedence in what the model actually receives was not determined; the typo was read from the raw string and the wrong name was confirmed against the running app, but the two were not traced to each other.
