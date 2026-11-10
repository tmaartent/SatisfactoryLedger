# Step 32 · A belt that brings nothing leaves its share (BR-A1)

**Goal:** 15 of your 20 insulated crystal oscillator manufacturers were reported at exactly 50%, starved of quartz crystal. In your game they aren't short of quartz. Branch `fix/idle-machine-inputs`.

## How it was found

1. **Supply was plenty:** 72 quartz constructors at 100% make about 1300 a minute, and those 15 manufacturers need 597.
2. **The sink was wired right:** smart splitter `…2147387948` sends *any* to the manufacturers and *overflow* to the resource sink. The sink only got the leftovers, but the manufacturers' side accepted just 251.6 of the 580.
3. **Each manufacturer accepted half.** Temporary debug output (removed again) showed quartz accepted 19.9 of 39.8, rubber 13.9 of 27.9, circuit boards 2.0 of 4.0. A **fourth belt** (lift `…2146411410`) is connected but brings nothing. A belt that hasn't offered anything "may bring any input", so the machine shared every input's need between the real belt and the empty one, and the empty one's half was never used.

## The fix

The rule from step 31, applied to a machine's belts: **a belt is charged only what it brought last sweep, up to its share.** One that brings less, or nothing, leaves the rest to the belts that do. BR-A1 updated; analysis version 10.

## On your world

- The 7 manufacturers `…2147209xxx`/`…2147210xxx` with a recipe: 50% → **100%**.
- The 8 manufacturers `…21464114xx`/`…21464115xx`: 2 at 100%, the rest short of **circuit boards**. That's a real shortage: 10 board assemblers make 50 a minute, and these manufacturers plus 20 control rod assemblers want about 64. **Confirmed in your game.**

## Tried and dropped

The first idea, that a machine counts its other inputs as what *arrives* rather than what's *offered*, would stop an idle machine taking inputs it can't use. Your quantum encoder `…2146425563` has no time crystals yet takes 180 quartz a minute. But that idea made acceptance and flow chase each other, and the largest part of your world no longer settled in 2,000 sweeps. Reverted; the encoder stays open.

## Tests

Written first: `A_belt_into_a_machine_that_brings_nothing_leaves_its_share_to_the_belts_that_do`. A smelter with an empty second belt ran at 50%; now 100%. **Totals:** 821 .NET tests.

## Points worth noticing

| | Java | Here |
|---|---|---|
| The same rule in two places | Extract a method | Not yet: `Share` works per connection and over priorities, the machine per item over the belts that bring it. If a third place needs it, it becomes one helper |

## Still open

- **An idle machine takes inputs it can't use** (the quantum encoder and its quartz). It needs a rule that doesn't oscillate.
- The time crystals that never reach the encoders: the Ficsonium/quantum area.
- How fast vehicles empty a station (enhancement pile).
