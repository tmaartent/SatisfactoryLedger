# ADR-0014: A World module replaces BlueprintDesign and FactoryNetwork

- **Status:** Accepted
- **Date:** 2026-11-04

## Context
The modules were cut for a design tool: BlueprintDesign (blueprints and their steps) upstream of FactoryNetwork (placed factories and supply lines). ADR-0012 changed the product: the world is imported from the save file and diagnosed, and factories are groups of machines the player defines in that world (BR-W1 to BR-W8). Both modules were still empty scaffolds.

## Decision
We replace both modules with one **World** module, in the three-project layout of ADR-0003:

- `World.Domain`: the world graph (buildables and connections), factory definitions and how their members are traced, and later the flow analysis and findings.
- `World`: the import use case, persistence and endpoints.
- `World.Contracts`: what other modules may see.

The save-file adapter from ADR-0013 becomes a fourth project, `World.SaveFile`, when the import is built.

BlueprintDesign and FactoryNetwork are removed. Planning (ADR-0012) returns later as its own module, built on the World's graph.

## Alternatives considered
- **Keep FactoryNetwork and put the world in it:** the name and its documentation describe placed blueprints and abstract supply lines, which no longer exist.
- **Separate modules for the world and for factories:** a factory is defined by and traced in the world graph. Splitting them would put the core rules on both sides of a module boundary, without a reason to deploy or change them separately.
- **Keep the empty modules for later:** dead scaffolding, and empty test projects forced CI to accept "zero tests ran".

## Consequences
- The core domain lives in one module, so the context map has two contexts: Catalog (supporting) and World (core).
- CI no longer needs `--ignore-exit-code 8`: every test project has tests, so a project that silently loses its tests fails the build.
- The learning guide's examples still mention the old modules in places. They describe the design tool and are marked as on hold where they matter.
