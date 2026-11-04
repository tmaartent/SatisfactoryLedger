# ADR-0012: Diagnose the world from the game's save file; planning comes later

- **Status:** Accepted
- **Date:** 2026-11-04

## Context
The project started as a replacement for an Excel sheet in which the player designs blueprints, stacks them into factories and connects them with supply lines (BR-B, BR-F, BR-S). That sheet only holds what the player typed in, so it's an incomplete picture of the world. It can't see a Mk.2 belt segment in the middle of a Mk.5 line, a machine overclocked beyond its input, or an output with nowhere to go.

The game's save file (`.sav`) contains the built world: every machine with its recipe, clock speed and somersloops, every belt and pipe segment with its tier, and how they are connected. The flaws the player wants to find are all visible in that data:
- supply lines stretched thin,
- machines not configured for the input they actually get,
- outputs on belts that can't carry the amount.

## Decision
We will build a **diagnosis tool first**: the world is imported from the save file, and the analysis reports flaws in it.

- **Diagnosis now, planning later.** Designing blueprints and placing them (BR-B, BR-F) is on hold. Planning returns later as a hypothetical part of the same graph, checked by the same analysis.
- **Simulate as faithfully as possible.** The analysis computes the steady-state flow through the real network, including splitters, mergers and back-pressure. It reports per machine what share of its demand it receives ("this constructor gets 60%").
- **The player defines factories within the graph.** A factory is defined by what enters it and what leaves it. Every machine in between belongs to that factory. The game has no such concept, so the definition is the player's own data.
- **Track the world over time.** Each import is a snapshot. Machines keep their identity from save to save, so the tool can show what changed and which flaws are new or resolved.

How the save file is read (a library or our own reader) is a separate decision. It depends on a spike against real saves.

## Alternatives considered
- **Keep the design tool (blueprints as the source of truth):** it reproduces the Excel sheet and has the same blind spot. The player wants to find flaws in what's actually built.
- **Blueprint as the plan, save as the reality, and compare the two:** powerful, but matching machines in the save to steps in a plan is hard and error-prone. It can come back later on top of this decision.
- **Capacity checks only, no flow simulation:** much simpler ("this can never be enough"), but it can't say which machine starves or by how much. The player chose the faithful model.

## Consequences
- The Catalog stays and becomes more important. The save only stores class names; recipes, building slots and belt capacities come from the Catalog.
- The per-machine rate rules (BR-B2, BR-B9, BR-B10, BR-B11) still apply. The other BR-B and BR-F rules are on hold.
- The BR-S rules were written for an abstract list of taps and injections. They have to be rewritten as rules on the real graph. BR-S3 (shortage shared proportionally) describes an idealised manifold. Real splitters behave differently, so the faithful model replaces it.
- The core of the domain moves from aggregates guarding commands to an **analysis on an imported, read-only snapshot**. What the player owns is small: factory definitions, intent (an intended surplus, a deliberate sink) and acknowledged findings.
- The save format changes with game updates, so the reader must sit behind an anti-corruption layer, as ADR-0005 does for the Docs file.
- The module structure in `docs/architecture/overview.md` (BlueprintDesign, FactoryNetwork) no longer fits. It will be revised in its own ADR once the spike shows what the save provides.
