# ADR-0007: PostgreSQL with EF Core, schema per module

- **Status:** Proposed
- **Date:** 2026-10-26

## Context
We need durable storage that runs locally (via Aspire/Docker), supports per-module isolation, and is what employers expect to see. SQLite would be simpler for a pure offline app.

## Decision
PostgreSQL, started by Aspire in a container, with **one schema and one DbContext per module**, each with its own migrations. Value objects are mapped as complex types or owned types. Aggregates are loaded and saved whole.

## Alternatives considered
- **SQLite:** zero setup, a single file. But it lacks schemas and is less representative for a showcase. It remains an option for a future "portable" build.
- **Event sourcing:** attractive for a ledger (history of the world), but a big extra learning curve. Consider it later, for one aggregate only.

## Consequences
- Docker Desktop is required for local development and integration tests (Testcontainers).
- Modules can't join across schemas, by design.
