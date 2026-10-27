# ADR-0004: Exact rational arithmetic for rates

- **Status:** Accepted
- **Date:** 2026-10-26

## Context
Recipe rates are amount × 60 ÷ cycle time, e.g. 3 RIP per 32 s = 5.625/min, or 1 per 9 s = 6.666…/min. Balancing compares sums of such rates with zero. With `double`, 0.1 + 0.2 ≠ 0.3, and a balanced blueprint can show −0.0000001 (red). `decimal` can't represent 1/3 exactly either.

## Decision
We will represent rates as a `Rate` value object holding an **exact fraction** (numerator/denominator, always normalised), in the Shared Kernel. Conversion to decimal happens only at the edges (API response, UI display).

Implementation choices (M1):
- **`BigInteger`, not `long`:** adding fractions multiplies denominators, and a long chain of sums can overflow `long` silently. `BigInteger` can't overflow, and at our sizes it costs nothing noticeable.
- **One internal `Fraction` type, held by composition:** `Fraction` owns the invariants (normalised by GCD, sign in the numerator, denominator never zero). `Rate` and `ClockSpeed` each hold a `Fraction` instead of inheriting from a shared base class. That keeps them separate types (a `ClockSpeed` can't be passed where a `Rate` is expected) and keeps `Fraction` out of the Shared Kernel's public surface.
- **A clock speed is a multiplier, not a rate:** `Rate * ClockSpeed` gives a `Rate`; `Rate * Rate` is not defined.

## Alternatives considered
- **`double` with an epsilon:** every comparison needs a tolerance, and "exactly balanced" loses its meaning.
- **`decimal`:** better, but still inexact for thirds and ninths.

## Consequences
- Balance checks are exact. "Exactly balanced" is a meaningful state.
- We must implement and thoroughly test arithmetic, comparison, equality and normalisation. That's a great first TDD exercise.
- Clock speeds are fractions too: the percentage as entered in the game (up to 4 decimals) → e.g. 333333/1000000. Note that 33.3333% is **not** exactly 1/3. The model then shows the same tiny surplus or deficit the game really has, which is correct behaviour, not a rounding bug.
- API design question: send fractions or decimals? Decide in step 5, e.g. a decimal for display plus an exact `"45/8"` string.
