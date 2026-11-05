# Satisfactory Ledger

[![CI](https://github.com/tmaartent/SatisfactoryLedger/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/tmaartent/SatisfactoryLedger/actions/workflows/ci.yml)

An offline ledger of a Satisfactory world. It reads the world from the game's save file and finds the flaws in it: supply lines stretched thin, machines that don't get what they need, outputs on belts that can't carry them. It replaces a hand-maintained spreadsheet that only knew what was typed into it.

It is also a deliberate learning and portfolio project. It shows Domain-Driven Design, Test-Driven Development, API design and a modern .NET + Angular stack, built as a modular monolith that can be split into services later.

> **Status:** game catalog import and API done. The World module models the world as a graph and finds a factory's machines; reading the save file is next. Follow the [learning guide](docs/guide/README.md) and the [step logs](docs/guide/log/).

## What it does (target)

- **Import the world:** read a save file into a graph of machines, belts, splitters, mergers and pipes ([ADR-0012](docs/adr/0012-diagnose-the-world-from-the-save-file.md)).
- **Define factories:** mark a factory's output belt and name its input items. The tool finds every machine in between ([business rules](docs/domain/business-rules.md), BR-W).
- **Diagnose:** simulate the flow through the real network and show what each machine gets ("this constructor gets 60%"), belts that are too slow, and items leaving a factory that shouldn't.
- **Track over time:** each save is a snapshot, so you can see which flaws are new and which are fixed.
- **Later:** plan changes and check them against the same analysis before you build them.

## Getting the code

The save-file reader is a git submodule ([ADR-0013](docs/adr/0013-read-the-save-with-satisfactorysavenet.md)), so clone with:

```bash
git clone --recurse-submodules https://github.com/tmaartent/SatisfactoryLedger.git
# or, in an existing clone:
git submodule update --init
```

## After a game update

1. Import the game's Docs file (`CommunityResources/Docs/en-US.json`) into the Catalog.
2. Write the resource node table from the game install, then import it into the Catalog:

   ```bash
   dotnet run --project tools/SatisfactoryLedger.MapExtractor -- --game "<game folder>" --game-version 1.2
   # then POST resource-nodes.json as "file" to /api/v1/catalog/resource-node-imports
   ```

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
    World/              the world from the save file, factories, analysis (core domain)
      *.Domain          pure domain model, no framework references
      *                 application + infrastructure + endpoints (internal)
      *.Contracts       the only thing other modules may reference
tests/
  *.Domain.Tests        fast unit tests, written test-first
  ArchitectureTests     enforce the module and layer rules
  Api.IntegrationTests  HTTP-level tests against a real database
web/                    Angular frontend
docs/                   architecture, domain, ADRs, learning guide
tools/
  SatisfactoryLedger.MapExtractor/    Writes the resource node table from a game install (ADR-0015)
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
