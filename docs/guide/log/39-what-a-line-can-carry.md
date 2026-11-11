# Step 39 · What a factory's line can carry

**Goal:** fix the output table of *Cooling Systems* (factory `01a1cb27…`, reported 2026-11-11). Its two Packagers unpack Packaged Nitrogen Gas for its Blenders. Nitrogen Gas showed up as an output (0 a minute now, 900 at 100%, going nowhere), though it never leaves the factory. Branch `feat/choose-factory-in-3d`, on top of step 38.

## What was wrong

Two things, both older than step 38:

1. **The trace (BR-W4) didn't know belts from pipes.** An output's items were *everything its machines make*. The Packagers' belt of Empty Fluid Tanks was listed as carrying Nitrogen Gas too, and each Blender's belt inputs as bringing Water. Two Blenders also have a pipe on their fluid *output* port, which the Cooling System recipe doesn't use. The pipe network behind it ends nowhere, so it was an output "carrying" Cooling Systems.
2. **"At 100%" added up every product of the producers**, also what never leaves. The Packagers are producers (their tanks leave), so their Nitrogen Gas, all taken by the Blenders, was counted.
3. **"At 100%" was gross, not net** (reported on *Rubber*, factory `01a1cb7f…`, 2026-11-11). All 75 machines run at 100% in the model and 1,800 Rubber a minute leaves, yet *At 100%* said 2,333: the rubber its Recycled Plastic refineries take back in was counted as if it could leave.
4. **A dead end was an output** (reported on *Ficsite Trigons*, factory `01a1cbb0…`, 2026-11-11). Its Ficsite Ingot manifold ends in a splitter with a Conveyor Lift on its spare side that leads nowhere. Any owned line ending in nothing counted as an output to nowhere, so Ficsite Ingot was listed (0 now, 0.0002 at 100%: the converters make about what the constructors take).

## What changed

- **A handover carries only what its line can.** `FactoryDefinition.TraceMembers(world, fluids)` now takes the fluids. Each handover keeps the items of its line's form: a pipe, junction, pipe network or fluid buffer carries fluids, everything else solids. Its line is whichever side is logistics. A handover whose line can carry none of its machines' items is dropped: it's no output or input.
- **`Logistics.CarriesFluids`** says which form a piece of logistics carries. The flow (`WorldFlow.CarriesOf`) used the same list privately; it now uses this property, so the trace and the flow can't disagree.
- **Output per item counts only what leaves.** `FactoryOutputs.Of` counts *At 100%* and *Can't leave* only for items that one of its outputs carries.
- **At 100% is net.** Over all its machines (not only those that reach an output): what they make of the item at 100%, less what they take of it at 100% (`Makes`, `Takes`; a generator takes its fuel and water). Never below 0. *Rubber* now shows 1,800 at 100%, as it makes now.
- **Only a container ends a line as a handover.** You chose *no output*: an owned belt, lift or pipe with nothing after it is part of the factory, not an output; only a storage container or fluid buffer at the end of its own line is one (its output buffer). The same on the way in: only a container nothing feeds is an input from nothing, as BR-W4 already said. On your world, 19 factories lost such dead ends (Dark Matter Crystals 12 → 6 outputs, Copper Sheets 3 → 1 inputs); none lost an item from its outputs or inputs, except Ficsite Trigons its Ficsite Ingot.
- BR-W4 says all of these.

On your world: Cooling Systems' outputs went from 20 handovers to 18 (the two dead-end pipe networks are gone). Its output table now lists Cooling System and Empty Fluid Tank only. Its inputs: Heat Sink and Rubber on belts, Water from the two Water Extractors, Packaged Nitrogen Gas on the Packagers' belts. Checked through the running app.

## Files

| Where | What |
|---|---|
| `World.Domain/FactoryDefinition.cs` | `TraceMembers(world, fluids)`; `Trace.Carried` keeps the items of the line's form, or drops the handover |
| `World.Domain/Logistics.cs` | `CarriesFluids` |
| `World.Domain/Flow/WorldFlow.cs` | `CarriesOf` uses `CarriesFluids` |
| `World/Factories/FactoryTraces.cs`, `World/Analysis/SnapshotAnalysis.cs` | Pass the snapshot's fluids |
| `World/Analysis/FactoryOutputs.cs` | Only items that leave; at 100% net of what its own machines take |
| `docs/domain/business-rules.md` | BR-W4 |

## Tests

Domain (`FactoryDefinitionTests`), on a small unpacking world with a spare fluid port: an output on a belt carries only the solids its machines make · a pipe from a fluid port its recipe doesn't use is no output · an input on a pipe brings only fluids, and one on a belt only solids. I switched the filter off once to check that all three fail without it; at first the input test didn't, so it got a belt of plates into the blender.

Also: *a belt of its own that goes nowhere is no output* (a lift off the last splitter) · *a belt of its own that starts at nothing is no input* (a loose belt into a merger). Both failed before the change.

Also: *what its own machines take of an item that leaves is not what it could make more*: a smelter (30 ingots a minute) feeds its own constructor at 50% (15) and the rest leaves, so 15 at 100%, where it said 30 before.

Two older tests used Coal on a pipe as a stand-in for Dark Matter Residue; with pipes carrying fluids only, they now use Water. `TestWorldFactoryTests` passes Water as its fluid.

Application (`FactoryOutputsTests`): *what its producers make for its own machines only is no output*, on a hand-built world: a Packager unpacks Turbofuel for the factory's own Fuel-Powered Generator, and only Empty Canisters leave. The test save's own Packager and generator have no output at all, so a world was built in the test, as `StockFindingsTests` does.

**Totals:** 893 .NET tests (with step 38's additions), 76 web tests (web unchanged).

## Points worth noticing

| | Java / TypeScript | Here |
|---|---|---|
| Dropping nulls from a stream | `.filter(Objects::nonNull)`, and the type stays `Stream<T>` | `.Select(handover => Carried(...)).OfType<FactoryConnection>()`: `OfType<T>` skips nulls and narrows `FactoryConnection?` to `FactoryConnection` in one step |
| `as` with `??` | `x instanceof Logistics l ? l : (…)` | `world.Find(inside) as Logistics ?? (outside is { } o ? world.Find(o) as Logistics : null)`: `as` gives null instead of throwing |

## Choices worth a second look

1. **A handover with no items left is dropped**, but only when the filter emptied it. A handover that had no items to begin with (a machine whose recipe isn't known, BR-W10) stays, as before.
2. **Between a machine and a consumer with no logistics in between**, anything goes. The game doesn't build that, but the trace doesn't assume it.
3. **"At 100%" is net over all its machines at 100%**, also those that run slower now. A machine that could take more of its own product at 100% lowers it. *Can't leave* still counts only output blocked producers.

## Next

Part 2b(2): a live preview of the traced factory while choosing in 3D.
