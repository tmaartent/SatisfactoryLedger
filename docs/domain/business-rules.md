# Business rules

Each rule has an ID. **Every rule should end up as one or more tests named after it.** Write the test first, then the code (see [guide step 3](../guide/03-tdd.md)). If a rule changes, change it here first.

All rates are per minute. Solids are items/min, fluids are m³/min.

---

## Blueprint Design

| ID | Rule |
|---|---|
| **BR-B1** | A production step is one recipe on a whole number of identical machines (≥ 1). All machines in a step share **one setting**: the same **clock speed** (under-, normal or overclocked) and the same number of **somersloops**. |
| **BR-B2** | A step's base rate for an item = recipe amount × 60 ÷ cycle time in seconds × clock speed × machine count. |
| **BR-B3** | A blueprint has **declared inputs**: the item rates its input belts or pipes deliver per copy (e.g. 600 iron ore, 400 copper ore). |
| **BR-B4** | **Item balance** for each item = declared input + produced by steps − consumed by steps. |
| **BR-B5** | Balance < 0 → **deficit** (red): the blueprint can't run as designed. Balance ≥ 0 → OK (green). |
| **BR-B6** | A blueprint has exactly **one main product**, and its balance must be > 0. That positive balance is the blueprint's main output per copy. |
| **BR-B7** | Byproducts are **declared** explicitly by the designer (e.g. polymer resin from a refinery). A declared byproduct with balance > 0 is an output. **Any other item with balance > 0 is an undeclared surplus, which is a design error** (e.g. 2.5 surplus iron plates), except a precision surplus (BR-B15). |
| **BR-B8** | Blueprint outputs (main product + byproducts) and declared inputs are what other contexts see, through `BlueprintDesign.Contracts`. Steps stay internal. |
| **BR-B9** | **Clock speed** is between 1% and 250%, in the game's precision (up to 4 decimals, e.g. 33.3333%). It scales inputs **and** outputs linearly. |
| **BR-B10** | Above 100%, each machine needs **power shards**: 1 shard per started 50% above 100% (≤ 150% → 1, ≤ 200% → 2, ≤ 250% → 3). |
| **BR-B11** | **Somersloops** amplify **outputs only**: output × (1 + sloops ÷ slots). Inputs stay the same. A machine holds 0 up to its building's slot count (from the catalog). Buildings without slots can't be slooped. |
| **BR-B12** | A blueprint reports the shards and somersloops it needs per copy. A factory needs that × its stack height. |
| **BR-B13** | A blueprint has **at most one input per item**, and each input and each output (main product and byproducts) arrives or leaves on **one** belt or pipe. Per copy, each of these rates must be **≤ the max capacity** for its form (BR-F5). |
| **BR-B14** | A blueprint with at least one deficit (BR-B5) or undeclared surplus (BR-B7) is **unbalanced**. Otherwise it's **balanced**. Unbalanced blueprints can still be saved and edited, because a blueprint is unbalanced while you build it step by step, but they can't be placed as a factory (BR-F1). |
| **BR-B15** | An undeclared surplus that is **unavoidable at the game's clock precision** is **not** an error. It's unavoidable when it's smaller than the output change that lowering any producing step by the smallest clock step (0.0001%) would cause. That lower setting would create a deficit. The UI shows it as green with a note ("precision surplus"). |
| **BR-B16** | Each step in a blueprint has a **unique recipe**, so the recipe identifies the step. A step's output can be the input of one or more later steps. The balance is calculated per item (BR-B4), so step order is presentation only and doesn't affect the result. |

**Example (clock speed and sloops):** an Assembler on Stitched Iron Plate (base 18.75 iron plate + 37.5 wire → 5.625 RIP per minute), at 150% with 1 of 2 slots slooped:
- inputs = 18.75 × 1.5 = 28.125 iron plate and 56.25 wire
- output = 5.625 × 1.5 × (1 + ½) = 12.65625 RIP
- needs 1 power shard and 1 somersloop

**Example:** "Reinforced Iron Plates" with declared inputs of 600 iron ore and 400 copper ore. Steps: smelters → iron ingots, constructors → iron plates, smelters → copper ingots, constructors → wire, assemblers → RIP (Stitched Iron Plate alternate). Each intermediate item should balance to exactly 0 or close to it, and RIP balances > 0, which is the output.

---

## Factories

