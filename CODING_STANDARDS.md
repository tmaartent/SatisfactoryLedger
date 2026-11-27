# Coding standards

The judgement calls a review checks in this repo. The build already enforces one type per file (StyleCop SA1402, SA1649) and the module dependencies (`tests/SatisfactoryLedger.ArchitectureTests`), so they aren't repeated here.

Structure is described in the vocabulary of the `codebase-design` skill: **module**, **interface**, **implementation**, **depth**, **seam**, **adapter**, **leverage**, **locality**. Domain words come from `GLOSSARY.md`.

## Modules

- **Every member in `src/` has a caller in `src/`.** Logic lives in the module that owns the concept, which production calls and tests test; a helper added to another module just to have something to assert on is a finding. Example: `DronePort.EnergyOf` and `Generator.BurnRate`, `SupplementalRate`, `ByproductRate` in the Catalog, used only by tests while the World module computed the real rates.
- **The second copy moves home.** A calculation of a concept that a module already derives, written again next to its caller, is a finding: the caller asks the module, and the module's interface widens if it can't answer yet. Search for the concept by its glossary term and its code names. Example: "oldest factory" decided both by a sort in `MachineMatch` and by a comparison in `SnapshotFactories` (step 122).
- **Callers stay on the interface.** A caller that reaches past a module's interface into its implementation is a finding.
- **The interface is the one agreed.** A module interface wider than the spec or ticket agreed is flagged, so the owner can accept it or rework it.
- **Seams follow `codebase-design`:** one adapter means a hypothetical seam, two adapters a real one (see its `DEEPENING.md`).

## Domain

- **Rates are exact rationals** (`Rate`), never `double` or `decimal` (ADR-0004).
- **Names come from `GLOSSARY.md`.** A type, member, test or route named outside it is a finding, and so are "manager", "helper" and "util": a domain service is named after what it does.
- **A rule changed in code is changed in the docs:** the business rule in `docs/domain/business-rules.md`, the term in `GLOSSARY.md`, the ADR when a decision changes.
- **A case the ticket's rule doesn't hold for** is flagged, even when the step log calls it narrow: the owner decides the domain.
- **The model is not the game.** Docs, step logs, UI text and replies say what happens *in the model*; a model result stated as what happens in the owner's game is a finding.
- **Items and buildings go by their in-game display names** (Ficsite Trigon, not "ficsite mesh"), as the Docs file fixture names them.

## Commits

- **One feature per commit.** Unrelated changes go into separate commits, even when they touch the same files.

## Tests

- **One behaviour per test, named in domain language**, not after how a collection or method behaves.
- **Replace, don't layer:** when a deepened module's interface has its own tests, the old tests of the shallow modules it replaced are deleted, unless the owner keeps them as a safety net.
- **End-to-end tests are few**, and say they're end-to-end.
