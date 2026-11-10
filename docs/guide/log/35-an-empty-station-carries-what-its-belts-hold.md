# Step 35 · An empty station carries what its belts hold (BR-A9)

**Goal:** the quantum encoder `Build_QuantumEncoder_C_2147171474` (temporal processors) came out in the model at 0%, "starved of excited photonic matter". In your game it runs at 100%, and its photonic matter comes from its own converter. Branch `fix/encoder-photonic-matter`.

## What the model had wrong

1. **Not the photonic matter.** The converter `…2147171471` makes 100 a minute and feeds two encoders (`…474` and `…476`) through a cross junction, 50 each, and that arrived in full in the model too.
2. **The Ficsite Trigon belt was clogged.** The Ficsite Trigons come from four freight platforms (`…2144814378`–`387`), all empty at save time. None had a neighbour holding anything either, so under step 33's rule they still "supplied whatever is asked": everything the machines further on use, Assembly Director Systems included. That phantom offer clogged the encoder's Ficsite Trigon belt (BR-A1).
3. **The label named the wrong input.** With every input offered in full, the model named the first input of the recipe rather than the one that didn't arrive.

The save showed what those platforms carry: their belts hold only Ficsite Trigons, about 850 each.

## The rules

- **BR-A9 (decided 2026-11-10):** an empty station whose neighbours hold nothing carries what's on the belts of its lines in the save. Only a station with nothing to go by supplies whatever is asked.
- **BR-A11, a fix:** a machine offered every input in full but still short names the input that arrives least.

## In the code

- `Logistics.Carrying`: what's on a belt, lift or splitter at save time, from the snapshot's `Contents`.
- `WorldFlow.CargoOfEmptyStations`: the neighbours' stock first; when that's empty, what the station's lines carry.
- `Solver.Run`: the starved item is the scarcest *arriving* input when every input is offered in full.
- BR-A9, BR-A11, glossary *Cargo*, analysis version 13.

## On your world

Run on the saved flow network with the cargo worked out by the new rule (the AppHost stayed off while your game ran): 15 empty stations get a cargo.

| In the model | Before | Now | Your game |
|---|---|---|---|
| Encoders `…2147171474`, `…2147171476` | 0%, "starved of photonic matter" | **100%** | 100% |

## Tests

Written first:
- `WorldFlowTests`: an empty station whose neighbours hold nothing carries what its belts hold.
- `SteadyStateTests`: a machine whose inputs are all offered is starved of the one that doesn't arrive.
- Snapshot: a belt in the world knows what it carries.

**Totals:** 837 .NET tests.

## Points worth noticing

| | Java | Here |
|---|---|---|
| A positional pattern in an `if` | Records patterns, Java 21 | `Scarcest(…) is (var arriving, { } starvedOf)`: deconstructs the tuple, binds both, and checks the item isn't null, in one condition |
| `OfType<T>()` after a lookup | `filter(Logistics.class::isInstance).map(Logistics.class::cast)` | `lines.Select(world.Find).OfType<Logistics>()` drops nulls and other types at once |

## Still open

- A line that merges belts from several sources could carry items an empty station doesn't. Its "what its belts hold" then is the union. No case of it in your world yet.
- Idle machines accepting inputs they can't use; how fast vehicles empty a station.
