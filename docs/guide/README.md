# Learning guide

You write the code. These chapters tell you **what** to build in which order, **why**, and **which practices** a senior reviewer would expect to see. Each chapter ends with a *Done when* checklist. Don't move on until it's ticked.

## Chapters (by discipline)

| # | Chapter | You'll practise |
|---|---|---|
| 00 | [Environment](00-environment.md) | Toolchain, IDE, Docker, running the scaffold |
| 01 | [Workflow](01-workflow.md) | Git, commits, branches, PRs, CI from day one |
| 02 | [Domain modelling](02-domain-modelling.md) | Ubiquitous language, value objects, aggregates, invariants, domain services, events |
| 03 | [Test-driven development](03-tdd.md) | Red-green-refactor, test naming, builders, property-based tests |
| 04 | [Application layer & persistence](04-application-and-persistence.md) | Use cases, EF Core mapping of a rich model, migrations, Testcontainers |
| 05 | [API design](05-api-design.md) | Resource modelling, status codes, ProblemDetails, versioning, OpenAPI, concurrency |
| 06 | [Frontend](06-frontend.md) | Angular 22 architecture, signals, generated client, the network graph, testing |
| 07 | [Quality & delivery](07-quality-and-delivery.md) | Architecture tests, analyzers, CI pipeline, observability |
| 08 | [Towards microservices](08-towards-microservices.md) | Outbox, integration events, extracting a module into a service |

## Roadmap (by feature)

Each milestone is a **vertical slice**: domain → persistence → API → UI, test-first throughout. You use every chapter many times, not once.

| Milestone | Delivers | Business rules | Chapters to read first |
|---|---|---|---|
| **M0 Foundation** | Repo on GitHub, CI green, first architecture test | — | 00, 01, 07 (architecture tests section) |
| **M1 Rate** | `Rate` value object in SharedKernel, fully tested | ADR-0004 | 02 (value objects), 03 |
| **M2 Catalog** | Import the game's Docs file, browse items and recipes via the API | ADR-0005 | 02, 04, 05 |
| **M3 Blueprints** | Create a blueprint, add steps, see the item balance in green/red. **First end-to-end slice with UI.** | BR-B1…B16 | 02 (aggregates), 05, 06 |
| **M4 Factories** | Place a blueprint stacked N high, max stack height | BR-F1…F7 | 02 (invariants), 08 step 1 (integration events + outbox, needed for BR-F6/F7) |
| **M5 Supply lines** | Lines with taps and injections, line analysis, overflow lines | BR-S1…S16 | 02 (domain services), 03 (property-based) |
| **M6 World view** | Whole network as a graph, deficits in red, spare capacity visible | BR-S9 | 06 (graph) |
| **M7 Suggestions** | Propose tap order and merge points | BR-P1…P4 | 02 (strategy), 03 |
| **M8 Extraction** | Catalog as a separate service with an ADR | — | 08 |

**Tip:** M3 is the most important milestone. Once one slice works end to end, every later feature follows the same pattern, and your repo is already presentable.

## How to work with Claude in this project

`CLAUDE.md` sets Claude up as a **mentor and reviewer**. Useful prompts:
- "Review my `Rate` implementation and tests against ADR-0004 and chapter 03."
- "I'm about to model `SupplyLine`. Ask me questions until my design is sound. Don't give me code."
- "Which business rules from BR-S do my tests not cover yet?"
- "Is this an entity or a value object? Help me reason about it."
