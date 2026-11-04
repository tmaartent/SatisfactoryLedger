# ADR-0013: Read the save file with SatisfactorySaveNet's v1.2 branch, behind an anti-corruption layer

- **Status:** Accepted
- **Date:** 2026-11-04

## Context
ADR-0012 makes the save file the source of the world. The saves are from game version 1.2. A spike ([log 03](../guide/log/03-spike-save-file.md)) tried the options against the owner's real saves:

- `SatisfactorySaveNet` is an MIT-licensed C# reader with a single maintainer. Its latest release, **4.0.0-alpha.3**, fails on a 1.2 save (the same error as its open issue #14).
- Its unreleased branch **`fix/v1.2`, commit `dd994a6`**, reads the 1.2 saves in about 5 s and about 740 MB. That commit is a large, AI-assisted rewrite of the parser that nobody has reviewed yet.
- Everything the diagnosis needs is in the save: recipes, clock speeds, somersloops, belt tiers, splitter and merger settings, and a complete, two-sided connection graph.
- The save format changes with game updates, so any reader will break again.

## Decision
We will read the save with **SatisfactorySaveNet at commit `dd994a6`**, built from source, and hide it behind an **anti-corruption layer**.

- **Source:** the fork [tmaartent/SatisfactorySaveNet](https://github.com/tmaartent/SatisfactorySaveNet/tree/fix/v1.2), added as a git submodule under `external/SatisfactorySaveNet` and pinned to that commit. The fork protects us if the upstream branch is force-pushed or deleted. CI checks out submodules.
- **Boundary:** only one adapter project references the library. It's a separate project, not a folder, so the compiler stops other code from using the library. That's one project more than ADR-0003's three per module, accepted for a large, unreviewed dependency. An architecture test checks that no other assembly uses the `SatisfactorySaveNet` namespace. The adapter translates the save into our own read-only model (buildings, connectors, links, settings), applying the rules from log 03: a missing property means the default, clock speeds are rounded to 4 decimals before they become exact rationals, and links must agree on both ends. No library type crosses the adapter.
- **Tests:** the adapter is tested against a small 1.2 save committed as a fixture, like the Docs-file fixtures. If a save can't be read, the import fails with a clear error, never with a partial world.
- **Upstream:** when a stable release reads 1.2 saves, we switch to the NuGet package and drop the submodule.
- *Amended 2026-11-04:* the pin moved to **`b329a9b`** on the fork: our own one-line fix on top of `dd994a6`. Saves of game build 517554 lack the persistent level custom version, the library adds it to a fixed-size array, and that threw. Fixes like this go on the fork's `fix/v1.2` branch, so the pin always points at a commit that exists there.
- *Amended 2026-11-05:* the pin moved to **`b85249a`**: a second fix on the fork. In a Debug build the library took two stack traces for every string it read, so a big save took more than 5 minutes instead of 13 seconds. That tracing is now opt-in (`TRACE_STRINGS`).

## Alternatives considered
- **4.0.0-alpha.3 from NuGet:** the simplest option, but it can't read the owner's saves.
- **Our own reader:** full control and a lot of .NET to learn (binary reading, decompression, `Span<T>`), but weeks of work before the first diagnosis, and we'd own every format change. It remains the fallback if the library stalls. The anti-corruption layer keeps that switch local.
- **A parser in another language (TypeScript, Rust) as a separate process:** mature options exist, but it adds a second runtime and a process boundary for a single import.
- **Referencing the upstream branch directly, without a fork:** one less repository, but a force-push upstream could make our pinned commit disappear.

## Consequences
- We depend on unreviewed code. The fixture tests and the two-sided link check are our safety net. Parsing errors surface as failed imports.
- Importing needs about 1 GB of memory for a large save. That's acceptable for an occasional, local import. The library's objects are dropped after translation.
- A game update can break imports until the library catches up. Snapshots that were already imported keep working.
- The repository gets a submodule: `git clone --recurse-submodules`, and `submodules: true` in CI.
