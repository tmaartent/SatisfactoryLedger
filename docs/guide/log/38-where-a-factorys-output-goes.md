# Step 38 · Where a factory's output goes

**Goal:** on a factory's page, per item: which factories its output goes to, what its receivers would take, what's sunk, and what can't leave. Asked on 2026-11-11, after step 37. Branch `feat/choose-factory-in-3d`, on top of step 37.

## What you decided (2026-11-11)

- **Destinations by factory, shared at mergers.** The output is followed to the ends of its lines and grouped as the receiving factory, *machines of no factory*, the *AWESOME Sink*, a *consumer* by building, *storage and stations*, or *nowhere*. Where other supply merges in, the output goes on in proportion to its share of the mix.
- **Receivers' full demand** for "how much it would need to produce": what the machines and generators it reaches would take at 100%, from whatever source.
- **Surplus is the sink plus what can't leave:** what reaches a sink, and what its output blocked machines would make more at 100%.

All three are BR-A18.

After review (2026-11-11): **Portals are consumers**, not storage. A Portal or Alien Power Augmenter shows under its own building name (`OutputDestinationKind.Consumer`, with `Building` its class). Loading stations and depot uploaders stay *storage and stations*.

After review (2026-11-11): **is the need covered?** On *AI Expansion Servers*, Dark Matter Residue showed *Receivers need* 600 in red, though other encoders on the same lines bring the rest. You chose *Others' share*: two columns next to it, **Others bring** (what the receivers take now, less what this factory brings them) and **Short** (their need at 100%, less what they take now from all sources). Only *Short* is red now; *Receivers need* no longer is.

## How it works

```
output (BR-W4) ─ Over(): what leaves now ─► OutputDestinations.Of(world, start, item, amount, flowInto)
                                              follow logistics; at a split, amount × flow on that side ÷ flow on all sides
                                              ends: machine, consumer, container, belt to nowhere
                                          ─► FactoryOutputs: group the ends (owner factory / no factory / sink / storage / nowhere)
receivers ─────────────────────────────────► OutputDestinations.ReceiversNeed(world, starts, item): Σ demand at 100%, each once
                                              OutputDestinations.ReceiversGet(…, flowInto): Σ what flows into them now
                                              others bring = get − what reaches machines/generators of ours; short = need − get
producers with limit OutputBlocked ────────► can't leave: production at 100% × (1 − utilisation)
```

- **Proportional mixing needs no bookkeeping.** The output keeps its own amount as it travels. Merging doesn't change it; only a split does, by the ratio of what flows on each side. That ratio already includes the other supply, so each side gets the output's share of the mix.
- **The flows come from the stored analysis** (`AnalysisDocument.Into`): what flows into every belt, lift, pipe and container. Machines and generators aren't in it, so where a pipe network feeds them directly, `FlowInto` takes what each uses at the rate it runs (utilisation × demand). No new analysis version: nothing is computed again.
- **Who owns a receiving machine:** the endpoint now loads the world's factories, and `FactoryOutputs.OwnersIn` matches each one's machines in the snapshot (`MachineMatch`, BR-W11), as `ClaimedMachines` does.
- **Loops:** a split side that goes back to where the output passed is left out, so a recirculating line isn't followed forever.

## Files

| Where | What |
|---|---|
| `World.Domain/OutputDestinations.cs` (new) | `Of`: the ends of an output's lines and how much reaches each; `ReceiversNeed` |
| `World/Analysis/FactoryOutputs.cs` (new) | The factory's output per item, moved out of `SnapshotAnalysis` (with `Over`) and extended: destinations, receivers' need, sunk, can't leave |
| `World/Analysis/OutputDestination.cs`, `OutputDestinationKind.cs` (new) | Where some of it goes, and to what kind of end; a consumer carries its building's class |
| `World/Analysis/FactoryOutput.cs` | `ReceiversNeed`, `OthersBring`, `Short`, `Sunk`, `CannotLeave`, `Destinations` |
| `World/Analysis/SnapshotAnalysis.cs`, `AnalysisEndpoints.cs` | `ForFactory` takes the world's factories; the endpoint loads them |
| `web/src/app/world/factory-page.*` | The output table: *Receivers need*, *Others bring*, *Short* (red), *Sunk*, *Can't leave*, *Goes to* (linked to the receiving factory) |
| `api/openapi.json` | The new fields and `OutputDestinationKind` |
| BR-A18, glossary, primer | The rule, the terms, a value tuple as a dictionary key |

## Tests

