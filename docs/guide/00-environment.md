# 00 · Environment

## Installed already
- .NET SDK 10.0.403 (pinned in `global.json`, rolls forward to newer feature bands)
- Aspire 13 project templates
- Node.js 22.23 + npm (Angular CLI 22 needs Node ≥ 22.22)

## Still to install
| Tool | Why |
|---|---|
| **Docker Desktop** | Aspire runs PostgreSQL in a container. Testcontainers needs it for integration tests. |
| **IDE:** JetBrains Rider (free for non-commercial use) or Visual Studio 2026, or VS Code with C# Dev Kit | Rider has the best refactoring support for DDD-style code. |
| **Angular Language Service** (VS Code) or Rider's built-in Angular support | Template type checking in the editor. |
| `dotnet dev-certs https --trust` | Trust the local HTTPS certificate once. |
| **GitHub CLI** (`gh`) | Create the repo and PRs from the terminal. |

## Verify the scaffold

```bash
dotnet build SatisfactoryLedger.slnx                 # must be 0 warnings, 0 errors (warnings are errors)
dotnet test SatisfactoryLedger.slnx                  # exits with code 8 = "no tests found". Your first test fixes that.
dotnet run --project src/SatisfactoryLedger.AppHost  # opens the Aspire dashboard. The "api" resource should be running.
cd web && npm start                                  # http://localhost:4200
cd web && npm test                                   # Vitest, 2 passing template tests
```

## What's in the scaffold, and why

| File | Purpose | Best practice it shows |
|---|---|---|
| `global.json` | Pins the SDK | Reproducible builds on every machine and in CI |
| `Directory.Build.props` | Shared settings: net10.0, nullable, **warnings as errors**, latest-recommended analyzers | One place for project-wide policy, so csproj files stay tiny |
| `Directory.Packages.props` | **Central Package Management**: all NuGet versions in one file | No version drift between projects. Add packages with `dotnet add package X` and the version lands here. |
| `SatisfactoryLedger.slnx` | The new XML solution format (.NET 10 default) | Readable, merge-friendly |
| `.editorconfig` | Code style, enforced at build (`EnforceCodeStyleInBuild`) | Style is checked by tools, not discussed in reviews |
| `src/…AppHost` | Aspire orchestrator | One command starts the whole system, with dashboards included |
| `src/…ServiceDefaults` | OpenTelemetry, health checks, resilience | Observability from day one, without effort |
| Module projects | Empty, with references already following the [dependency rules](../architecture/overview.md#dependency-rules-enforced-in-testssatisfactoryledgerarchitecturetests) | The compiler prevents wrong references |

## Exercise
Open `SatisfactoryLedger.slnx` in your IDE and look at the project references of `SatisfactoryLedger.FactoryNetwork`. Explain to yourself why it may see `BlueprintDesign.Contracts` but not `BlueprintDesign.Domain`. If you can, you understand the architecture.
