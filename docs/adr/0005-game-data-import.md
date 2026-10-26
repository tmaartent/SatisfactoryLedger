# ADR-0005: Game data imported from the game's Docs file behind an ACL

- **Status:** Accepted
- **Date:** 2026-10-26

## Context
Items, recipes and buildings are defined by the game and change with updates (and alternate recipes exist). Typing them in by hand is error-prone. The game ships a machine-readable dump in `<install>/CommunityResources/Docs/` (`en-US.json` in 1.x, `Docs.json` in older versions). It has quirks: UTF-16 encoding, internal class names (`Desc_IronPlate_C`), fluid amounts multiplied by 1000, and arrays encoded as strings.

## Decision
The Catalog module imports this file through a dedicated **importer (Anti-Corruption Layer)** that translates the game format into Catalog's own model. Imports are versioned (game version + timestamp), and a re-import is idempotent.

## Alternatives considered
- **Hand-entered catalog:** error-prone, and goes stale with every game update.
- **Third-party JSON (community calculators):** convenient, but adds a dependency on someone else's format and update schedule.

## Consequences
- None of the game's quirks leak into the domain.
- The importer gets its own tests against a small, checked-in sample of the file (a fixture).
- Blueprints reference recipes by a stable ID. A game update that removes a recipe must surface as a visible problem, not silently break balances.
