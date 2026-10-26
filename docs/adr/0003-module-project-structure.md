# ADR-0003: Three projects per module: Domain, implementation, Contracts

- **Status:** Accepted
- **Date:** 2026-10-26

## Context
A module needs (a) a pure domain model, (b) use cases, persistence and endpoints, and (c) a public surface for other modules. Splitting every layer into its own project (Domain/Application/Infrastructure/Api × 3 modules = 12+ projects) adds ceremony. A single project per module relies purely on discipline to keep the domain clean.

## Decision
Each module has three projects:
- `<Module>.Domain`: entities, value objects, domain services and events. References only `SharedKernel`. **The compiler** guarantees there's no EF or ASP.NET in the domain.
- `<Module>`: application, infrastructure and endpoints, organised by feature folder. Types are `internal` by default. Exposes `Add<Module>Module()` and `Map<Module>Endpoints()`.
- `<Module>.Contracts`: public DTOs, query interfaces and integration events. The only thing other modules may reference.

## Alternatives considered
- **Four or more projects per module (Clean Architecture by the book):** more ceremony, little extra safety for a single developer.
- **One project per module:** the domain's purity would depend on discipline alone.

## Consequences
- Domain purity is enforced by the compiler. The other rules are enforced by architecture tests.
- Application/infrastructure separation inside `<Module>` is by folder and convention. That's acceptable because those layers rarely get reused separately.
- `InternalsVisibleTo` is needed where a module's own tests reach internals.
