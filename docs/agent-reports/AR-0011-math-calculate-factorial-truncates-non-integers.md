# AR-0011 - math-calculate silently truncates non-integer `factorial` arguments

- **Status:** open
- **Severity:** silent
- **Lane:** agent
- **Surface:** tool `math-calculate`
- **First seen:** 2026-10-14

## What I expected

`factorial` is advertised in the description with no qualifier at all, in a list of ordinary functions:

```
Normal:

asinh, acosh, tan, atanh, atan2, cbrt, sign, floor, ceil, round, trunc, mod,
gamma, factorial, integral, derivative
```

Every numeric value in this language is a floating-point number - the descriptions of the tool and its siblings spell that out with point decimals (`sin(x) - 0.5`, `solve_complex(x^3 - 1 = 0, x, -5, 5, -5, 5, 0.5)`) - and the same list already ships `gamma`. Read as a contract, `factorial(0.5)` is the factorial of `0.5`, i.e. `Γ(1.5) ≈ 0.8862269254527580`; if the domain were integers only, the description would have to say so.

## What happened

The fractional part is discarded, silently, and a successful result comes back:

| call | returned | correct value |
|---|---|---|
| `factorial(0.5)` | `1` | `Γ(1.5) ≈ 0.8862269254527580` |
| `factorial(2.9)` | `2` | `Γ(3.9) ≈ 5.3993` |
| `0.5!` (operator form) | `1` | `Γ(1.5) ≈ 0.8862269254527580` |
| `factorial(10)` | `3628800` | `3628800` |
| `5!` | `120` | `120` |

Both entry points are affected - the `factorial(...)` call and the postfix `!` operator - which confirms they share the same evaluation path rather than being two independent features.

## Why it costs

- **`1` is a plausible answer.** "Factorial of something small is 1" is close enough to intuition (`0! = 1`, `1! = 1`) that nothing about the response reads as truncated, so the wrong value gets reported.
- **`2` for `factorial(2.9)` is not a rule a caller could predict**: it is not rounding (`round(2.9)` is `3`, `3! = 6`), it is not the nearest valid factorial, and it is not an error. It is simply the input with its decimal tail cut off.
- **The tool provides the fix and doesn't use it.** `gamma` sits in the same advertised list, so a caller that trusts the description has no reason to route around `factorial` - the correctness cost is the wrong number *plus* the calls spent discovering it.

## Repro

1. Call `math-calculate` with `factorial(0.5)`.
   Observed `1`; expected `0.8862269254527580` (equivalently `gamma(1.5)`).
2. Call it with `factorial(2.9)`.
   Observed `2`; expected `Γ(3.9) ≈ 5.3993`.
3. Confirm the operator form goes the same way: call it with `0.5!`.
   Observed `1`.

## Witnesses

- **2026-10-14** - session that inventoried `math-calculate` - probing every function its description advertises. `factorial(0.5)` returned `1`, `factorial(2.9)` returned `2`, and the `0.5!` operator form returned `1` on a direct re-check.

## What I did instead

Read `factorial` as integers-only and would call `gamma(x + 1)` for anything non-integral. That workaround is only available to a caller who already knows the truncation happens - which is the whole problem.

## Proposed fix

1. **Route non-integers through the real gamma that already exists on the same class.** Traced: the caller at `src/LLMDesktopAssistant/Calculation/Ast/FunctionCallEntity.cs:93` casts before the call,

   ```csharp
   "factorial" => new Complex(ComputeFactorial((int)args[0].Real), 0),
   ```

   and `ComputeFactorial` (lines 175-181) then just multiplies `2..n`. `ComputeGamma` (line 151) already implements the Lanczos approximation, so `if (n != Math.Floor(n)) return ComputeGamma(n + 1);` at the top of `ComputeFactorial` yields the standard answer for free.
2. **Cheapest alternative:** state the integer-only domain in the description *and* reject a non-integer argument with a `MathEvaluationException` instead of truncating it - a loud failure at least stops the wrong number from being born.
3. **Worth a look at the same time:** `ComputeFactorial` takes an `int` while its loop counter is a `long`, so the cast happens one layer above the domain check. Whether a non-integer should even reach that function is the design question underneath the bug.

## Unverified

- `Γ(3.9) ≈ 5.3993` was derived from the definition of `Γ`, not produced by the tool - the tool has no way to return it today.
- Whether `factorial` accepts a complex argument at all was not checked, nor how the `(int)` cast behaves for magnitudes outside `int` range (e.g. `factorial(1e10)`).
- The behaviour at a large integer argument was not probed; the loop stops when the running product reaches `+∞`, but that was read from the source, not observed.