| ID | Rule |
|---|---|
| **BR-F1** | A factory is one **balanced** blueprint (BR-B14) placed with a **stack height** ≥ 1 (a whole number). |
| **BR-F2** | A factory's inputs and outputs = the blueprint's per-copy inputs and outputs × stack height. |
| **BR-F3** | For every input and output, the total rate must fit the **max capacity** for its form (solids → belt, fluids → pipe, BR-F5). Otherwise the stack height is rejected. |
| **BR-F4** | **Max stack height** = ⌊ min over all inputs and outputs of (tier capacity ÷ per-copy rate) ⌋. |
| **BR-F5** | Everything is assumed unlocked. **Max capacity** = the highest tier in the catalog: belt Mk.6 = 1200/min, pipe Mk.2 = 600 m³/min. There is no world setting for this. |
| **BR-F6** | Editing a blueprint is always allowed, even when factories are built from it. If the edit makes the blueprint **unbalanced** (BR-B14), or makes a factory's stack exceed max capacity (BR-F3), those factories are **flagged**, not blocked. The flag clears automatically when the problem is fixed. |
| **BR-F7** | While flagged for an unbalanced blueprint, a factory keeps using the **last balanced inputs and outputs** of its blueprint, so supply line analysis stays meaningful during editing. Once the blueprint is balanced again, the factory takes over the new inputs and outputs and its lines are re-analysed. |

**Example:** 600 iron ore per copy with Mk.6 belts (1200) → max stack height 2. A third copy is rejected by BR-F3.

Because of BR-B13, a single copy always fits, so the max stack height is always ≥ 1, and BR-F3 only comes into play from the second copy onward.

---

## Supply lines

A supply line is a single belt or pipe for **one item**. It starts at a **source** and passes an ordered list of **points**. Each point is a **tap** (a factory takes its demand) or an **injection** (a second source merged in by a priority merger).

| ID | Rule |
|---|---|
| **BR-S1** | A line carries one item and has a capacity: the max capacity for its form (BR-F5). |
| **BR-S2** | Flow at the start = min(source rate, capacity). Source rate above capacity is **stranded**. |
| **BR-S3** | **Taps (factory or depot, BR-S14) behave like a manifold: no tap has priority over another**, on belts and pipes alike. Consecutive taps between two injections (or the line start/end) form a **section**. If the section's flow covers the section's total demand, every tap gets its full demand and remaining = flow − total demand. If not, the shortage is **shared proportionally**: each tap receives flow × (its demand ÷ total demand), its **deficit** is the difference, and the remaining flow is 0. |
| **BR-S4** | **Injection (priority merger):** primary = remaining flow on the line (always consumed fully). The secondary is drawn up to the free capacity: drawn = min(secondary rate, capacity − primary). Flow after = primary + drawn. |
| **BR-S5** | **Overflow** of an injection = secondary rate − drawn. It can be split off as the **source of a new supply line**. Overflow that isn't routed anywhere is **stranded capacity**. |
| **BR-S6** | The flow remaining after the last point is **available for future factories**. What happens to it today depends on the line type: BR-S12 (fluids), BR-S15 (raw resources on belts), BR-S16 (non-raw items on belts). |
| **BR-S7** | A line source is one of: a resource supply, a factory's output (main product or byproduct), or the overflow of an injection. |
| **BR-S8** | Each output item of a factory is the source of exactly one supply line. "Factory B consumes A's output" is modelled as B tapping A's output line. |
| **BR-S9** | A factory's total supply of an input item = the sum of its taps on all lines for that item. Total supply < demand → the factory shows a deficit (red). |
| **BR-S10** | Lines must not form a cycle through overflow sources. |
| **BR-S11** | **Fluid lines (pipes) have no injections**, only taps. So there's no priority merger and no overflow line for fluids. Taps consume until the demand is met, the source is exhausted, or the pipe capacity is reached, whichever comes first. |
| **BR-S12** | What's left on a fluid line after the last tap is **unused supply**, not "available for future". |
| **BR-S13** | When a fluid line has unused supply, the analysis gives **underclock advice** for the source: advised clock = current clock × total demand ÷ supplied rate. In v1 it's expressed as a percentage of the source's current rate, because a resource supply has no machine details yet. It's rounded **up** to the game's precision (4 decimals), so the advice never creates a deficit. |
| **BR-S14** | A tap's consumer is a **factory** or a **dimensional depot uploader**. A depot tap has a demand equal to its upload rate (entered by the player, because it depends on depot upgrades). Use it for intermediates that are also building materials. |
| **BR-S15** | Leftover flow of a **raw resource** on a belt is meant to feed the next factory that needs it: it continues as the **primary input of a priority merger** (BR-S4), topped up by a new source. Until a factory taps it, the analysis reports it as **unallocated leftover** (available for future), not as waste. |
| **BR-S16** | A belt line carrying a **non-raw item** must end in an **AWESOME Sink**. Leftover flow is then sunk, and remains available for a future tap placed before the sink. A non-raw line with leftover flow and no sink is an **error** (it will back up into the factory). |

### Shortage example (BR-S3)

Iron ore, source 600, one section with two taps.

| Tap | Demand | Receives (600 ÷ 800 = 75%) | Deficit |
|---|---|---|---|
| Factory P | 300 | 225 | 75 |
| Factory Q | 500 | 375 | 125 |

