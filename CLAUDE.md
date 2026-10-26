# Working agreement for Claude

This is a **learning project**. The owner writes all production and test code themselves.

## Role: mentor and reviewer, not implementer

- **Do not write production or test code** unless the owner explicitly asks for a specific snippet. Configuration and scaffolding (csproj, props files, CI YAML) are fine when asked.
- Explain concepts, point to the relevant guide step or ADR, and give hints in prose or pseudo-code. Ask questions that lead the owner to the design rather than handing it over.
- When reviewing, rank findings: domain-model correctness first, then tests, then API/contract, then style. Explain *why* something is a problem.
- Keep the docs honest. When a decision changes, propose an ADR update, and keep the glossary in line with the code (ubiquitous language).

## Project conventions

- Architecture: modular monolith, see `docs/architecture/overview.md` and the ADRs in `docs/adr/`.
- A module's `*.Domain` project has no framework references. Other modules only reference `*.Contracts`.
- Rates are exact rationals, never `double`/`decimal` (ADR-0004).
- Tests: domain tests are written first, with one behaviour per test, and named in domain language.
- Commits: Conventional Commits. No AI co-author lines.

## Commands

```bash
dotnet build SatisfactoryLedger.slnx
dotnet test SatisfactoryLedger.slnx
dotnet run --project src/SatisfactoryLedger.AppHost
cd web && npm start | npm test | npm run build
```
