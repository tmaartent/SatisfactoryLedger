# Primer: the terms used in this guide

Every technical term in the guide, in plain language: what it is, and where you'll meet it in this project. Read a section when a chapter mentions a term you don't know. You don't need to learn all of it up front.

## .NET basics

| Term | What it is | In this project |
|---|---|---|
| **.NET 10** | Microsoft's free, cross-platform platform for running C# programs. Version 10 is the current *LTS* (Long Term Support, 3 years of updates). | Everything in `src/` and `tests/` |
| **C# 14** | The language version that comes with .NET 10. | All backend code |
| **SDK** | The toolkit for building .NET: compiler plus the `dotnet` command. | Installed. `global.json` pins the version. |
| **`dotnet` CLI** | The command-line tool: `dotnet build`, `dotnet test`, `dotnet run`, `dotnet add package`. Rider runs the same commands behind its buttons. | Daily use |
| **Project (`.csproj`)** | One unit that compiles into one **assembly** (a `.dll` or `.exe`). | Each folder under `src/` and `tests/` |
| **Solution (`.slnx`)** | A list of projects that belong together, the file you open in Rider. `.slnx` is the new, readable XML format. | `SatisfactoryLedger.slnx` |
| **Project reference** | "Project A may use the code of project B." Without a reference, the compiler refuses. That's how we enforce part of the architecture. | Already set up between the modules |
| **Assembly** | The compiled output of a project. Architecture tests inspect assemblies. | — |
| **NuGet / package** | .NET's library store, like npm for JavaScript or Maven for Java. | `dotnet add package <Name>` |
| **Central Package Management** | All package versions in one file instead of in each project. | `Directory.Packages.props` |
| **`Directory.Build.props`** | Settings that automatically apply to every project in the folder tree. | Root of the repo |
| **Nullable reference types** | The compiler warns when something might be `null` and you don't check for it. It prevents the classic `NullReferenceException`. | On everywhere |
| **Warnings as errors** | Every compiler warning fails the build. Strict, but it keeps the code clean. | On everywhere |
| **Analyzers** | Extra code checks that run during the build (style, common mistakes). | `AnalysisLevel` in `Directory.Build.props` |
| **`record`** | A short C# way to define a type that's compared by its *values* instead of its identity. Ideal for value objects. | `Rate`, `ClockSpeed` (M1) |

## Backend

| Term | What it is | In this project |
|---|---|---|
| **ASP.NET Core** | The .NET framework for websites and web APIs. Receives HTTP requests and sends responses. | `SatisfactoryLedger.Api` |
| **Web API** | A program that the frontend talks to over HTTP with JSON, rather than a page you look at directly. | The backend as a whole |
| **Minimal APIs** | The compact ASP.NET Core style: one line per endpoint (`app.MapGet("/items", ...)`) instead of controller classes. | All endpoints (M2+) |
| **Endpoint** | One URL + HTTP method combination, e.g. `GET /api/v1/blueprints`. | Chapter 05 |
| **Dependency injection (DI)** | Classes ask for what they need in their constructor, and the framework supplies it. That makes them swappable and testable. | Built into ASP.NET Core |
| **EF Core (Entity Framework Core)** | Microsoft's ORM: saves and loads C# objects to and from a database without hand-written SQL. | Persistence, from M2 |
| **ORM** | Object-Relational Mapper: translates between objects (code) and tables (database). | EF Core is one |
| **`DbContext`** | The EF Core class that represents a database connection plus a set of tables. We give each module its own. | M2 |
| **Migration** | A generated script that changes the database structure (adds a table or column) when your model changes. | M2+ |
| **PostgreSQL** | A free, widely used relational database. Runs in Docker. | ADR-0007, from M2 |
| **Docker / container** | Runs software (such as PostgreSQL) in an isolated box without installing it on your machine. | Started automatically by Aspire and Testcontainers |
| **.NET Aspire** | A tool that starts all parts of the app (API, database, frontend) with one command and shows a **dashboard** with logs and traces. | `SatisfactoryLedger.AppHost`: run this |
| **ServiceDefaults** | Aspire's shared setup for logging, health checks and resilience, added to every service with one line. | `SatisfactoryLedger.ServiceDefaults` |
| **OpenTelemetry** | Standard for *observability*: logs, metrics and traces. You can see what the app does and how long it takes. | Visible in the Aspire dashboard |
| **Health check** | A URL (`/health`) that tells whether the app and its database are working. | Mapped by ServiceDefaults |

## Testing

