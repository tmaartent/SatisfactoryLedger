# Architecture overview

This document follows the [C4 model](https://c4model.com): context → containers → components → code. Decisions behind it are recorded in the [ADRs](../adr/README.md).

## 1. Goals and constraints

| Quality goal | What it means here |
|---|---|
| **Correctness** | Balancing must be exact: 7.5/min stays 7.5/min. The domain is the most important and most tested part. |
| **Evolvability** | Module boundaries are enforced, so a module can become a separate service without rewriting it. |
| **Showcase value** | The structure, tests, ADRs and API should read as senior-level work to someone browsing the repo. |
| **Offline first** | Runs entirely on one machine. No cloud dependencies required. |

Constraints: a single developer, a learning project, and a game-data source (`Docs.json`) that changes with game updates.

## 2. System context (C4 level 1)

```mermaid
flowchart LR
    player([Player<br/>diagnoses and tracks the world])
    ledger[Satisfactory Ledger]
    docs[(Game data file<br/>CommunityResources/Docs)]
    save[(Save file .sav)]

    player -- "imports saves,<br/>defines factories,<br/>reads findings" --> ledger
    ledger -- "imports items,<br/>recipes, buildings" --> docs
    ledger -- "imports the world<br/>(ADR-0012, ADR-0013)" --> save
```

## 3. Containers (C4 level 2)

```mermaid
flowchart LR
    browser[Angular SPA<br/>web/]
    api[ASP.NET Core API<br/>SatisfactoryLedger.Api<br/>modular monolith]
    analysis[Analysis service<br/>SatisfactoryLedger.AnalysisService<br/>stateless, ADR-0018]
    db[(PostgreSQL<br/>one schema per module<br/>+ Wolverine's outbox)]
    mq[[RabbitMQ]]
    apphost[Aspire AppHost<br/>local orchestration +<br/>dashboard / OpenTelemetry]

    browser -- "JSON over HTTPS<br/>REST, OpenAPI-described" --> api
    api -- "EF Core" --> db
    api -- "SnapshotImported<br/>(transactional outbox)" --> mq
    mq -- "SnapshotImported" --> analysis
    analysis -- "GET flow network,<br/>PUT analysis<br/>(/internal/world)" --> api
    apphost -. starts and wires .-> browser
    apphost -. starts and wires .-> api
    apphost -. starts and wires .-> analysis
    apphost -. starts .-> db
    apphost -. starts .-> mq
```

- **Two deployable backends.** The API host composes all modules in-process. The **analysis service** runs a snapshot's flow simulation when the API says it was imported (ADR-0018). It owns no data: it fetches the flow network over HTTP and sends the analysis back.
- **Messages stay small.** RabbitMQ carries only `SnapshotImported { SnapshotId }`; the documents (megabytes on a big world) go over HTTP (the *claim check* pattern). The API publishes through Wolverine's transactional outbox, so a snapshot and its message are stored together.
- **One database, a schema per module.** A module never reads another module's tables. This is the cheapest boundary to enforce now and the most expensive to fix later.
- The **TypeScript API client is generated** from the OpenAPI document (ADR-0006), so the frontend can't drift from the contract.

## 4. Components: the modules (C4 level 3)

```mermaid
flowchart TB
    subgraph Api[SatisfactoryLedger.Api — composition root]
        direction LR
        host[Program.cs<br/>registers modules, maps endpoints]
    end

    subgraph Catalog[Catalog module · supporting]
        cat[Items · Recipes · Buildings · Belt/Pipe tiers<br/>import from Docs.json]
    end

    subgraph World[World module · core]
        wd[World graph from the save<br/>factory definitions and their members<br/>flow analysis and findings]
        ws[World.SaveFile<br/>anti-corruption layer, ADR-0013]
    end

    host --> Catalog
    host --> World

    World -- "Catalog.Contracts<br/>(recipes, belt/pipe capacity)" --> Catalog
```

Dependencies only point **downstream** (World → Catalog) and only through `*.Contracts`. The World module replaced BlueprintDesign and FactoryNetwork (ADR-0014). See the [context map](../domain/context-map.md) for the DDD relationships.

### Inside a module

Each module consists of three projects (ADR-0003). World adds a fourth for the save-file adapter (ADR-0013):

```
SatisfactoryLedger.<Module>.Domain      entities, value objects, domain services, domain events
                                         → references only SharedKernel. No EF, no ASP.NET.
SatisfactoryLedger.<Module>             application layer (use cases / handlers)
                                         infrastructure (EF DbContext, configurations, importers)
                                         endpoints (minimal API route group)
                                         → all types internal, exposes one AddXxxModule() / MapXxxEndpoints()
SatisfactoryLedger.<Module>.Contracts   public DTOs, integration events, query interfaces
                                         → the only assembly other modules may reference
```

This is *vertical slices across modules, clean layering within a module*. The `Domain` project boundary is checked by the compiler. Everything else is checked by architecture tests.

### Dependency rules (enforced in `tests/SatisfactoryLedger.ArchitectureTests`)

1. `*.Domain` depends on nothing but `SharedKernel` and the BCL.
2. A module may depend on another module's `*.Contracts` only, never its `Domain` or implementation.
3. `*.Contracts` depends on nothing (primitive types and records only).
4. `Api` only calls each module's public registration and endpoint-mapping methods.
5. No cycles between modules.

## 5. Runtime view: two key flows

**Importing a save (command, planned):** the endpoint receives the `.sav` → the `World.SaveFile` adapter reads it and translates it into a `WorldGraph` (buildables and connections) → recipes are resolved through `Catalog.Contracts` → the snapshot is stored as a new version of the world.

**Diagnosing a factory (query):** load the world graph and the player's `FactoryDefinition` → `TraceMembers` finds the factory's machines and logistics (BR-W3, BR-W4) → the flow analysis (planned) computes what each machine receives → return the findings.

## 6. Cross-cutting concerns

| Concern | Approach |
|---|---|
| Errors | Domain failures are returned as results, not thrown, and mapped to RFC 9457 ProblemDetails with a stable `code`. Exceptions are reserved for bugs. |
| Validation | Request shape is checked at the endpoint (.NET 10 minimal API validation). Business rules are checked in the domain. Never duplicate a rule in both. |
| Concurrency | Optimistic: row version → `ETag` / `If-Match` (412 on conflict). |
| Observability | OpenTelemetry via ServiceDefaults; traces and logs in the Aspire dashboard. |
| Module communication | Synchronous: query interfaces in Contracts. Asynchronous: integration events through an outbox (introduced when first needed). |
| Time and IDs | Inject `TimeProvider`. Strongly typed IDs (`BuildableId`, `ItemId`) to prevent mix-ups. |

## 7. Evolution path

1. **Now:** modular monolith, in-process module calls.
2. **When a real reason appears:** introduce an in-process event bus and an outbox for integration events. A candidate trigger is re-analysing the world when a new save is imported.
3. **Extraction showcase:** move **Catalog** into its own service. It's read-mostly and has a clean contract, so it's the lowest-risk candidate. Contracts become HTTP and messages, Aspire wires both services, and an ADR documents the before/after.

Microservices are a means, not a goal. See [guide step 8](../guide/08-towards-microservices.md).
