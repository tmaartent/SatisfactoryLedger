# Step 09 · World: factory definitions

**Goal:** make factory tracing usable through the API. Suggest output belts, define a factory once per world, and trace it in any snapshot (BR-W2, BR-W11, BR-W12).
**Result:** 31 new tests, all green. Solution total: 332 green, including the integration tests on Docker. On your test world, the suggested belt into the storage container carries iron plates and would contain 2 smelters and 1 constructor. A factory defined on it is traced in every snapshot of the `Development` world.
**Decisions you made:** a definition lives with the world and is traced in every snapshot. A world is a session name. The tool suggests candidate output belts. Input items are checked against the Catalog (BR-W2).
**Decisions I made, for you to overrule:** BR-W12's definition of a candidate, and the points under "Choices worth a second look".

## What was built

```
GET  /api/v1/world/snapshots/{id}/output-belts   candidate output belts: items, what it ends in, position, would-be machines
POST /api/v1/world/factories                     { world, name, outputBelt, inputItems } → 201 / 400 / 409 / 422 (code + items)
GET  /api/v1/world/factories?world=Development   the world's factories
GET  /api/v1/world/factories/{id}                one definition
GET  /api/v1/world/snapshots/{id}/factories      every factory of the snapshot's world, traced in it
```

| Layer | Change |
|---|---|
| Rules | **BR-W11:** a world is a session name, and a definition belongs to the world, traced in every snapshot of it, or reported as not traceable. Names are unique per world. **BR-W12:** what a candidate output belt is. |
| Domain | `CandidateOutputBelts.In(world)` and `OutputBeltCandidate`, `FactoryDefinition.IsTraceableIn(world)`, `WorldGraph.Buildables` |
| Adapter | `SavedBuildable.Position` (game units, centimetres) |
| World | `SnapshotDocument` stores positions. `Factories/`: `StoredFactory` (table `world.factories`, unique on world and name), `DefineFactoryHandler` with `DefineFactoryOutcome`, `FactoryTraces`, `OutputBeltSuggestions`, `FactoryEndpoints`. `Snapshots/SnapshotQueries` loads a snapshot's content. Migration `AddFactories`. |
| Catalog | `ICatalogQueries.GetItemsAsync(ids)` and `CatalogItem` (id, name), so the World can check input items. |
| Build | `.editorconfig`: migrations are generated code, so analyzers skip them. |

## How

### 1. Candidate output belts (BR-W12), in the domain
A belt or lift is a candidate when:
- everything it feeds is a storage container or a consumer, or it feeds nothing;
- walking upstream through logistics only (stopping at the first machine), it reaches at least one machine.

Its items are what those machines produce. That makes the last belt before the container a candidate, but not the belts before it. Machines behind another machine don't add their items.

### 2. Making a suggestion recognisable
An id alone doesn't let you find a belt in the game. So each suggestion also carries:
- its **position**: the adapter now reads it, and the snapshot stores it;
- what it ends in;
- what a factory from that belt **would contain**: a trace without any declared input items, counted per building class.

For your test world, that's `{ Build_ConstructorMk1_C: 1, Build_SmelterMk1_C: 2 }`.

### 3. Defining and tracing (BR-W11)
- **Defining a factory** checks its output belt against the world's **latest** snapshot, and its input items against the Catalog (BR-W2, added at your request). `DefineFactoryHandler` returns one of five outcomes (`Defined`, `UnknownWorld`, `NotAnOutputBelt`, `UnknownInputItems`, `NameTaken`), not exceptions. The endpoint maps them to 201, 422, 422, 422 and 409, each with a stable `code`. An unknown item is listed in an `items` field next to `code`, so a client doesn't have to parse `detail`.
- **Tracing** loads the snapshot's document, builds the graph once, and traces every factory of its world. If a factory's output belt isn't in that snapshot, it's reported with `traceable: false`.
- **"Not traceable" couldn't be tested over the API:** every save of your test world contains the belt, autosaves included. So it's tested in the domain, and in the World module on your test world's snapshot with that belt removed.

### 4. Tests that can fail
`IsTraceableIn` broken to always say yes failed exactly the four tests named after BR-W11 and BR-W2, at three levels: domain, World module and API. With the input-item check switched off, exactly its API test failed.

## Bugs the tools caught

- **CA1861 in EF's generated migration.** A composite index is written as a constant array. Generated code shouldn't be judged by our analyzers, so `.editorconfig` now marks migrations as `generated_code`.
- **The architecture test caught nested records declared `public`** inside an internal type. Their effective accessibility was internal, but reflection sees the declared one (primer).
- **An expected order in one of my tests was wrong:** `Build_Constructor…` sorts before `Build_Smelter…`.

## .NET points worth noticing (all now in the primer)

| .NET | Java/Spring (TypeScript) | Note |
|---|---|---|
| `abstract record` with a private constructor and nested `sealed record` cases | `sealed interface … permits` with records; a TypeScript union type | A closed set of outcomes. `switch` handles each case. |
| Declared vs. effective accessibility of nested types | Java has the same capping | Reflection and ArchUnitNET see the declared `public`. |
| `.editorconfig` `generated_code = true` | Excluding generated sources from Checkstyle or SpotBugs | Analyzers skip those files. |
| `HasIndex(f => new { f.World, f.Name }).IsUnique()` | `@Table(uniqueConstraints = …)` | A composite unique index from an anonymous type. |

## Choices worth a second look

1. **BR-W12 is my working definition of a candidate.** Belts that feed a station are candidates through "ends in nothing", because stations aren't modelled yet. Belts into another factory's machines are **not** candidates, because nothing marks those machines as another factory yet.
2. **A definition is checked only against the latest snapshot.** That's enough to catch typos, and BR-W11 allows the belt to be missing in other snapshots.
3. **The would-be machines** come from a trace without input items, so they go all the way up to the providers. Declaring input items when defining the factory narrows that.
4. **Positions are raw game units** (centimetres, Unreal's axes). Converting them to the in-game map's coordinates waits for a UI.
5. **No editing or deleting of definitions yet.** That's next to add when you need it (`PUT` and `DELETE /factories/{id}`).
6. **Input items must be known to the Catalog, but don't have to be raw resources or items a machine actually consumes.** Declaring Iron Plate as an input of a factory that only smelts ore is allowed. It just never stops the trace.
