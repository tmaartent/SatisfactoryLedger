# ADR-0011: Catalog handlers use the module's DbContext directly, without repositories

- **Status:** Proposed
- **Date:** 2026-10-30

## Context
Chapter 04 says to keep repositories thin or skip them, and to record the choice. The Catalog is reference data: no aggregate with complex invariants across several tables, one write use case (the import) and a few read-only queries.

## Decision
In the Catalog module, the import handler and the query endpoints use `CatalogDbContext` directly. The `DbContext` already is a unit of work, and each `DbSet<T>` already is a collection-like repository. Queries read with `AsNoTracking()` and map straight to response records (light CQRS, chapter 04).

## Alternatives considered
- **A repository per entry type (`IItemRepository`, …):** four thin wrappers that only forward to `DbSet<T>`, with nothing to hide.
- **A generic repository:** adds nothing over `DbSet<T>` (chapter 04 says so too).

## Consequences
- Less code, and EF Core features (change tracking, `AsNoTracking`, projections) are used where they're needed.
- The handler is tested through the API on a real database (ADR-0008), not with a mocked repository.
- This ADR is for the Catalog only. BlueprintDesign and FactoryNetwork have real aggregates; decide for them separately (M3), where a repository per aggregate root may well be worth it.
