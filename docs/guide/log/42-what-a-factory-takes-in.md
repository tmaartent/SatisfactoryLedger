# Step 42 · What a factory takes in

**Goal:** an *Input* table on the factory page, with the columns Name, Now, At 100%, Short and Comes from (asked 2026-11-11). Branch: `main`, uncommitted, after step 41.

## What you decided (2026-11-11)

- **At 100% is net**: what all its machines take of the item at 100%, less what its own machines make of it, as for outputs (step 39). A factory whose own smelter makes more ingots than its constructor takes needs none from outside, even if some come back to it over a shared line.
- **Comes from is grouped like Goes to, by building**: another factory (linked), machines of no factory, and the rest by building name (Miner Mk.3, Water Extractor, Freight Platform, …). Shared in proportion at mergers.

Both are BR-A19. *Now* and *Short* follow: what arrives now, and At 100% less Now.

After review (2026-11-11): **each building on its own**. Two Miners merging onto one belt were one *Miner Mk.3* line; you prefer two. A building source now carries its id (`InputSource.Id`), and its name links to it on the map (`?focus=`), as the findings' positions do.

After review (2026-11-11): **Short is what the sources can't bring.** At first it was At 100% less Now, so a factory running at 83% for any reason showed every input a sixth short. You chose *spare, limited by the lines*: Short = At 100% − Now − what its sources could bring more, as far as the belts and pipes between have room. A belt that carries 480 into a 500 need is 20 short, however much the miner has to spare.

After review (2026-11-11, *Crystal Oscillators*): **what overflows into a sink can come instead.** Its Quartz Crystal showed 186 short, though the Quartz Crystals factory sinks 771 a minute: it runs at 100%, so it had no spare by the first rule. The overflow branches off the line at a smart splitter and reaches the sink over a merger. Now the walk also follows the branches off the line to their ends; what a sink takes there may be sent back up its branch, as far as the branch carries it, and on to the inputs. Quartz Crystal: short —. AI Limiter stays 18.65 short: its factory runs at 100% and sinks none.

After review (2026-11-11): **a station has spare by its stock.** Freight Platforms counted no spare, so Rubber from them showed the whole gap. The rate trains bring isn't in the save, so you chose BR-A17 turned round: a station could deliver more as far as its stock lasts 30 minutes at the higher rate, spare = stock ÷ 30 − what it delivers now. You added: a platform buffers into storage containers, so their stock counts too (the containers its line passes through, down to the machines). Crystal Oscillators' Rubber: short —.

## How it works

```
input (BR-W4) ─ Arriving(): what crosses it now ─► InputSources.Of(world, start, item, amount, flowOutOf)
                                                     walk upstream; at a merge, the part each line brings
                                                     starts: machine, generator, provider, container nothing feeds, belt from nowhere
                                                 ─► FactoryInputs: group the starts (factory / no factory / building / nowhere)
all its machines ─ Takes − Makes at 100% ─────────► At 100%, Short
```

- **The walk works in fractions.** Going upstream, each buildable's arrivals are split over the lines that feed it, by what each sends (`flowOutOf`): the stored flow into a belt, pipe or container; for a machine or generator, what it makes at the rate it runs; for a provider, what it delivers. The fractions are worked out first and multiplied by the amount at the end.
- **Loops**: a line that only leads back to where the walk passed brings nothing of its own, so its share goes to the other lines. The outputs' walk (step 38) drops such a part instead. Going upstream, that part came from the same sources as the rest, so dropping it would lose some of what arrives.
- **What crosses an input** is read from the stored flows as for an output: the logistics on either side when it has one way in or out. Otherwise (a machine fed straight from a shared splitter or pipe network), what the machine takes at the rate it runs, shared over its inputs that bring the item.
- **Short within 0.01% of the need is none.** The flow settles within a small tolerance, while the need comes exactly from the recipes. *Rubber* showed Crude Oil 0.007 short of 600 before this.
- **What the sources could bring more** is a maximum flow (`InputSources.CouldBringMore`, Edmonds-Karp). The line upstream of the inputs is walked to its sources. Each source sends at most its spare: a provider's unused supply; a machine of another factory, or of none, what it would make more at 100%. Its own machines, stations and containers count no spare (their rate isn't known, or it isn't outside supply). Each belt, lift or pipe passes at most its room: its capacity less what it carries now, of all items. A splitter, merger or pipe network has no limit. Each piece of logistics is two points, in and out, with its room on the edge between, so a merger shared by two sources gives them its room once.
- **Sending back from a sink** is the residual step of a maximum flow: a branch's logistics may carry back what they carry forward now (the way back of their in-to-out edge), and the sink is a source of what it takes. What a splitter, merger or pipe network carries isn't stored, so it's what the logistics feeding it carry; a belt, lift or pipe without a stored flow carries nothing. That last point was a bug on the way: reading "no entry" as "sum what feeds it" made an empty overflow belt carry all 50 AI Limiters past its smart splitter.
- `FactoryOutputs.Makes`, `Takes` and `OwnersIn` are now `internal`, so `FactoryInputs` uses the same ones.

