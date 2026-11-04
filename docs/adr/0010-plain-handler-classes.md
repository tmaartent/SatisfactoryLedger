# ADR-0010: Use cases are plain handler classes, without a mediator library

- **Status:** Proposed
- **Date:** 2026-10-30

## Context
Chapter 04 asks for one handler per use case, and leaves open whether a mediator library dispatches commands to handlers. MediatR became commercial; free alternatives exist (Mediator, a source generator; Wolverine). A mediator adds pipeline behaviours (logging, validation, transactions around every handler) at the price of indirection: the endpoint no longer shows which class does the work.

## Decision
A use case is a plain `internal sealed class` with one `HandleAsync` method, registered in the module's DI setup and injected straight into the endpoint (e.g. `ImportCatalogHandler`). Cross-cutting concerns use ASP.NET Core's own tools: endpoint filters, middleware and OpenTelemetry.

## Alternatives considered
- **MediatR:** the best known, but now commercially licensed.
- **Mediator (source-generated) / Wolverine:** free, but one more library and its conventions to learn, for no behaviour we need yet.

## Consequences
- "Go to definition" from an endpoint lands on the handler. No hidden dispatch.
- Shared behaviour around every handler has to be added explicitly. If that becomes repetitive, revisit this ADR.