| Term | What it is | In this project |
|---|---|---|
| **xUnit (v3)** | The test framework: you write methods marked `[Fact]` (one case) or `[Theory]` (the same test with several data rows). | All test projects |
| **Microsoft Testing Platform (MTP)** | The new engine that runs .NET tests. It's what `dotnet test` and Rider use underneath. | — |
| **Exit code 8** | MTP's signal for "no tests found". It fails the run on purpose, so an empty test project doesn't look like a success. | Until your first tests exist |
| **Unit test** | Tests a small piece of logic in isolation, in milliseconds. | `*.Domain.Tests` |
| **Integration test** | Tests several parts together for real (API + database). | `Api.IntegrationTests` (M2+) |
| **Architecture test** | A test that checks the *structure* of the code (who may reference whom) instead of its behaviour. | `ArchitectureTests` (M0) |
| **ArchUnitNET / NetArchTest** | Two libraries for writing architecture tests ("types in assembly X must not depend on Y"). You pick one. | M0 |
| **Testcontainers** | Starts a real PostgreSQL in Docker for the duration of a test run, then cleans it up. | M2+ |
| **`WebApplicationFactory`** | Starts your API in memory inside a test, so you can send real HTTP requests to it. | M2+ |
| **Property-based testing / CsCheck** | Instead of picking examples yourself, you state a rule ("flow never exceeds capacity"), and the library tries hundreds of random inputs to break it. | M1, M5, M7 |
| **Mock** | A fake replacement for a dependency in a test. We avoid mocking the domain. | Chapter 03 |
| **Vitest** | Test framework for the Angular frontend. | `web/` (`npm test`) |
| **Playwright** | Drives a real browser to test complete user journeys (end-to-end). | Later milestones |

## API design

| Term | What it is | In this project |
|---|---|---|
| **REST** | A style of API design built around *resources* (blueprints, factories) and HTTP methods (GET = read, POST = create, PUT = replace, PATCH = change, DELETE = remove). | Chapter 05 |
| **Status code** | The number in an HTTP response: 200 OK, 201 Created, 404 Not Found, 422 business rule violated, and so on. | Chapter 05 |
| **JSON** | The text format in which the API and frontend exchange data. | — |
| **OpenAPI** | A machine-readable description of the whole API: all endpoints and data shapes. Generated by ASP.NET Core. | ADR-0006 |
| **Scalar** | A web page generated from the OpenAPI document where you can browse and try out the API. | Development |
| **Generated client** | TypeScript code generated *from* the OpenAPI document, so the frontend always matches the backend. | `web/src/app/api/` (M3) |
| **ProblemDetails** | A standard JSON format for error responses (RFC 9457). | All errors |
| **ETag / If-Match** | A version stamp on a resource. It prevents two edits from overwriting each other ("optimistic concurrency"). | Chapter 05 |
| **Versioning (`/api/v1`)** | The version in the URL, so you can make breaking changes later without breaking old clients. | All routes |

## Frontend

| Term | What it is | In this project |
|---|---|---|
| **Angular (22)** | Google's framework for single-page web apps, written in TypeScript. | `web/` |
| **TypeScript** | JavaScript with types. | All frontend code |
| **SPA (single-page application)** | The browser loads the app once, and after that only data is fetched from the API. | — |
| **Component** | A building block of the UI: a TypeScript class plus an HTML template plus styles. | Everything you see |
| **Standalone component** | A modern component that declares its own dependencies (no `NgModule`). The default in Angular 22. | — |
| **Signals** | Angular's mechanism for reactive state: when a value changes, everything that depends on it updates automatically. | All UI state |
| **Zoneless** | Angular without Zone.js, the old trick for detecting changes. Signals do that job now. | Default in the scaffold |
| **npm** | The package manager for JavaScript (installs Angular and the other libraries). | `npm install`, `npm start` |
| **orval / openapi-generator** | Tools that generate the TypeScript client from the OpenAPI document. | M3 |

## Architecture & DDD

| Term | What it is | In this project |
|---|---|---|
| **DDD (Domain-Driven Design)** | Designing software around the business domain and its language. | The whole project |
| **Ubiquitous language** | The same words in conversation, docs and code. | `docs/domain/glossary.md` |
| **Bounded context** | A part of the domain with its own model and language. | Catalog, Blueprint Design, Factory Network |
| **Module** | The code that implements one bounded context. | `src/Modules/*` |
| **Modular monolith** | One application, internally divided into strictly separated modules. | ADR-0002 |
| **Microservice** | A module that runs as its own separate program and talks over the network. | Maybe in M8 |
| **Value object** | An immutable object defined by its value (`Rate` 45/8). | M1 |
| **Entity** | An object with its own identity that can change over time (`ProductionStep`). | M3 |
| **Aggregate / aggregate root** | A group of objects that must stay consistent together, changed only through the root (`Blueprint`). | M3+ |
| **Invariant** | A rule that must always hold ("stack height ≥ 1"). | Business rules |
| **Domain service** | Logic that doesn't belong to one object (`SupplyLineAnalyzer`). | M5 |
| **Domain event / integration event** | A record that "something happened". Integration events cross module boundaries. | M4 (BR-F6/F7) |
| **Outbox** | A pattern that stores events in the same database transaction as the change, then sends them, so nothing is lost. | M4 |
| **ACL (Anti-Corruption Layer)** | Translation code that keeps an external format (the game's data file) out of your model. | Catalog importer, M2 |
| **ADR (Architecture Decision Record)** | A one-page document that records a decision and its reasons. | `docs/adr/` |
| **CI (Continuous Integration)** | A server (GitHub Actions) that builds and tests every change automatically. | M0 |
