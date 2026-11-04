# ADR-0016: A world snapshot is stored as one document per import

- **Status:** Accepted
- **Date:** 2026-11-05

## Context
Each imported save becomes a **world snapshot**, and every snapshot is kept so the world can be tracked over time (ADR-0012). Your big world has about 42,000 buildables and 44,600 connections per save. The analysis always loads a snapshot whole: a factory trace or a flow simulation needs the whole graph, never one buildable on its own. ADR-0007 says aggregates are loaded and saved whole, in PostgreSQL with one schema per module.

## Decision
A snapshot is **one row** in `world.snapshots`:
- **columns for what lists and filters need:** id, session name, game build, saved at, imported at, and the counts per kind;
- **one `jsonb` column for the content:** the buildables, the connections, the recipes they use, what's unsupported, and what was unknown.

The content is a versioned document (`"version": 1`) that the World module writes and reads itself with System.Text.Json. PostgreSQL compresses large `jsonb` values on its own (TOAST), so there's no extra compression step, and the content can still be inspected with SQL when needed.

**Recipes are copied into the snapshot at import:** their inputs, outputs and cycle time, as the Catalog has them at that moment. A snapshot keeps meaning the same thing after a later Catalog import. To pick up a fixed or newer recipe, import the save again.

## Alternatives considered
- **Rows per buildable and connection:** queryable across snapshots in SQL, but about 87,000 rows per import, millions after a few dozen saves. Imports and loads get slower, and bulk inserts would be needed to keep them usable. The questions the tool asks are answered on the whole graph in memory anyway.
- **A compressed `bytea` blob:** a little smaller, but opaque in the database, and PostgreSQL already compresses `jsonb`.
- **EF Core's own JSON mapping of the domain model (`ToJson()`, as for recipe ingredients):** the domain graph isn't shaped for persistence (lookups, a class hierarchy), and EF would track 87,000 elements. A document type of our own, serialized ourselves, keeps the domain free of persistence concerns.
- **Only the latest snapshot:** simpler, but drops tracking over time.

## Consequences
- An import writes one row, and a load reads one value and builds the graph in memory.
- Comparing snapshots ("which flaws are new?") happens in memory, between two loaded documents.
- The document format is a contract with ourselves. A change needs a new `version`, and the reader has to keep understanding older versions or convert them.
- SQL can't easily answer questions about single buildables across snapshots. If that's ever needed, a projection table can be added next to the document.
