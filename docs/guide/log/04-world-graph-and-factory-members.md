# Step 04 · World module: the world graph and a factory's members

**Goal:** the first domain step of the diagnosis tool ([ADR-0012](../../adr/0012-diagnose-the-world-from-the-save-file.md)). Model the world as a graph and find which machines and logistics belong to a factory you define (BR-W1 to BR-W8).
**Result:** a new World module replaces BlueprintDesign and FactoryNetwork ([ADR-0014](../../adr/0014-world-module-replaces-blueprint-and-network.md)). There are 22 new domain tests, all green. Solution total: 169 green. The 16 integration tests need Docker, which wasn't running. Nothing they cover changed.
**Decisions you made:** BR-W1 to BR-W8, Q12 to Q17, the module name World, and removing the two empty modules.
**Decisions I made, for you to overrule:** see "Choices worth a second look".

## What was built

```
World.Domain
  Buildable (abstract)  ── Machine   consumes / produces items
                        ── Provider  miner, extractor, unloading station          (BR-W1)
                        ── Consumer  sink, depot uploader, generator, loading station (BR-W8)
                        ── Logistics belt, lift, splitter, merger, storage container
  Connection(From, To)  items move from an output to an input
  WorldGraph            buildables + connections; Upstream(id), Downstream(id)
  FactoryDefinition     name, output belt, input items (BR-W2)
    .TraceMembers(world) → FactoryMembers { Machines, Logistics }  (BR-W3, BR-W4)
```

| Project | Files |
|---|---|
| `src/Modules/World/SatisfactoryLedger.World.Domain` | `BuildableId`, `ItemId`, `Buildable`, `Machine`, `Provider`, `Consumer`, `Logistics` (+ `LogisticsKind`), `Connection`, `WorldGraph`, `FactoryDefinition`, `FactoryMembers` |
| `src/Modules/World/SatisfactoryLedger.World`, `.Contracts` | Empty entry points, like the old modules had |
| `tests/SatisfactoryLedger.World.Domain.Tests` | `WorldGraphTests` (6), `FactoryMembersTests` (16), `TestWorld` (a builder for hand-made worlds) |
| Removed | `src/Modules/BlueprintDesign`, `src/Modules/FactoryNetwork` and their two empty test projects |
| Rewired | `SatisfactoryLedger.slnx`, `Api.csproj`, the architecture tests (now check World), CI (no more `--ignore-exit-code 8`) |
| Docs | ADR-0014, architecture overview, context map, glossary, README, and guide examples that named the old modules |

## How the trace works

The tests read like small worlds:

```csharp
var world = new TestWorld()
    .Miner("miner").Belt("main").Splitter("first").Splitter("second")
    .Machine("left", Items.IronOre, Items.IronIngot).Machine("right", Items.IronOre, Items.IronIngot)
    .Merger("merge").Belt("output")
    .Connect("miner", "main", "first", "left", "merge", "output")
    .Connect("first", "second", "right", "merge")
    .Build();
```

`TraceMembers` works in two passes. The first returns an `UpstreamTrace`, a private record; the second is a method on it.

1. **Upstream from the output belt (BR-W3).** A breadth-first walk against the flow, through logistics and machines. It stops at a provider, and at a machine that makes one of the declared input items, because that one belongs to another factory (BR-W6). Every machine passed is a member. Consumers are never upstream of anything, so they're never reached (BR-W8).
2. **Which of the logistics passed are the factory's (BR-W4, Q17):**
   - Everything **fed by the factory's machines** belongs to the factory, up to and including the output belt.
   - The rest lies on input lines. A piece of logistics stays only while everything it feeds is in the factory. This repeats until nothing changes, so a main-line splitter that also feeds another factory drops out, and so does everything upstream of it.
   - Of what's left, the input **starts at a splitter**: the splitters plus everything downstream of them. The belt between the supply and the first splitter is supply.

**Checking the tests can fail.** Every test passed on its first run, after the red step where the types didn't exist yet. So I broke two rules on purpose. Without the splitter rule, exactly the two BR-W4 tests failed. Without the stop at machines making a declared input, exactly the BR-W6 test failed. Then I restored the code.

## .NET points worth noticing (all now in the primer)

| .NET | Java/Spring (TypeScript) | Note |
|---|---|---|
| `record` for `BuildableId`, `class` for `Machine` | Java `record` vs class | Value objects compare by value, entities by identity. A record compares a collection property *by reference*. |
| `class Provider(BuildableId id) : Buildable(id)` | — | Primary constructor. The parameter is captured, not a property. |
| `private protected` constructor on `Buildable` | `sealed … permits` | Only this assembly can add roles. |
| `output is Logistics { IsBelt: true }`, `switch` with `when` | Java 21 record patterns | Type and property check in one expression. |
| `ILookup<K, V>` from `ToLookup` | Guava `ListMultimap` | A missing key gives an empty sequence. |
| CA1859 | "Program to interfaces" | Private members should use concrete types. The build fails until they do. |
| `Assert.Equal` on two sets | AssertJ `containsExactlyInAnyOrder` | xUnit compares them **in order**. `Ids.AssertSet` sorts first. |
| Private nested `record UpstreamTrace` instead of a tuple | A private static nested record | Changed after your review. The tuple had an element named `Logistics` that held more than the factory's logistics. The record names it `PassedLogistics`, and the second pass is now its method `FactoryLogistics`. |
| `inputSide.RemoveWhere(...) > 0` in a `while` | `removeIf` | `RemoveWhere` returns how many it removed, which gives a short fixpoint loop. |

## Choices worth a second look

1. **Logistics fed by the factory's machines always belong to the factory**, even when they also feed something outside. Example: the splitter after shared smelters (BR-W5) that sends ingots to a second factory. That splitter is in both factories. The alternative is to apply the "feeds only the factory" rule inside the factory too, but then logistics between a factory's own machines could fall out of it. Which do you want?
2. **Every belt segment is a node**, and a connection links two buildables. Ports aren't modelled yet: which output of a splitter, or which output of a refinery carries which item. The flow analysis needs them, so they come with it.
3. **A machine only knows which items it consumes and produces.** Rates, clock speed and somersloops come with the flow analysis. My plan is to resolve each recipe through `Catalog.Contracts` at import and store it in the snapshot, so a later Catalog import doesn't change an old snapshot.
4. **World has its own `ItemId`**, not Catalog's. It's the same class name, but each context owns its model. The import translates between them.
5. **Invalid definitions throw `ArgumentException`**, like the Catalog domain does: the output belt isn't in the world, isn't a belt, or the name is blank. The architecture overview (§6) says domain failures should be *results*, not exceptions. When defining a factory becomes an endpoint, a non-belt output is your mistake, not a bug, so it should become a result there. That's something to decide in that step.
6. **Not covered yet:**
   - **Pipes.** Pipe connections in the save have no direction. It has to come from the machines' fluid ports.
   - **Stations.** A station is a provider or a consumer depending on whether it unloads or loads. The import decides that per platform.
   - **An output belt that also carries another factory's items.** You'd pick the belt before the merge. The tool doesn't warn about this yet.
   - **A factory with no input items** is allowed. The trace stops at providers anyway.

## Next steps

- **Save adapter (`World.SaveFile`)** with your small test save: translate a real save into a `WorldGraph`.
- **Flow analysis:** ports, recipe rates, and real splitter and merger behaviour, so the tool can say "this machine gets 60%".
- **Persistence:** world snapshots per import, and your factory definitions.