Both factories show red. The order of P and Q makes no difference. A fluid line has no injections, so it's always one single section.

### Fluid example

Water, pipe Mk.2 (capacity 600). The source supplies 600 at 100%.

| Point | Flow in | Action | Flow out |
|---|---|---|---|
| Source | — | — | 600 |
| Tap Factory X | 600 | −250 | 350 |
| Tap Factory Y | 350 | −250 | 100 |
| *end* | | | **100 unused** (BR-S12) |

Advice: underclock the source to 500 ÷ 600 = 83.3333…% → rounded up to **83.3334%** (BR-S13). That supplies 500.0004/min, so it never comes up short.

### Worked example (the case that started it all)

Iron ore, capacity 1200.

| Point | Flow in | Action | Flow out | Notes |
|---|---|---|---|---|
| Source: Line A (resource supply 1200) | — | — | 1200 | |
| Tap Factory 1 | 1200 | −300 | 900 | |
| Tap Factory 2 | 900 | −400 | 500 | |
| Injection: Line B (resource supply 1200) | 500 | primary 500 + drawn 700 | 1200 | overflow 500 → **Line C** (BR-S5) |
| Tap Factory 3 | 1200 | −900 | 300 | |
| *end* | | | **300** | available for future (BR-S6) |

**Line C** (source: overflow of Line B, 500) → 500 available for future factories.

If Line C isn't routed, the analysis reports **500 stranded capacity**.

### Steady-state note

The analysis assumes every tap draws its full demand and that "future" capacity is being drawn as well. If downstream draws less, the belt backs up, the merger draws less from the secondary, and the **real overflow is larger**. So the analysis reports a **guaranteed minimum** overflow. Name it that way in the API and the UI.

---

## Layout suggestions

| ID | Rule |
|---|---|
| **BR-P1** | Input: one item, a set of factory demands, available sources, and a capacity. Output: an ordered list of taps and injections in which every demand is met. |
| **BR-P2** | Prefer fewer sources (injections) first, then less stranded capacity. |
| **BR-P3** | A suggestion is advice and is never persisted automatically. The player applies it explicitly. |
| **BR-P4** | For raw resources, a suggestion uses **existing leftovers first** (unallocated leftover at line ends and overflow lines) as the primary input, and only then adds new sources as secondary inputs (BR-S15). |

Example: demands 300, 400, 900 with sources of 1200 each. Order 900, 300 on Line A (exactly full), then inject Line B for the 400 → 800 available, nothing stranded.

---

## Open questions (decide, then update this doc)

- ~~**Q1** Do fluid supply lines support injections?~~ **Resolved 2026-10-26:** no, only taps. Unused supply leads to underclock advice (BR-S11…S13).
- ~~**Q6** Underclock advice for belt lines too?~~ **Resolved 2026-10-26:** no underclock advice for belts. Leftover raw resources feed the next factory as primary input of a priority merger (BR-S15, BR-P4). Non-raw items feed the dimensional depot if needed (BR-S14), and the rest must go to an AWESOME Sink (BR-S16).
- ~~**Q7** Underclock advice without machine details?~~ **Resolved 2026-10-26:** yes. Advice is "reduce the source to X% of its current rate" until the Resource Nodes context exists.
- ~~**Q2** Can a blueprint need more than one belt of the same input item?~~ **Resolved 2026-10-26:** no. One input per item, each ≤ max capacity (BR-B13).
- ~~**Q8** Who owns the unlocked transport tiers?~~ **Resolved 2026-10-26:** everything is assumed unlocked. Max capacity is catalog data (the highest belt and pipe tier), so there's no world setting (BR-F5).
- ~~**Q3** How is a shortage divided over taps?~~ **Resolved 2026-10-26:** manifold behaviour, no priority between taps, for belts and pipes alike (pipes can't prioritise). A shortage is shared proportionally within a section (BR-S3).
- ~~**Q4** Is a surplus intermediate a warning or an error?~~ **Resolved 2026-10-26:** error. Byproducts must be declared, any other surplus makes the blueprint unbalanced (BR-B7, BR-B14).
- ~~**Q9** Is a surplus that's unavoidable at clock precision an error?~~ **Resolved 2026-10-26:** no (BR-B15). It's defined exactly, with no fuzzy tolerance.
- ~~**Q10** Existing factories when their blueprint becomes unbalanced?~~ **Resolved 2026-10-26:** flag the factories (BR-F6, BR-F7).
- ~~**Q5** Mixed settings within one step?~~ **Resolved 2026-10-26:** no. Every machine in a step has the same setting (BR-B1). A step has no machine groups.
- ~~**Q11** Same recipe in two steps?~~ **Resolved 2026-10-26:** no. Each step has a unique recipe, and a step's output can feed several later steps (BR-B16).
