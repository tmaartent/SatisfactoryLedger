# Step 31 · A merger shares by what its inputs send (BR-A5)

**Goal:** step 30 left the Alclad sheet assemblers (`…2147346xxx`) taking only 416 of the 600 aluminium ingots a minute they need, while the smelters feeding them were held back. With smelters backed up the belt is full, and a full manifold feeds every assembler, so the model was wrong somewhere. Branch `fix/sheet-manifold`.

## How it was found

Temporary debug output in the solver showed acceptance, spare, offers and flow on chosen connections. It was removed again.

1. **The acceptance downstream was fine.** Smart splitter A (`…2146728051`) accepted 615.7: 15.7 for the full ingot drone port on *any*, and 600 for the sheets on *overflow*, through smart splitter B.
2. **The supply was short.** Only 432 came from the smelter chain: loop C's 8 smelters (`…2147415412`–`419`) merge onto one line, and the 8 smelters `…2147442xxx` feed into the start of that line.
3. **The cause was the mergers.**
   - Smelter `…419` is short of scrap and makes 16.5, but it **offers** 75. A belt's offer isn't capped by its capacity, and bottleneck detection relies on that.
   - The merger shared its 90.7 evenly by offers: 45.3 for the smelter, 45.3 for the line behind it.
   - The rule "an input may take what the others leave" counted what the others were *given*, not what they *sent*. So the 28.8 the smelter didn't use never reached the line behind.
   - That repeated at every merger, halving, until the `…2147442` smelters got under 1 a minute and showed "output blocked", while the sheets were short.

## The fix

`Solver.Share`: an input is charged only what it sent last sweep, up to its share. One that sends less than its share leaves the rest to the others, whatever it offers. That's the game's merger: it takes from whichever input has items. Only firm acceptance is charged this way; spare keeps its rule. BR-A5 updated; analysis version 9.

## On your world (snapshot `01a1c66b…`)

| | Step 30 | Now | Your game |
|---|---|---|---|
| Alclad sheet assemblers | 12–14 of 20 | **20 of 20** | all run |
| Loop C (`…2147415xxx`): refineries / smelters | 72% / partly starved | 95% / 7 of 8 at 100% | |
| Loop B (`…2146737xxx`) refineries | 0% | 24% | runs, a bit blocked |
| Casing assemblers | many blocked | 40 of 60 at 100%, the rest blocked by the full stations | some blocked |
| Machines running / starved | 1959 / 220 | **2078 / 144** | |

All parts settle. Loop B stays more blocked than in your game. That follows from the casing stations' arriving rate, a lower bound until we know how fast the trains empty them (enhancement pile).

## Tests

Written first: `A_merger_input_that_sends_less_than_it_offers_leaves_the_rest_to_the_others`. A smelter behind a 15-ore belt offers 30 ingots but sends 15; the merger's 60 go 15 to it and 45 to the other line (before: 30 and 30). **Totals:** 820 .NET tests.

## Points worth noticing

| | Java | Here |
|---|---|---|
| A collection expression with an index | A stream with `IntStream.range` | `[.. inputs.Select((edge, k) => …)]`: `Select`'s second parameter is the index |
| Comparing arrays by reference | `==` on arrays, same in Java | `accept == _accept` tells the firm acceptance from the spare one: the same method fills either array |

## Technical choices you can overrule

- "What it sent" is last sweep's flow. In the first sweep nothing has flowed yet, so every input may take everything once, and later sweeps settle it.

## Still open

- How fast vehicles empty a station (enhancement pile): it decides loop B and the casing lines.
- The Ficsonium loops.
