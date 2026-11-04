# Architecture Decision Records

An ADR records **one** significant decision: its context, the decision, and its consequences. ADRs are immutable once accepted. To change your mind, write a new ADR that *supersedes* the old one. Format: [template](0000-template.md), based on Michael Nygard's.

**Status values:** Proposed → Accepted → (Deprecated | Superseded by ADR-xxxx)

| # | Title | Status |
|---|---|---|
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted |
| [0002](0002-modular-monolith.md) | Modular monolith first, microservices by extraction | Accepted |
| [0003](0003-module-project-structure.md) | Three projects per module: Domain, implementation, Contracts | Accepted |
| [0004](0004-exact-rational-rates.md) | Exact rational arithmetic for rates | Accepted |
| [0005](0005-game-data-import.md) | Game data imported from the game's Docs file behind an ACL | Accepted |
| [0006](0006-rest-openapi-generated-client.md) | REST + OpenAPI, generated TypeScript client | Proposed |
| [0007](0007-postgresql-ef-core.md) | PostgreSQL with EF Core, schema per module | Proposed |
| [0008](0008-testing-strategy.md) | Testing strategy | Proposed |
| [0009](0009-catalog-reimport-upsert-and-mark.md) | A catalog re-import updates by class name and keeps entries the game dropped | Accepted |
| [0010](0010-plain-handler-classes.md) | Use cases are plain handler classes, without a mediator library | Proposed |
| [0011](0011-catalog-handlers-use-dbcontext.md) | Catalog handlers use the module's DbContext directly, without repositories | Proposed |
| [0012](0012-diagnose-the-world-from-the-save-file.md) | Diagnose the world from the game's save file; planning comes later | Accepted |
| [0013](0013-read-the-save-with-satisfactorysavenet.md) | Read the save file with SatisfactorySaveNet's v1.2 branch, behind an anti-corruption layer | Accepted |
| [0014](0014-world-module-replaces-blueprint-and-network.md) | A World module replaces BlueprintDesign and FactoryNetwork | Accepted |
| [0015](0015-resource-nodes-from-the-game-map.md) | Resource nodes come from the game's map, through a generated node table | Accepted |
| [0016](0016-world-snapshots-as-one-document.md) | A world snapshot is stored as one document per import | Accepted |

*Proposed* ADRs are yours to decide. Read them, challenge them, then accept or change them before you start the step that needs them.