On your world, in the model: *Cooling Systems* takes Heat Sinks from *Heat Sinks* (72), and Packaged Nitrogen Gas and Rubber from Freight Platforms. *Rocket Fuel Power Plant* takes 2,999 Rocket Fuel from *Rocket Fuel* and is 600 short. *Ficsite Trigons* takes Reanimated SAM from *Reanimated SAM* and is 170 short. Checked through the running app.

## Files

| Where | What |
|---|---|
| `World.Domain/InputSources.cs` (new) | `Of`: the starts of an input's lines and how much comes from each; `CouldBringMore`: the maximum flow of the sources' spare |
| `World/Analysis/FactoryInputs.cs` (new) | Per item: now, at 100%, short, comes from |
| `World/Analysis/FactoryInput.cs`, `InputSource.cs`, `InputSourceKind.cs` (new) | The response |
| `World/Analysis/FactoryAnalysisResponse.cs`, `SnapshotAnalysis.cs` | `Input`, next to `Output` |
| `World/Analysis/FactoryOutputs.cs` | `Makes`, `Takes`, `OwnersIn` internal |
| `web/src/app/world/factory-page.*` | The Input table, *Nothing comes into this factory.* when empty; a building source links to the map |
| `api/openapi.json` | The new types |
| BR-A19, glossary | The rule, *Source* |

## Tests

Domain (`InputSourcesTests`, written first): what arrives on a plain belt all comes from the machine at its start · where lines merge, it comes from each in proportion to what each brings · a container nothing feeds is where it comes from · a line that loops back is followed once (and its part goes to the smelter, not lost) · what its sources have to spare can come when the line has room · a full belt on the way lets nothing more through · what overflows into a sink could come instead · only as far as its line has room · what another machine takes is not there to take · the tightest room on the way decides (a 480 belt into a 500 need) · sources sharing a narrow belt share its room · each of its inputs takes what its own line can bring.

Application (`FactoryInputsTests`, on the test save, and one hand-built world): constructor C takes 22.5 ingots now and at 100%, short of none · they come from the *Smelting* factory (12.5) and machines of no factory (10) · ore from a miner comes from that building, by its id · two miners merging onto one belt are two sources (hand-built) · it is not short when its sources have the rest to spare and the line has room for it (a hand-built smelter standing still with its ingots nowhere to go; this test fails with the old *Short*) · it is not short of what overflows into a sink over a merger (a shared line; failed before) · a sink on an overflow that carries nothing has nothing to give (failed before: the AI Limiter bug) · a platform whose stock lasts half an hour at the higher rate has it to spare · a platform has to spare only what its stock keeps up for half an hour (300 ore: 10 a minute, 20 short) · the containers a platform unloads into count with its stock (all three failed before) · at 100% a factory of smelter A and constructor C needs no ingots from outside, though 22.5 arrive over the line smelter B shares.

The 0.01% tolerance has no test of its own: none of the saves shows it, only your world. **Not tried in a browser yet.**

**Totals:** 918 .NET tests, 79 web tests.

## Points worth noticing

| | Java / TypeScript | Here |
|---|---|---|
| A recursive local function returning a value | A private method | `Fractions` inside `InputSources.Of` calls itself and captures `world` and `flowOutOf`; a `static` local function can't capture, so the tolerance check `Short` is marked `static` and this one isn't |
| `x is not { } outside` | `if (x == null) …; var outside = x;` | Tests and names in one pattern; `outside` is in scope after the `if` when the `if` returns |

## Choices worth a second look

1. **The column is called *Name*** as you asked; the output table calls it *Item*. Say if they should match.
2. **A merger fed straight from a splitter** (no belt between) shares equally: splitters and mergers aren't in the stored flows.
3. **A container that fills** passes on what flows into it as what it sends, so a filling container counts a little more than it sends on.
4. **Short per item counts belts' room for all items**: a belt carrying two items gives its room to each when they're worked out one at a time. A mixed belt can look roomier than it is.
5. **A station's spare is a snapshot of its stock**: a platform that just got a train looks rich, one about to get one looks poor. A container a station's line passes through counts for every factory that line reaches.
6. **Many buildings make a long list**: a factory fed by twenty Water Extractors lists twenty, most first. Say if those should fold up past a few.
7. **A factory's own output that comes back** to it over a shared line shows under its own name in *Comes from*.

## Next

Part 2b(2): a live preview of the traced factory while choosing in 3D.
