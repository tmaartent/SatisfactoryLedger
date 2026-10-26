# ADR-0002: Modular monolith first, microservices by extraction

- **Status:** Accepted
- **Date:** 2026-10-26

## Context
The goal includes showing microservices experience. But there is one developer, one user, and one machine, and the domain boundaries are still being discovered. Distributed systems add network failure, eventual consistency, deployment and observability costs from day one. A wrong boundary costs much more to fix across services than across modules.

## Decision
We will build a **modular monolith**: one ASP.NET Core host, one module per bounded context, boundaries enforced by project references and architecture tests, and one database schema per module. A module is extracted into a service only when there's a concrete reason, and that extraction gets its own ADR.

## Alternatives considered
- **Microservices from the start:** high cost, boundaries not yet stable. Rejected.
- **Plain layered monolith:** cheapest, but no boundaries, so extraction later would mean a rewrite. Rejected.

## Consequences
- Fast in-process calls and simple transactions, with one deployable to run.
- Discipline required. Architecture tests are mandatory, not optional.
- Extraction (likely Catalog first) becomes a well-documented showcase step instead of the starting point.
