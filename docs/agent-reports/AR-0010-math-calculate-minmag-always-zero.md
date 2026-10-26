# AR-0010 - math-calculate's `minmag` always returns 0

- **Status:** fixed
- **Fixed:** 2026-10-26 - both folds lost the `Complex.Zero` seed.
- **Severity:** silent
- **Lane:** agent
- **Surface:** tool `math-calculate`
- **First seen:** 2026-10-14

## What I expected

The tool's description lists `minmag` under "Complex" functions, right next to `mag` and `conjugate`:

```
Complex:

mag, conjugate, minmag, maxmag, compgamma
```

`mag` is the modulus and `conjugate` is the complex conjugate, so the plain reading of `minmag(a, b, ...)` is "the argument whose magnitude is the smallest" - and of `maxmag` "the argument whose magnitude is the largest". For `minmag(3+4i, 1+1i)` that is `1 + 1i`, whose magnitude is `√2 ≈ 1.414` against `5`.

## What happened

`minmag` returns `0` - for every input I tried, including the single-argument case:

| call | returned | expected |
|---|---|---|
| `minmag(3+4i, 1+1i)` | `0` | `1 + 1i` |
| `minmag(3+4i)` | `0` | `3 + 4i` |
| `maxmag(3+4i, 1+1i)` | `3 + 4i` | `3 + 4i` |

No error and no warning: a plain `0` comes back as a successful result. `maxmag` answers correctly, which is exactly what makes the failure invisible - the pair looks symmetric and working, so a caller has no reason to suspect the third row of that table.

## Why it costs

- The result *looks* like an answer. A caller has no reason to doubt `0` and will carry it straight into its conclusion ("the smallest magnitude is 0") without ever learning the tool fabricated the value. This is the class of failure that is only found much later, when the conclusion is already downstream.
- The correct value is never returned anywhere in the response, so there is nothing to compare against and nothing to notice.
- It cannot be worked around from inside the toolset: no other tool takes the minimum magnitude of a set of complex numbers, so the only route is to call `mag()` once per argument and compare by hand - one extra call per argument, and only if the caller already knows `minmag` is broken.

## Repro

1. Call `math-calculate` with `minmag(3+4i, 1+1i)`.
   Observed `0`; expected `1 + 1i`.
2. Call it with `minmag(3+4i)`.
   Observed `0`; expected `3 + 4i`.
3. For contrast, call it with `maxmag(3+4i, 1+1i)`.
   Observed `3 + 4i` - correct, so the defect is specific to the minimum.

## Witnesses

- **2026-10-14** - session that inventoried `math-calculate` - probing every function its description advertises for behaviour contradicting that description. `minmag(3+4i, 1+1i)` returned `0` on the first call; the single-argument form was re-checked directly and also returned `0`.

## What I did instead

Nothing, in this session - there was no consumer of `minmag`. In real use the only workaround is to stop calling it and compose the comparison out of `mag()` calls, which the caller can only choose once it already knows the function lies.

## Proposed fix

1. **Drop the `Complex.Zero` accumulator seed** so the arguments alone decide the outcome. Traced to `src/LLMDesktopAssistant/Calculation/Ast/FunctionCallEntity.cs:88-89`:

   ```csharp
   "maxmag" => args.Aggregate(Complex.Zero, (a, b) => Complex.MaxMagnitude(a, b)),
   "minmag" => args.Aggregate(Complex.Zero, (a, b) => Complex.MinMagnitude(a, b)),
   ```

   `Complex.Zero` is being folded into the compared set as if it were an argument, and its magnitude is the smallest a complex number can have - so the minimum is `0` for every input, always. Seeding with `args[0]`, or using `args.Aggregate(Complex.MinMagnitude)` / `args.MinBy(a => a.Magnitude)`, makes both functions answer the question they are named after.
2. **Decide the single-argument case deliberately.** Today `minmag(3+4i)` is `0` as well; if the intent was a no-op it should be stated, because the `0` is indistinguishable from a real result.
3. **Say in the description whether the return value is the argument or its magnitude.** `mag` and `minmag` sitting side by side in the same list invites both readings, and the caller cannot tell from the output which one it received.

## Resolution

The seed was the whole bug: `Complex.Zero` sat in the compared set as if it were an argument, and nothing complex has a smaller magnitude than zero, so `minmag` answered `0` for every input. Both folds now run seedless:

```csharp
"maxmag" => args.Aggregate(Complex.MaxMagnitude),
"minmag" => args.Aggregate(Complex.MinMagnitude),
```

That also settles proposed fix 2: the single-argument form returns the argument itself, `minmag(3+4i)` is `3 + 4i` - the no-op reading, not a `0`. An empty argument list now throws instead of answering `0`, because a seedless fold has nothing to return. `maxmag` was rewritten for symmetry, not because it was broken.

Tests: `tests/LLMDesktopAssistant.Tests/Calculation/MagnitudeTests.cs` - ten cases over two- and three-argument folds, negative reals, single arguments and complex values. `minmag(3+4i, 1+1i)` is `1 + 1i`, where it used to be `0`.

## Unverified

- Only non-zero, non-NaN, positive-imagnitude arguments were exercised. Whether `maxmag` breaks for a set containing a NaN argument was not checked.
- The `maxmag` line carries the identical seed but happens to be correct for every non-negative input; whether the maintainer wants both rewritten for symmetry is a judgement call, not an observation.
- Behaviour for an empty argument list (`minmag()`) was not probed; the seedless fold throws `InvalidOperationException`, which the surrounding catch turns into a `MathEvaluationException` - read from the source, not observed.