Domain, written first (`OutputDestinationsTests`, 12): an output on a plain belt all goes to the machine at its end · a splitter shares it as the flow on each side is · where other supply merges in, it goes on in proportion to its share of the mix · a line into a container ends there · a belt that goes nowhere is its own end · a line that loops back is followed once · its receivers would take what their recipes ask for at 100%, whatever else feeds them · a receiver that doesn't use the item asks for none · a sink or container asks for nothing in particular · a generator asks for its fuel and water · a receiver is counted once however many lines lead to it · its receivers get what flows into them now from any source, each once.

Application (`FactoryOutputsTests`, 8, on the test saves): smelter A's ingots go to the factory of constructor C · to *machines of no factory* when C has none · C would take 22.5 a minute (30 at its 75% clock), though smelter B brings 10 of those · 17.5 a minute can't leave smelter A, output blocked at 12.5 of 30 · the iron plate factory's plates go to storage and nothing is sunk · the turbofuel packager's generator would take 7.5 m³ a minute · smelter B brings constructor C the other 10, and C is short of nothing · what goes into a Portal goes to a consumer named after its building (a hand-built world). I probed the saves before writing these, to find cases that show each part.

`WorldAnalysisApiTests`: the factory analysis's JSON with the new fields (`"kind":"storage"`). `FactoryAnalysisTests` and `GeneratorFactoryTests` now compare the output's fields: a `FactoryOutput` holding a list no longer compares by value (primer, *Records for values*).

**Totals:** 886 .NET tests, 76 web tests (890 with step 39). **Not tried in the running app yet.**

## Points worth noticing

| | Java / TypeScript | Here |
|---|---|---|
| A recursive local function with captured state | A private method with the map passed along, or a lambda that can't call itself | `Follow` inside `OutputDestinations.Of` calls itself and adds to `ends` from the enclosing method (primer, *Local functions*) |
| A value tuple as a dictionary key | A `record` key | `Dictionary<(OutputDestinationKind Kind, StoredFactory? Factory), double>` in `FactoryOutputs.Of`. In the primer |
| A property pattern with a guard on two cases | `case Consumer c when c.generation() != null && …` | `Consumer { Generation: { Supplemental: { } water } generation } when water == item` in `ReceiversNeed` |
| `Dictionary.TryGetValue(…, out var x) ? x : dict[key] = []` | `computeIfAbsent` | Get or add in one expression; .NET has no `computeIfAbsent` on `Dictionary` (there's `CollectionsMarshal.GetValueRefOrAddDefault`, but that's for hot paths) |

## Choices worth a second look

1. **Receivers' need is their full demand**, as you chose: a factory that is one of two suppliers of a line shows the whole line's demand. Smelter A alone shows 22.5 needed, while it makes 12.5 and smelter B brings 10 (*Others bring* 10, *Short* —).
2. **A receiver is any machine or generator its lines reach**, whether something flows to it now or not; a machine behind a smart splitter that never sends this item still counts, but asks for none of it unless its recipe uses it.
3. **Machines and generators aren't in the stored flows**: where a pipe network feeds several directly, the split is by what each uses at the rate it runs.
4. **A container in the middle of a line** passes the output on as its outgoing belts carry; what piles up in it isn't counted as storage.
5. **Can't leave counts only the output's producers** (machines reaching an output). An output blocked machine further in, whose product the factory uses itself, isn't in it.
6. **Fluids show as /min** like solids: m³ per minute, as everywhere else on the page.
7. **The sink and consumer groupings go by class name** (`Build_ResourceSink_C`; `Build_Portal_C`, `Build_AlienPowerBuilding_C`) in the application layer; the domain's `Consumer` doesn't say which kind it is. The page names a consumer from the catalog's building names, so it reads *Portal* once the Docs file is re-imported (step 36).
8. **No fixture sends output to a sink in the model**, so *Sunk* is covered only by the domain's ends plus that class name check.
9. **A full container beside a sink still takes half** (seen on your Ballistic Warp Drives factory, 2026-11-11). That's the analysis (BR-A7, *the save overrules full*), not this step: the arriving output is shared evenly over its line's ends. You chose to keep BR-A7 as is; it's in possible enhancements, *A full container beside a sink*.
10. **Short counts any reason a receiver takes less**: a machine starved of *another* input also takes less of this item, so it shows as short here too. *Short* is what they'd take more at 100%, not a shortage of this item alone.
11. **Others bring is a difference**: what the receivers take now, less what this factory's output reaches them with. Mixing at mergers (choice in *How it works*) decides that share; it isn't traced upstream to the other sources.

## Next

Part 2b(2): a live preview of the traced factory while choosing in 3D.
