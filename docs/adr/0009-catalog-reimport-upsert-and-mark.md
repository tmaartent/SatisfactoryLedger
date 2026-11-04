# ADR-0009: A catalog re-import updates by class name and keeps entries the game dropped

- **Status:** Accepted
- **Date:** 2026-10-30

## Context
ADR-0005 says imports are versioned and idempotent, and that a game update which removes a recipe must surface as a visible problem rather than silently break balances. It doesn't say *how*. The Docs file has no version number, and it arrives from the player's own game install.

## Decision
- **Upsert by class name.** Every catalog entry (item, building, transport tier, recipe) is identified by the game's class name (`Recipe_IronPlate_C`). An import adds entries that are new, and updates stored entries from the file.
- **Mark, don't delete.** A stored entry that the new file no longer contains stays in the catalog, marked **no longer in the game** (`IsInGame = false`). The import record lists the class names it marked. An entry that comes back in a later file is in the game again.
- **Upload in the request.** `POST /api/v1/catalog/imports` takes the file as `multipart/form-data`.
- **The caller gives the game version.** The version is a form field next to the file. It's stored with the import's timestamp and its counts per kind of entry (added, updated, no longer in the game).

## Alternatives considered
- **Upsert and delete missing entries:** simpler, but a blueprint could then point at a recipe that no longer exists.
- **A full catalog snapshot per import:** the most history, but every query would need "which snapshot", and blueprints would have to pin one.
- **Read the file from a path on the server:** simpler, but only works when the API runs on the player's machine.

## Consequences
- References from other modules (blueprints → recipes) stay valid across game updates. Later milestones can show "this recipe is no longer in the game" instead of failing.
- Re-importing the same file changes nothing, which is what makes the import idempotent.
- The catalog only grows. Cleaning up entries that are long gone is a future, explicit action.
- The rule lives in the domain (`CatalogEntries.Reconcile`), so it is unit-tested without a database.
