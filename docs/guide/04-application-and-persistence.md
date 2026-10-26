# 04 · Application layer & persistence

The application layer *orchestrates*. It loads aggregates, calls domain behaviour and saves. It holds **no business rules**. If you find an `if` about stack heights in a handler, it belongs in the domain.

## Application layer

- **One use case = one handler**, organised as **feature folders** inside the module project:
  `Blueprints/AddProductionStep/` → command, handler, validator, endpoint.
- Commands change state (`AddProductionStep`). Queries read it (`GetBlueprintBalance`). Keep them separate (light **CQRS**). Queries may skip the domain model and project straight from the database into DTOs, which is fast and simple.
- A typical command handler: load the aggregate → call **one** domain method → handle the result → save → return. That's about 10 lines. Longer means logic is leaking in.
- **Mediator library or not?** Not required: a handler can be a plain class injected into the endpoint. Whichever you choose, write an ADR (MediatR became commercial, and alternatives like Mediator (source-generated) or Wolverine exist).
- Cross-module data goes through the other module's `Contracts` (e.g. `ICatalogQueries.GetRecipe(id)`), implemented inside the owning module.

## Persistence with EF Core (ADR-0007)

- **One `DbContext` per module**, with its own schema (`modelBuilder.HasDefaultSchema("factory_network")`) and its own migrations folder.
- **Mapping lives in the module project** (`IEntityTypeConfiguration<T>`), never as attributes on domain classes.
- Map the rich model without compromising it:
  - Value objects → **complex types** (EF Core 8+) or owned types. `Rate` → two columns (numerator, denominator).
  - Strongly typed IDs → value converters.
  - Private collections → backing field mapping (`Navigation(...).UsePropertyAccessMode(PropertyAccessMode.Field)`).
  - Ordered taps and injections → an explicit position column. Never rely on insertion order.
- **Repositories: keep them thin or skip them.** A repository per aggregate root (`IBlueprintRepository` with `GetAsync`, `Add`) makes the aggregate boundary explicit. Generic repositories add nothing. Choose, and record the choice in an ADR.
- **Unit of work** = `SaveChangesAsync` once per command.
- **Optimistic concurrency:** a row version column per aggregate root, exposed as an `ETag` in the API (chapter 05).

## Catalog import (M2)

- The importer is infrastructure in the Catalog module: parse (UTF-16!) → translate into the Catalog domain → upsert.
- Test it against a **trimmed fixture file** in the test project, a few items and recipes cut from the real file, including a fluid and an alternate recipe.
- Trigger it from an admin endpoint or a CLI command. Imports must be idempotent.

## Integration tests

- `WebApplicationFactory<Program>` + **Testcontainers PostgreSQL**: real HTTP, real database, real migrations.
- One container per test run, with the database reset between tests (e.g. with Respawn).
- Test **through the API**: "POST a blueprint, add a step, GET the balance → expected values". These tests survive internal refactors.
- Keep them few and meaningful: one happy path per use case, plus the important failure paths (404, 409/412, 422).

## Pitfalls
- Lazy loading. Turn it off and load aggregates explicitly and whole.
- Returning EF entities from endpoints. Always map to DTOs (the contract).
- Business logic in handlers ("transaction scripts"). That's the anaemic model again.
- One shared `DbContext` for all modules, which quietly destroys the boundaries.

## Done when (per slice)
- [ ] Handler under ~15 lines, with no business conditionals
- [ ] Domain classes have no persistence attributes
- [ ] Migration created and applied by the app or AppHost on startup in development
- [ ] Integration test green against real PostgreSQL
