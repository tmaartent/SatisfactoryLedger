# 07 · Quality & delivery

## Architecture tests (do this in M0, before any domain code)

Put the [dependency rules](../architecture/overview.md#dependency-rules-enforced-in-testssatisfactoryledgerarchitecturetests) into `tests/SatisfactoryLedger.ArchitectureTests` with **ArchUnitNET** or **NetArchTest.Rules**:

1. `*.Domain` assemblies don't depend on `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore`, or any other module.
2. A module's implementation depends only on other modules' `*.Contracts`.
3. `*.Contracts` has no dependencies on other project assemblies.
4. Types in a module's implementation project are not public, apart from the registration entry points.
5. (Later) Domain classes have no public setters. Value objects are immutable.

These tests are your safety net for ADR-0002. They're also an excellent talking point in interviews. Writing them first means the first `dotnet test` run passes for a real reason (and exit code 8 disappears).

## Static analysis

- Already on: nullable reference types, `latest-recommended` analyzers, **warnings as errors**, code style enforced at build.
- Consider: `Meziantou.Analyzer` or SonarAnalyzer.CSharp for extra rules. Treat any suppression as a decision. Use `.editorconfig` severity changes with a comment, not `#pragma` scattered around.
- Frontend (since step 75): ESLint with angular-eslint's recommended rules for TypeScript and templates, accessibility included (`npm run lint`, `eslint.config.js`); Prettier for formatting (`npm run format`, `npm run format:check`); `strictTemplates` on. TypeScript 6 is strict by default.

## CI pipeline (GitHub Actions)

One workflow, `.github/workflows/ci.yml`, on every PR and push to `main`:

| Job | Steps |
|---|---|
| **backend** | checkout → setup-dotnet (from `global.json`) → `dotnet restore` → `dotnet build --no-restore` → `dotnet test --no-build` (Testcontainers works on GitHub's Ubuntu runners) → upload test results |
| **contract** | A step in the **backend** job, right after the build (which regenerates `api/openapi.json`): fail when `git status --porcelain -- api/` isn't empty. `git status` rather than `git diff --exit-code`, because a new, untracked document doesn't show up in `git diff`. |
| **frontend** | setup-node 22 → `npm ci` → `npm run format:check` → `npm run lint` → `npm test -- --watch=false` (its `pretest` generates the API client) → `npm run build` |
| **e2e** | setup-node 22 → `npm ci` → `npx playwright install --with-deps chromium webkit` → `npm run e2e` (its `pree2e` generates the API client; Playwright starts `ng serve` on port 4300, and the API's answers come from `web/e2e/fixtures/api.json`) → upload the report when it fails. Step 95, ADR-0008. |
| **journeys** | Only on a push to `main` (a merge) or by hand. PostgreSQL and RabbitMQ as service containers → setup-dotnet and setup-node → `dotnet build` → the API (`--launch-profile http`, port 5219, Development so it migrates) and the analysis service with `dotnet run`, in the background → `npm ci` → `npx playwright install --with-deps chromium` → `npm run e2e:stack` (Playwright starts `ng serve` on port 4301, its proxy to the API at `services__api__http__0`) → upload the report and the services' logs when it fails. Step 97, ADR-0008. |

Best practices: cache NuGet and npm, run jobs in parallel, keep the pipeline **under ~5 minutes**, and make it a required check on `main`. Add the status badge to the README.

## Observability (free with Aspire)

- ServiceDefaults already wires OpenTelemetry traces, metrics and logs into the Aspire dashboard.
- Use **structured logging** with message templates (`"Factory {FactoryId} restacked to {Height}"`), never string interpolation.
- Add a custom `ActivitySource` for the line analysis, so you can see in the dashboard how long it takes for large networks.
- Health checks: `/health` and `/alive` are mapped by ServiceDefaults. Add a database check when PostgreSQL arrives.

## Aspire AppHost evolution

- M2: `builder.AddPostgres("db").WithDataVolume().AddDatabase("ledger")` → `.WithReference(db).WaitFor(db)` on the API.
- M3: add the Angular dev server (`Aspire.Hosting.JavaScript` package) so one `dotnet run` starts everything, with the API URL injected through environment variables or a proxy config.
- Later: `aspire publish` / deploy targets if you ever want to host it. That's optional for an offline tool.

## Done when (M0)
- [ ] Architecture tests for rules 1–4 exist and pass
- [ ] CI runs backend and frontend jobs on PRs and is a required check
- [ ] README shows the CI badge
