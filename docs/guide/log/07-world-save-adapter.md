# Step 07 · World: the save-file adapter

**Goal:** read a save into our own model, behind an anti-corruption layer ([ADR-0013](../../adr/0013-read-the-save-with-satisfactorysavenet.md)), and trace a factory in a real world for the first time.
**Result:** a new project, `World.SaveFile`, reads your test world and your big world. With 72 new tests, all green, the solution has 284 green tests, including the 17 integration tests on Docker. End to end, the factory traced from the belt into the storage container in your test world is exactly Smelter A, Smelter B and Constructor C.
**Decisions you made:** push the library fix to your fork (pin `b329a9b`), and the test world's contents.
**Decisions I made, for you to overrule:** see "Choices worth a second look".

## The test world

`tests/SatisfactoryLedger.World.SaveFile.Tests/Fixtures/Development.sav` (158 KB, game build 517554):

```
Miner Mk.1 → Belt Mk.1 → Lift Mk.1 → Belt Mk.1 → Splitter
  → Smelter A (100%) and Smelter B (33.3333%) → Merger → Belt Mk.6 → Constructor C (Iron Plate, 75%)
  → Smart Splitter: overflow → AWESOME Sink, rest → belt → Storage Container
Constructor D (Iron Rod): input unconnected, output belt ends in the air
Water Extractor → Pipeline Mk.1 → Clean Pipeline Mk.2 → Packager (Packaged Water)
```

## What was built

```
World.SaveFile (the only project that references SatisfactorySaveNet)
  SaveFileReader.Read(Stream) → SavedWorld
    SaveInfo          session name, game build, saved at
    SavedBuildable    SavedMachine (recipe, clock speed, production boost)
                      SavedProvider (node, clock speed)
                      SavedConsumer
                      SavedLogistics (kind: belt, lift, splitter, merger, container, pipe, pipe junction)
    Connection        from an output to an input (World.Domain's own type)
    Unsupported       classes in the network that aren't modelled yet, with a count
  GameClasses         the game knowledge: role per class, direction per port
  ConnectionBuilder   links → directed connections; pipes get their direction here
  SaveFileException   anything that can't be read or doesn't add up
```

| File | Change |
|---|---|
| `src/Modules/World/SatisfactoryLedger.World.SaveFile/` | New: `SaveFileReader`, `SavedWorld` (and its records), `GameClasses`, `ConnectionBuilder`, `SaveFileException` |
| `World.Domain/Logistics.cs` | `LogisticsKind` gains `Pipe` and `PipeJunction` |
| `tests/SatisfactoryLedger.World.SaveFile.Tests/` | New: 72 tests and the test world |
| `tests/SatisfactoryLedger.ArchitectureTests/` | The adapter is a fourth kind of assembly. Two new rules: only the adapter uses the library, and `SaveFileReader` does use it. A module's own projects may depend on each other. |
| `.gitattributes` | `*.sav binary` |
| `SatisfactoryLedger.slnx` | Both projects added |

## How

### 1. Only the reader touches the library
`SaveFileReader` is the only class that uses the library's types (`SatisfactorySave`, `ActorObject`, `ObjectProperty`). It turns every `Build_…` actor into a `SavedBuildable`, plus its belt and pipe ports. Any failure inside the library comes out as a `SaveFileException`, whatever the library threw (it also throws plain `EndOfStreamException` and `NotSupportedException`). A save is read completely or not at all.

### 2. The rules of the anti-corruption layer (from spike log 03)
- **A missing value means the default.** No `mCurrentPotential` means 100%, and no `mCurrentProductionBoost` means 1.
- **Clock speeds are floats.** `0.333333` × 100, rounded to 4 decimals, gives 33.3333%. Only then does it become an exact `ClockSpeed`.
- **Every link must be stored on both ends.** A link stored once, a link to a missing port, or two outputs linked together is a `SaveFileException`.
- **Snap points (`SnapOnly0`) carry nothing** and are skipped.

### 3. Which way items flow
- **Belts:** the port names say it. `Input*` and `Output*`, a belt's `ConveyorAny0` (in) and `ConveyorAny1` (out), the pipe ports of machines (`PipeInputFactory`, `PipeOutputFactory`, `PipeInput0`, `PipeFactoryOutput0`).
- **The unnamed fluid port** (`FGPipeConnectionFactory`) depends on the building. Extractors and the Converter put fluid out (Converter recipes only make fluids, checked in the Docs file). Generators and the Particle Accelerator take it in.
- **Pipes have no direction.** `ConnectionBuilder` walks each pipe network breadth first, outward from where fluid enters (a directed connection into a pipe). Each pipe link then points from the side nearer to the source.

