# ADR-0008: Testing strategy

- **Status:** Proposed
- **Date:** 2026-10-26

## Context
TDD is a stated goal. Tests must be fast enough to run on every change, and must test behaviour, not implementation.

## Decision

| Level | Scope | Tools | Share |
|---|---|---|---|
| Domain unit tests | Aggregates, value objects, domain services. Written **test-first**. | xUnit v3, CsCheck (property-based) | most tests |
| Architecture tests | Dependency rules from the architecture overview | ArchUnitNET or NetArchTest | a few, always on |
| Module/integration tests | Use case + real database through the HTTP API | WebApplicationFactory + Testcontainers (PostgreSQL) | one or more per use case |
| Contract check | OpenAPI document unchanged unless committed | CI diff | 1 |
| Frontend unit tests | Components, signals, services | Vitest + Angular Testing Library | per component |
| End-to-end | A few critical user journeys | Playwright | a handful |

Rules: don't mock the domain. Mock only at process boundaries, and prefer a real database over a mock. Name tests after business rules (`BR-S4`) and behaviours, not methods.

## Alternatives considered
- **In-memory EF provider for integration tests:** fast, but behaves differently from PostgreSQL (no real transactions or constraints) and gives false confidence.
- **Mock-heavy unit tests for handlers:** couple tests to implementation details and break on every refactor.

## Consequences
- Docker is needed for the integration tests.
- The domain stays easy to test only as long as it has no infrastructure dependencies, which ADR-0003 guarantees.
