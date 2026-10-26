# AR-0012 - math-calculate prints results in the machine's culture, which its own parser rejects

- **Status:** fixed
- **Fixed:** 2026-10-26 - every branch of `FormatComplex` and the root formatter in `Solve` now use the invariant culture.
- **Severity:** friction
- **Lane:** agent
- **Surface:** tool `math-calculate`
- **First seen:** 2026-10-14

## What I expected

`math-calculate` takes one string expression and returns the evaluated value. Its grammar, as the parser itself prints it back when it rejects a character, is point-decimal and has no group separators:

```
',' is unexpected character, expected one of:
  literal '!'
  number
  keyword 'i'
  ...
```

The descriptions of the whole family write every decimal with a point (`sin(x) - 0.5`, `solve_complex(x^3 - 1 = 0, x, -5, 5, -5, 5, 0.5)`). Read as a contract, a returned number should be written in the same notation the tool accepts, so that the result of one call can be passed as the argument of the next.

## What happened

On a machine whose culture uses a comma as the decimal separator (ru-RU here), results come back with a comma - and a result fed straight back in fails to parse:

```
math-calculate("1 + 2 * -(3^2 / e)")
  -> -5,621829941085962

math-calculate("-5,621829941085962 + 0")
  -> Error parsing expression: An error occurred during parsing:

     The line where the error occurred (position 2):
     -5,621829941085962 + 0
       ^ line 1, column 3

     ',' is unexpected character, expected one of:
     ...
```

Complex results are affected in both parts of the pair:

```
math-calculate("sin(9+2i)")
  -> 1,5504703893924499 - 3,3045422732208203i
```

The same `,` appears in the UI status line the tool builds alongside the result (`` `expr` = `value` ``), so the localized notation is what a human sees as well.

## Why it costs

- **Every chained calculation pays a transliteration tax.** Using a result as the next argument means rewriting the mantissa by hand - strip the comma, insert a point - and on a 16-digit value that is a transcription risk on top of the wasted attention.
- **The failure is misattributed.** The error is a *parse* error pointing at the expression, so the natural first reaction is to doubt the expression, not the transport. The same misattribution pattern is already recorded for quoting in AR-0002.
- **The output is not reproducible across machines.** The same call returns a different string depending on the user's culture, so a transcript cannot be replayed verbatim and a number copied between sessions on different setups may not parse.
- **The tool contradicts itself within one round-trip.** It rejects `,` on the way in and emits `,` on the way out, so its own output is not valid input to itself.

## Repro

1. On a machine with a comma-decimal culture, call `math-calculate` with `1 + 2 * -(3^2 / e)`.
   Observed `-5,621829941085962`.
2. Pass that exact string back as an expression: `-5,621829941085962 + 0`.
   Observed `Error parsing expression: ... ',' is unexpected character ...`.
3. Confirm the complex form too: `sin(9+2i)`.
   Observed `1,5504703893924499 - 3,3045422732208203i`.

## Witnesses

- **2026-10-14** - session that inventoried `math-calculate` - probing every advertised function; the comma-decimal output appeared on the very first call, and the round-trip failure was reproduced directly against the tool.

## What I did instead

Nothing in this session (no chained computation was needed). In real use the workaround is to transliterate every returned value before reusing it: replace `,` with `.` and drop any grouping. A caller that does not notice the notation will instead conclude that its own expression was malformed.

## Proposed fix

1. **Format numbers with the invariant culture.** Traced to `src/LLMDesktopAssistant/Tools/Implementations/MathematicsToolModule.cs:256-265` - `FormatComplex` builds its string from culture-sensitive calls,

   ```csharp
   if (c.Imaginary == 0)
       return c.Real.ToString("G");
   if (c.Real == 0)
       return $"{c.Imaginary}i";
   string sign = c.Imaginary > 0 ? "+" : "-";
   return $"{c.Real:G} {sign} {Math.Abs(c.Imaginary):G}i";
   ```

   `ToString("G")` and the `$"{...}"` interpolations both use `CultureInfo.CurrentCulture`. `ToString("G", CultureInfo.InvariantCulture)` (and invariant interpolation) makes the output match the grammar the parser enforces.
2. **Fix the sibling in the same pass.** `Solve` formats its roots with `r.ToString("G")` at `src/LLMDesktopAssistant/Tools/Implementations/MathematicsToolModule.cs:169`, so `math-solve` emits comma-decimal roots for the same reason and feeds the same problem back into `math-calculate`.
3. **Do not "fix" it on the input side instead.** Accepting `,` as a decimal separator in the parser is ambiguous in this grammar, because `,` is already the argument separator (`integral(x^2, x, 0, 10)`). The output side is the only side that can be changed safely.

## Resolution

1. **`FormatComplex` is fully invariant.** All three branches go through `ToString("G", CultureInfo.InvariantCulture)`, and a NaN part short-circuits to the literal `NaN` instead of leaking the localized word. With a comma-decimal culture `gamma(2+i)` answers `0.6529654964201672 + 0.34306583981654626i` and `gamma(-1)` answers `NaN` - both round-trip through `math-calculate` (the parser accepts `infinity` in any case, so an infinite result does too).
2. **The sibling is fixed with it.** `Solve` and `SolveComplex` format through the same helper, so roots lost their commas as well; `Solve` keeps `r.ToString("G", CultureInfo.InvariantCulture)` directly.

What made this survive the first pass and cost a second one: only the pure-real branch had been switched to the invariant culture, so real results read correctly while complex ones still carried commas and localized NaNs - a half-fix that looks green from the outside and is exactly the case the report was filed about.

## Unverified

- Only one culture (ru-RU) was observed. The claim that the notation follows the machine's culture is read from the source, not tested on a second locale.
- Whether the same culture-sensitive formatting leaks into other tool results in dASS was not audited.
- The risk that a caller misreads a comma-decimal number as a grouped integer was not observed - it is an inference about a possible cost, not a measured one.
