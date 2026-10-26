# Satisfactory Ledger

[![CI](https://github.com/tmaartent/SatisfactoryLedger/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/tmaartent/SatisfactoryLedger/actions/workflows/ci.yml)

An offline ledger of a Satisfactory world. It keeps blueprints, stacked factories and the supply lines between them balanced, and replaces a hand-maintained spreadsheet.

It is also a deliberate learning and portfolio project. It shows Domain-Driven Design, Test-Driven Development, API design and a modern .NET + Angular stack, built as a modular monolith that can be split into services later.

> **Status:** scaffold with architecture tests and CI. No domain code yet. Follow the [learning guide](docs/guide/README.md).

## What it does (target)

- **Blueprints:** model a 6×6 blueprint as a chain of production steps. Inputs, outputs and byproducts are derived from game recipe data, so you never type a ratio again.
- **Factories:** place a blueprint stacked N high. The tool knows the maximum stack height from the belt and pipe limits.
- **Supply lines:** model a belt with taps (consuming factories) and injections (new resources via a priority merger). See where the line runs dry, where capacity is stranded, and where overflow can feed future factories.
- **Suggestions:** propose a tap order and merge points that use the fewest resource lines.

## Tech stack

| Layer | Choice |
|---|---|
| Backend | .NET 10 (LTS), C# 14, ASP.NET Core minimal APIs |
| Persistence | EF Core + PostgreSQL *(ADR-0007, proposed)* |
| Orchestration | .NET Aspire 13 (local dev, observability) |
| Frontend | Angular 22: standalone, signals, zoneless, Vitest |
| Tests | xUnit v3 (Microsoft Testing Platform), architecture tests, Testcontainers, Playwright |

## Repository layout

```
src/
  SatisfactoryLedger.AppHost/          Aspire orchestrator: run this to start everything
  SatisfactoryLedger.ServiceDefaults/  OpenTelemetry, health checks, resilience
  SatisfactoryLedger.Api/              The single ASP.NET Core host: composes the modules
  SatisfactoryLedger.SharedKernel/     Tiny set of concepts shared by all contexts (e.g. Rate)
  Modules/
    Catalog/            game data: items, recipes, buildings, belt/pipe tiers
    BlueprintDesign/    blueprints and their production steps
    FactoryNetwork/     factories, supply lines, balancing (core domain)
      *.Domain          pure domain model, no framework references
      *                 application + infrastructure + endpoints (internal)
      *.Contracts       the only thing other modules may reference
tests/
  *.Domain.Tests        fast unit tests, written test-first
  ArchitectureTests     enforce the module and layer rules
  Api.IntegrationTests  HTTP-level tests against a real database
web/                    Angular frontend
docs/                   architecture, domain, ADRs, learning guide
```

## Getting started

```bash
dotnet build SatisfactoryLedger.slnx
dotnet run --project src/SatisfactoryLedger.AppHost   # Aspire dashboard + API
cd web && npm start                                   # Angular dev server
```

## Documentation

- [Architecture overview](docs/architecture/overview.md)
- [Domain: glossary](docs/domain/glossary.md) · [context map](docs/domain/context-map.md) · [business rules](docs/domain/business-rules.md)
- [Architecture Decision Records](docs/adr/README.md)
- [Learning guide: step by step, with best practices](docs/guide/README.md)
