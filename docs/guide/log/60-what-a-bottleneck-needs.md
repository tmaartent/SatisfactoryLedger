# Step 60 · What a bottleneck needs

**Goal:** on the learning snapshot, the Quickwire Stator explanation named a bottleneck at a Conveyor Belt Mk.6, and the model suggested "increasing the capacity of the Conveyor Belt Mk.6". There's nothing above Mk.6, so the answer is another belt (asked 2026-11-22). Branch `feat/mcp-server`, on top of step 59 (uncommitted), uncommitted.

## What you decided

- **Below the highest tier:** the lowest tier that carries what's offered. **At or beyond the highest:** how many lines of the highest tier side by side.
- **Enough for what's offered:** what the belt carries plus what's offered to it on top (`offeredMore`, BR-A12). More than that would bring nothing.
- **A rule of the World trace** (BR-A20, BR-A21), not of the MCP server.

## How it reads now

```
Bottleneck · Quickwire
  Short of it                          Assembler (Alternate: Quickwire Stator) ×6
  At capacity · 1,200/min              Conveyor Belt Mk.6 ×1
  To carry all that's offered · 1,550/min
                                       2 lines of Conveyor Belt Mk.6 side by side
```

(1,550 is an example; your snapshot gives its own.)

## How it's built

- **World domain** (`Tracing/`):
  - `TransportTier` (new): a tier as the trace reads it, with its id, kind and capacity.
  - `MoreCapacity` (new): `For(conduit, offeredMore, tiers)` computes the rule. A pipe network takes pipe tiers. Among tiers of equal capacity, the pipeline with indicators wins over its "NoIndicator" twin by name order.
  - `TraceStarts.Bottleneck` makes the start for both traces. It replaces `TraceStarts.Capacity`, which had no other caller.
  - `TraceStart.MoreCapacity` (init), and `FlowReadings.OfferedMore`.
  - `ShortageTrace.From` and `BlockageTrace.From` take the tiers as an optional last parameter. The domain tests that don't need tiers leave it out.
- **Catalog:** `ICatalogQueries.GetAllTransportTiersAsync`, every tier still in the game. The snapshot only stores the tiers the save uses, so it can't tell what the highest tier is.
- **World application:**
  - `TraceEndpoints` asks the Catalog for the tiers.
  - `SnapshotTraces.Tiers` sorts them into belts, lifts and pipes with the save reader's `GameClasses.TryLogisticsKindOf`. The World module now sees the SaveFile project's internals (`InternalsVisibleTo`). The class names stay the anti-corruption layer's knowledge.
  - `TraceStartResponse.MoreCapacity`.
- **MCP:**
  - `MoreCapacityAnswer` names the tier by its in-game name and gives `carriesPerMinute`.
  - The trace tools' bottleneck descriptions say what `moreCapacity` means.
- **Assistant:** the model doesn't read `moreCapacity` as tier, lines and rate. `ExplanationPrompt` words it as a `nextStep` sentence, and a rule says to give it as written and suggest no other belt, lift or pipe. The sentences are *"Lay another Conveyor Belt Mk.6 beside it, for 2 lines side by side."* when the bottleneck is of that tier already, *"Rebuild it as Conveyor Belt Mk.4."*, or *"Rebuild it as 2 lines of Conveyor Belt Mk.6 side by side."*. On your world, qwen2.5:7b first called the 1,285.42/min the Mk.6 belt's capacity. With tier and lines alone, it read `lines: 2` as how many there are now, and wrote "if lines are already at 2, consider upgrading to a higher-tier belt", half quoting the rule. The dialog still shows the rate.
- **Web:** `StartRow.needs` (*"2 lines of Conveyor Belt Mk.6 side by side"*, or just the tier), shown under the bottleneck. Bottlenecks that need different things get rows of their own.
- **Docs:** BR-A20, BR-A21 and the glossary (**More capacity**).

## Tests

| Where | New |
|---|---|
| `World.Domain.Tests/Tracing/MoreCapacityTests.cs` | The lowest tier that's enough; lines at the highest tier; lines of the highest from below it; lift tiers for a lift; pipe tiers for a pipe network; nothing without tiers |
| `ShortageTraceTests`, `BlockageTraceTests` | A full belt's start carries its more capacity (one tier up; two lines of the highest) |
| `World.Tests/SnapshotTracesTests.cs` | The Catalog's tiers read by kind, leaving out what isn't a belt, lift or pipe |
| `Api.IntegrationTests/Catalog/CatalogQueriesTests.cs` | Every tier, up to Mk.6 belts and lifts and Mk.2 pipelines |
| `Mcp.Tests/TraceToolsTests.cs` | The bottleneck's `moreCapacity` by the tier's in-game name |
| `Assistant.Tests/ExplanationTests.cs` | The next step as a sentence (another line, or a rebuild as a tier or in lines), and the rate left out of what the model reads |
| `trace-starts.spec.ts`, `explain-dialog.spec.ts` | Lines or tier, rows apart by what they need, nothing for other causes; shown in the dialog |

1134 .NET and 222 web tests pass.

## C# / .NET notes

- **`await` inside a switch expression arm** (`TraceEndpoints.TraceAsync`): `{ Document: { } analysis } => TypedResults.Ok(trace(..., await catalog.GetAllTransportTiersAsync(...)))`. Any expression in an `async` method may await, arms included. The Catalog is only asked when the analysis is ready. Java's switch expressions have no `await` to compare with; a `CompletableFuture` would need `thenCompose`.
- **An optional parameter instead of an overload** (`ShortageTrace.From(..., tiers = null)`). The default must be a compile-time constant, so it's `null` and the method does `tiers ??= []`. Java has neither; it would take an overload.
