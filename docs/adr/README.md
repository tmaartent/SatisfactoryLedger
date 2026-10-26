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

*Proposed* ADRs are yours to decide. Read them, challenge them, then accept or change them before you start the step that needs them.