### 4. What gets classified
`GameClasses` lists every class in the item network of your big world (54 classes):
- **Machines (11):** smelter to quantum encoder.
- **Providers (6):** miners, oil, water and resource well extractors.
- **Consumers (8):** sinks, depot uploaders, generators, the alien power augmenter and the portal.
- **Logistics:** belts, lifts, splitters (including smart and priority), mergers, containers, fluid buffers, pipes, pumps, valves and junctions.
- **Unsupported:** stations, because their mode (load or unload) isn't modelled yet (BR-W1).
- **Unknown classes:** anything else with linked ports is reported as unsupported. Anything without links (foundations, poles, power) is left out silently.

### 5. Checked against your big world (manual, not in CI)
`Clean Stop.sav` reads in about 12 s:

| | |
|---|---|
| Machines | 2,425 (1 without a recipe, 10 with somersloops) |
| Providers | 288, all with a node |
| Consumers | 546 |
| Logistics | 38,700 |
| Connections | 44,618 |
| Unsupported | 216 train stations, 36 fluid train stations, 28 drone ports |
| Pipes, junctions and pumps | 8,376, of which 612 got no direction |

## Bugs the tests caught

- **`Build_Pipeline…` as a prefix also matched `Build_PipelineSupport_C` and `Build_PipelineFlowIndicator_C`.** The test "poles, supports, flow indicators and the coupon shop are not part of the world" failed. Pipe classes are now listed one by one.
- **`World.Buildables` in a test didn't compile.** Inside `namespace SatisfactoryLedger.World.SaveFile.Tests`, the name `World` is the namespace `SatisfactoryLedger.World`, not the `using static` property. The property is now called `Save` (primer).

## .NET points worth noticing (all now in the primer)

| .NET | Java/Spring (TypeScript) | Note |
|---|---|---|
| A name from an enclosing namespace beats a `using static` import | In Java, a static import wins over a package name | `World` was the namespace, not the property. |
| `catch (Exception e) when (e is not (SaveFileException or OutOfMemoryException))` | `catch (A \| B e)` | Pattern combinators (`not`, `or`) inside an exception filter. |
| `switch ((one.Port, other.Port)) { case (Port.Out, Port.In or Port.Undirected): … }` | Java 21 record patterns | Switching on a tuple of two values, with `or` patterns. |
| `"this is not a save file"u8` | `"…".getBytes(UTF_8)` | A UTF-8 string literal: a `ReadOnlySpan<byte>`, made at compile time. |
| Internal enums in theories passed as strings | — | A `public` test method can't take an `internal` type (CS0051). xUnit needs public test classes, so `Enum.Parse` inside the test. |
| ArchUnitNET: a positive rule guarding a negative one | ArchUnit, same thing | `NotDependOnAny(library)` also passes when the pattern matches nothing. `SaveFileReader … Should().DependOnAny(library)` proves it matches. |

## Choices worth a second look

1. **The adapter returns its own model (`SavedWorld`), not a `WorldGraph`.** A `Machine` in the domain needs its recipe's items, and those come from the Catalog, which the adapter doesn't know. The import use case (next step) turns `SavedWorld` plus Catalog lookups into a `WorldGraph`. The end-to-end test does that with a hand-written recipe table.
2. **The adapter reuses World.Domain's `BuildableId`, `Connection`, `LogisticsKind` and the shared kernel's `ClockSpeed`.** It depends inward on the domain, never the other way round. The other option, plain strings and our own enums, would mean a second translation for nothing.
3. **Stations are reported, not modelled.** Their connections are left out, so a belt from a train station starts nowhere for now. That's the likely reason for the 612 pipes without a direction: fluid that comes from the 36 fluid train stations.
4. **Pumps and valves are treated as plain pipe.** A pump does have a direction in the game, and a valve has a flow limit. Neither is read yet.
5. **Smart splitter rules and priority merger settings aren't read yet.** The spike showed they're readable (Wildcard → 0, None → 2, Overflow → 1). The flow analysis needs them, so they come with it.
6. **The Portal and the Alien Power Augmenter are consumers.** They take items out of the network, like generators.
7. **Q19 is still open:** a storage container that nothing feeds (your packager's canisters).
