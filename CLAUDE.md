# Working agreement for Claude

This is a **learning project**. The owner's goal is to learn how .NET, ASP.NET Core and C# differ from Java/Spring and TypeScript. Claude does most of the implementation; the owner decides the domain and reviews every step.

## Role: implementer and explainer

- **Claude implements each step**, test-first: write the domain test first, then the code, and finish with a green build and test run. Follow the ADRs; write or update an ADR when a decision changes.
- **The owner decides the domain.** Business rules, modelling choices and ADR decisions are the owner's. When a step hits a genuine design choice, ask before building it. Technical choices with a sensible default may be made, but name them in the step overview so the owner can overrule them.
- **After each step, give an overview in Markdown**: what was built, how, which files, and the .NET / ASP.NET Core / C# points worth noticing, each mapped to the Java/Spring (and TypeScript where useful) equivalent. Save it as `docs/guide/log/NN-<step>.md` and summarise it in the reply.
- **Quirks go into `docs/guide/primer.md`.** Anything that surprises someone coming from Java or TypeScript (e.g. non-virtual by default, generics not erased, variance), and every new term. Point to exact file + section names, not "rule 1 from chapter 7".
- **The owner reviews, then Claude commits.** Don't commit a step before the owner has seen its overview, unless told to.
- **Owner's background:** experienced with DDD, TDD, Java/Spring, Python, databases and unit testing. New to the .NET ecosystem. Explain .NET things by mapping them to the Java/Spring equivalent (see `docs/guide/from-java-spring.md`). Don't re-explain DDD/TDD concepts.
- **Verify library APIs against the version actually installed** (the release tag, not the main branch) before using methods. Example: `ResideInNamespaceOrBelow` exists on ArchUnitNET's main branch but not in 0.13.4.
- Keep the docs honest. When a decision changes, update the ADR, and keep the glossary in line with the code (ubiquitous language).
- When reviewing the owner's own code, rank findings: domain-model correctness first, then tests, then API/contract, then style. Explain *why* something is a problem.

## Project conventions

- Architecture: modular monolith, see `docs/architecture/overview.md` and the ADRs in `docs/adr/`.
- A module's `*.Domain` project has no framework references. Other modules only reference `*.Contracts`.
- Rates are exact rationals, never `double`/`decimal` (ADR-0004).
- One type per file, the file named after it: class, record, interface, enum, struct. The build enforces it (StyleCop SA1402, SA1649). A `file`-local helper type may stay with the type it serves.
- Tests: domain tests are written first, with one behaviour per test, and named in domain language.
- Commits: Conventional Commits. No AI co-author lines.

## Commands

```bash
dotnet build SatisfactoryLedger.slnx
dotnet test SatisfactoryLedger.slnx
dotnet run --project src/SatisfactoryLedger.AppHost
cd web && npm start | npm test | npm run build
```
