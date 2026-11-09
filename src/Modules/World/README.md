# World module

The world as you built it, read from your save files, and what's wrong with it. A snapshot holds every machine, provider, consumer and piece of logistics with how they're connected. On top of it: your factory definitions, the flow analysis (a steady-state simulation of what every machine really runs at), its findings, and the world map.

It's the *core* subdomain ([context map](../../../docs/domain/context-map.md), [ADR-0012](../../../docs/adr/0012-diagnose-the-world-from-the-save-file.md)). It needs the Catalog's rates and sizes, which it copies into each snapshot at import.

## Projects

| Project | What's in it | May reference |
|---|---|---|
| `SatisfactoryLedger.World.Domain` | The world graph (`WorldGraph`, `Machine`, `Provider`, `Consumer`, `Logistics`, `Connection`), factory definitions and their members, drones (`DroneRoute`, `DroneRefuelling`), and in `Flow/` the flow network and the solver (`SteadyState`). No framework references. See [`Flow/README.md`](SatisfactoryLedger.World.Domain/Flow/README.md). | SharedKernel |
| `SatisfactoryLedger.World.SaveFile` | Reads a `.sav` file into a `SavedWorld`. The anti-corruption layer for the save format ([ADR-0013](../../../docs/adr/0013-read-the-save-with-satisfactorysavenet.md)): the SatisfactorySaveNet library and the game's class names stay in here. It also turns the save's links into directed connections, and each pipe network into one hub (ADR-0023). | its own Domain, SatisfactorySaveNet |
| `SatisfactoryLedger.World.Simulation` | What the API and the analysis service share ([ADR-0018](../../../docs/adr/0018-analysis-as-a-separate-service-over-rabbitmq.md)): the flow network as a document, the stored analysis (`AnalysisDocument`), and the `SnapshotImported` message. | its own Domain |
| `SatisfactoryLedger.World` | Everything around the domain: importing saves, the endpoints, persistence, the findings, the map. | its own projects, `Catalog.Contracts` |
| `SatisfactoryLedger.World.Contracts` | What other modules may use. Empty so far: nothing asks the World anything yet. | nothing |

## Inside `SatisfactoryLedger.World`

| Folder | What it does |
|---|---|
| `Snapshots/` | `POST /api/v1/world/snapshots`: read the save, copy what the Catalog says about the recipes, extractors, belts, nodes and generators it uses, and store it all as one document (`SnapshotDocument`, [ADR-0016](../../../docs/adr/0016-world-snapshots-as-one-document.md)). Also listing, reading and deleting snapshots. |
| `Analysis/` | The findings of a snapshot or a factory (`SnapshotAnalysis`): machines and why they run below 100%, bottlenecks, unused and unknown supply, generators, drone ports. The background work that asks for an analysis ([ADR-0017](../../../docs/adr/0017-snapshot-analysis-stored-and-computed-in-the-background.md)), and the internal endpoints the analysis service calls (`/internal/world`). |
| `Factories/` | Your factory definitions: the machines you selected, matched in each snapshot (a rebuilt machine by class and position) and traced to the logistics, outputs, inputs and suppliers that belong to it. Factories defined earlier by an output belt are converted at startup. |
| `Map/` | `GET /api/v1/world/snapshots/{id}/map`: what the world map draws ([ADR-0020](../../../docs/adr/0020-world-map-leaflet-on-a-plain-grid.md)). |
| `Persistence/` | EF Core: `WorldDbContext` in schema `world`, with snapshots, factories and stored analyses, and the migrations. |

## How the data flows

```
.sav file → World.SaveFile → SavedWorld ─┐
                     Catalog (ICatalogQueries) ─┴→ SnapshotDocument → PostgreSQL (schema world)
                                                         │
                                SnapshotImported over RabbitMQ (outbox)
                                                         ↓
            analysis service: GET flow network → SteadyState → PUT AnalysisDocument
                                                         ↓
                               SnapshotAnalysis → findings over the API → web app
```

The flow analysis runs in a separate process, `SatisfactoryLedger.AnalysisService` (ADR-0018), because a big world takes seconds. It owns no data: it fetches the flow network and sends the analysis back.

## Worth reading

- The business rules BR-W* (the world) and BR-A* (the analysis) in [`docs/domain/business-rules.md`](../../../docs/domain/business-rules.md).
- [ADR-0014](../../../docs/adr/0014-world-module-replaces-blueprint-and-network.md) (why this module exists), [ADR-0021](../../../docs/adr/0021-generators-burn-at-full-power.md) (generators), [ADR-0022](../../../docs/adr/0022-drone-fuel-from-the-latest-trip.md) (drones), [ADR-0023](../../../docs/adr/0023-pipe-networks-as-one-hub.md) (pipe networks).
- Tests: `tests/SatisfactoryLedger.World.Domain.Tests` (the graph and the solver), `World.SaveFile.Tests` (on the fixture saves), `World.Simulation.Tests`, `World.Tests` (snapshots, findings, map), and `Api.IntegrationTests/World`.
