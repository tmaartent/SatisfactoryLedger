# Working agreement for Claude

This is a **learning project**. The owner's goal is to learn how .NET, ASP.NET Core and C# differ from Java/Spring and TypeScript. Claude does most of the implementation; the owner decides the domain and reviews the work.

## Workflow: Matt Pocock's skills

Work follows the `mattpocock-skills` plugin, and its skills are the reference: `grill-with-docs → to-spec → to-tickets → implement` (or `implement-spec`) `→ code-review → retro`. Where this file and a skill differ, the skill wins; this file only adds what the project needs on top.

- **The owner decides the domain:** business rules, modelling choices and ADR decisions, settled through `/grill-with-docs`. Technical choices with a sensible default may be made, but are named in the step log so the owner can overrule them.
- **Seams in an `/implement-spec` run** are the ones named in the spec or its tickets; they count as agreed for `tdd`.
- **Reviews** run `/code-review`, which reads `CODING_STANDARDS.md`.
- **PR bodies** are written with `/pr`.
- **`/improve-codebase-architecture`** runs every ten steps, or sooner when one concept spreads over more than three files.
- **A run that stops partway** ends with `/handoff`.

## Explaining .NET

- **Owner's background:** experienced with DDD, TDD, Java/Spring, Python, databases and unit testing; new to .NET. Explain .NET by mapping it to the Java/Spring equivalent (see `docs/guide/from-java-spring.md`), and don't re-explain DDD or TDD.
- **Each step gets a log**, `docs/guide/log/NN-<step>.md`, summarised in the reply: what was built, how, which files, and the .NET / ASP.NET Core / C# points worth noticing, each mapped to Java/Spring (and TypeScript where useful). Its **Design** section names the module that owns the concept, whether its interface changes, and what the deletion test says about anything new.
- **Quirks go into `docs/guide/primer.md`:** anything that surprises someone coming from Java or TypeScript, and every new term, pointing to exact file and section names.

## Project conventions

- Architecture: modular monolith, see `docs/architecture/overview.md` and the ADRs in `docs/adr/`.
- Commits: Conventional Commits. No AI co-author lines.
- Pull requests: no AI attribution in the description: no "Generated with Claude Code" line and no Claude session link.

## Commands

```bash
dotnet build SatisfactoryLedger.slnx
dotnet test SatisfactoryLedger.slnx   # the integration tests need Docker Desktop running (Testcontainers)
dotnet run --project src/SatisfactoryLedger.AppHost
# After a deliberate change to the solver or the analysis version: write the test worlds' stored analyses anew, then read the diff
REGENERATE_ANALYSIS_FIXTURES=1 dotnet test tests/SatisfactoryLedger.World.Simulation.Tests
cd web && npm start | npm test | npm run build | npm run lint | npm run format
npx ng test --include src/app/<path>.spec.ts --watch=false   # one web spec
npm run e2e | npm run e2e:stack   # pages on recorded answers | journeys, against the running AppHost
```

A new git worktree needs `git submodule update --init` before it builds; removing one takes `git worktree remove --force`, once its submodule shows no changes. To put a worktree's branch on the integration branch, use `git switch -C <branch> <integration-branch>`: a global git guardrail blocks `reset --hard`, `branch -D`, `push --force` and a push to a branch whose PR is merged.

Claude may stop and restart the AppHost whenever a step needs it.

## Agent skills

### Issue tracker

Issues live in GitHub Issues on `tmaartent/SatisfactoryLedger`, through the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

The five default labels: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `GLOSSARY.md` at the root and the ADRs in `docs/adr/`. See `docs/agents/domain.md`.
