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
| Layout | The pages on a phone (Chromium and WebKit), a tablet and a desktop, against recorded API answers | Playwright | per rule a page keeps |
| End-to-end | A few critical user journeys, against the real stack | Playwright | a handful |

Rules: don't mock the domain. Mock only at process boundaries, and prefer a real database over a mock. Name tests after business rules (`BR-S4`) and behaviours, not methods.

## Amendment (2026-11-24, step 95): layout tests on recorded answers
The owner asked how to keep the touch-screen work of steps 90 to 93 from breaking again, and chose:
- **both** kinds of Playwright test: layout tests against recorded API answers in every CI run, and a few journeys against the real stack (step 97);
- **assertions**, not screenshot comparisons: what a rule says is measured (no sideways scroll, the canvas as big as its stage, the choices in view), so fonts and rendering that differ between machines don't matter;
- **four devices:** a Pixel 7 (Chromium), an iPhone 15 (WebKit), an iPad (WebKit) and a 1280 × 800 desktop with a mouse.

The answers are recorded from the running app for the test world (`Development.sav`) by `web/e2e/record.mjs`, into `web/e2e/fixtures/api.json`. Map art and icons, the game's art, aren't recorded; nor are the player's other worlds or their resource-node table. A test fails when a page asks for something not recorded. The recording doesn't follow the API by itself: when an answer's shape changes, record again. That gap is what tests against the real stack close. *Since step 96 a check runs in every CI run too: each recorded answer is validated against the schema `api/openapi.json` gives for it (`e2e/recording.spec.ts`), so a changed API fails until the answers are recorded again.*

## Amendment (2026-11-25, step 97): journeys against the real stack
The owner chose:
- **one journey to start:** import `Development.sav` and see its map; define a factory of its machines in 3D and save it; its page shows what it makes; delete it. It leaves the database as it found it: the factory is deleted as its last step, the snapshot after it, and what a failed run left is cleared before the next;
- **after a merge to main only,** not on every PR, so a PR's checks don't wait for it;
- **the desktop only:** the layout tests cover defining a factory on every device; the journey checks that the web app, the API, the analysis service, PostgreSQL and RabbitMQ fit together.

In `web/e2e/stack/`, with its own config (`playwright.stack.config.ts`, `npm run e2e:stack`). Locally it runs against the AppHost. CI's job **journeys** starts PostgreSQL and RabbitMQ as service containers and the API and the analysis service with `dotnet run`, wired as the AppHost would: not the AppHost itself, which also starts Ollama on a GPU.

## Alternatives considered
- **In-memory EF provider for integration tests:** fast, but behaves differently from PostgreSQL (no real transactions or constraints) and gives false confidence.
- **Mock-heavy unit tests for handlers:** couple tests to implementation details and break on every refactor.

## Consequences
- Docker is needed for the integration tests.
- The domain stays easy to test only as long as it has no infrastructure dependencies, which ADR-0003 guarantees.
