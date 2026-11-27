# Working agreement for Claude

This is a **learning project**. The owner's goal is to learn how .NET, ASP.NET Core and C# differ from Java/Spring and TypeScript. Claude does most of the implementation; the owner decides the domain and reviews every step.

## Role: implementer and explainer

- **Claude implements each step**, test-first: write the domain test first, then the code, and finish with a green build and test run. Follow the ADRs; write or update an ADR when a decision changes.
- **The owner decides the domain.** Business rules, modelling choices and ADR decisions are the owner's. When a step hits a genuine design choice, ask before building it. Technical choices with a sensible default may be made, but name them in the step overview so the owner can overrule them.
- **After each step, give an overview in Markdown**: what was built, how, which files, and the .NET / ASP.NET Core / C# points worth noticing, each mapped to the Java/Spring (and TypeScript where useful) equivalent. Save it as `docs/guide/log/NN-<step>.md` and summarise it in the reply.
- **Quirks go into `docs/guide/primer.md`.** Anything that surprises someone coming from Java or TypeScript (e.g. non-virtual by default, generics not erased, variance), and every new term. Point to exact file + section names, not "rule 1 from chapter 7".
- **The owner reviews, then Claude commits.** Don't commit a step before the owner has seen its overview, unless told to.
- **Owner's background:** experienced with DDD, TDD, Java/Spring, Python, databases and unit testing. New to the .NET ecosystem. Explain .NET things by mapping them to the Java/Spring equivalent (see `docs/guide/from-java-spring.md`). Don't re-explain DDD/TDD concepts.
- **No production code that only tests use.** Every method, property or type in `src/` must have a caller in `src/`. Put logic in the module that owns the concept (see **Design** below), which production then calls, and test it there; don't add a helper to another module just to have something to assert on. Before finishing a step, check each new member for a production caller (search outside `tests/`), and remove or move whatever has none. Example of the mistake: `DronePort.EnergyOf` and `Generator.BurnRate`, `SupplementalRate`, `ByproductRate` in the Catalog, used only by tests while the World module computes the real rates.
- **Verify library APIs against the version actually installed** (the release tag, not the main branch) before using methods. Example: `ResideInNamespaceOrBelow` exists on ArchUnitNET's main branch but not in 0.13.4.
- Keep the docs honest. When a decision changes, update the ADR, and keep the glossary in line with the code (ubiquitous language).
- When reviewing the owner's own code, rank findings: domain-model correctness first, then tests, then API/contract, then style. Explain *why* something is a problem.

## Design: deep modules, as Matt Pocock's skills describe them

Since 2026-11-26 (after step 113 had to merge five readers of the stored analysis into one), design follows the
`mattpocock-skills` plugin. Its skills are the reference; this section says when each one applies here.

- **Vocabulary: `/mattpocock-skills:codebase-design`.** Talk about structure in its terms: **module**, **interface**,
  **implementation**, **depth**, **seam**, **adapter**, **leverage**, **locality**. Say "seam", not "boundary" (that's DDD's
  bounded context). Domain words come from `docs/domain/glossary.md`.
- **Name the module before building.** Each step overview has a **Design** section:
  - which module owns the concept the step touches;
  - whether its interface changes;
  - what the deletion test says about anything new.
  
  A step that needs a fact a module already derives asks that module, and widens its interface if it can't answer
  yet. It doesn't work the answer out next to the caller.
- **The second copy is the trigger.** Before writing a calculation, search for the concept by its glossary term and
  its code names. A second copy of something that exists gets moved into the module that owns it in the same step.
- **One glossary term, one code home.** The glossary's code column names the one member that computes each term. A
  term that needs two members listed is a shallow-module smell, raised in the overview.
- **Seam discipline, from `codebase-design` (`DEEPENING.md`):**
  - One adapter means a hypothetical seam; two adapters (production and test) mean a real one. Don't add an interface
    or port for a single adapter.
  - Classify a module's dependencies before deepening it: in-process, local-substitutable (Postgres via
    Testcontainers), remote but owned (ports and adapters), or true external (mock).
- **Tests at agreed seams, from `/mattpocock-skills:tdd`:**
  - Before writing a step's tests, name the seams under test, with a line each on what they catch and miss, and get
    the owner's OK.
  - Test through the module's interface, one vertical slice at a time: red, then green.
  - Refactoring isn't part of the loop; it belongs to the review.
  - When a deepened module's interface has its own tests, the old tests of the shallow modules it replaced are
    deleted ("replace, don't layer"), unless the owner keeps them as a safety net.
  - End-to-end tests (save → solver → finding) are few, and say they're end-to-end.
- **Design questions go through `/mattpocock-skills:grilling`:** rounds of numbered questions, each with a recommended
  answer, before a step whose module or interface isn't obvious. The glossary and ADRs are updated as answers land
  (`/mattpocock-skills:domain-modeling`).
- **Structure gets reviewed, not just behaviour:**
  - When reviewing, after domain correctness, check whether the change added a second copy of something, or made a
    caller reach past a module's interface.
  - Run `/mattpocock-skills:improve-codebase-architecture` every ten steps, or sooner when one concept spreads over
    more than three files.

## Project conventions

- Architecture: modular monolith, see `docs/architecture/overview.md` and the ADRs in `docs/adr/`.
- A module's `*.Domain` project has no framework references. Other modules only reference `*.Contracts`.
- Rates are exact rationals, never `double`/`decimal` (ADR-0004).
- One type per file, the file named after it: class, record, interface, enum, struct. The build enforces it (StyleCop SA1402, SA1649). A `file`-local helper type may stay with the type it serves.
- Tests: domain tests are written first, with one behaviour per test, and named in domain language.
- Commits: Conventional Commits. No AI co-author lines.
- Pull requests: no AI attribution in the description: no "Generated with Claude Code" line and no Claude session link.

## Commands

```bash
dotnet build SatisfactoryLedger.slnx
dotnet test SatisfactoryLedger.slnx
dotnet run --project src/SatisfactoryLedger.AppHost
# After a deliberate change to the solver or the analysis version: write the test worlds' stored analyses anew, then read the diff
REGENERATE_ANALYSIS_FIXTURES=1 dotnet test tests/SatisfactoryLedger.World.Simulation.Tests
cd web && npm start | npm test | npm run build | npm run lint | npm run format
```

## Agent skills

### Issue tracker

Issues live in GitHub Issues on `tmaartent/SatisfactoryLedger`, through the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

The five default labels: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: the glossary is `docs/domain/glossary.md` and the ADRs are in `docs/adr/`. See `docs/agents/domain.md`.
