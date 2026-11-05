# ADR-0015: Resource nodes come from the game's map, through a generated node table

- **Status:** Accepted
- **Date:** 2026-11-04

## Context
A miner or extractor in the save only points at its node (`BP_ResourceNode35`). The node's resource and purity aren't in the save: they're part of the game's map (Q18). Without them the World can't say what a provider delivers, and the flow analysis needs that.

A spike ([log 06](../guide/log/06-spike-game-map.md)) showed the data can be read from the game install:
- All 459 resource nodes are in one package, `Map/GameLevel01/Persistent_Level.umap`. Each has its resource class, purity and position, under the same name the save uses.
- Reading it takes [CUE4Parse](https://github.com/FabianFG/CUE4Parse) (C#, Apache-2.0) and the type map the game ships (`CommunityResources/FactoryGame.usmap`).
- CUE4Parse 1.2.2.202611 can't load that type map as shipped. The game writes optional properties without an inner type. A patched copy works.
- A missing purity means normal (Unreal stores only non-default values). This is deduced, and the wiki's node counts confirm it.
- CUE4Parse pulls in `Microsoft.Bcl.Memory` 9.0.0, which has a known high-severity vulnerability.

## Decision
We will extract resource nodes with a **separate tool** that writes a **node table**, and the Catalog imports that table like the Docs file.

- **The tool:** `tools/SatisfactoryLedger.MapExtractor`, a console app. It reads a game install (read-only): the type map, patched in memory, and `Persistent_Level.umap`. It writes `resource-nodes.json`. You run it once per game update, as with exporting the Docs file. CUE4Parse and the type-map workaround live **only** in this tool. It's in the repository and in the solution (a `/tools/` folder), so `dotnet build` and CI compile and test it and it can't quietly break. But no project of the app references it, the Aspire AppHost doesn't start it, and it's never deployed. An architecture test checks the references.
- **The node table:** one entry per node, with its name as the game and the save know it (`BP_ResourceNode35`), the resource's class name (`Desc_OreIron_C`), the purity (impure, normal or pure, with "not stored" written out as normal), and the position. It carries a game version label, like a Docs import (ADR-0005).
- **The Catalog** gets a new kind of entry, the **resource node**, imported from the node table through its own endpoint and reconciled like the other entries (ADR-0009): added, updated, or no longer in the game. Translating the table is the anti-corruption layer. The game's spelling `RP_Inpure` never gets past the tool.
- **The World** resolves a provider's node by name through `Catalog.Contracts` at save import. A node it can't find is reported, not guessed.
- **Purity** becomes a business rule (BR-W9).

Covered first: the nodes miners and oil extractors stand on (`BP_ResourceNode_C`). Resource wells (satellite nodes), geysers and water volumes follow when they're needed.

*Implemented in [log 10](../guide/log/10-map-tool.md) (the tool) and [log 11](../guide/log/11-catalog-resource-nodes.md) (the import).*

*Amended 2026-11-05 ([log 10](../guide/log/10-map-tool.md)):* resource well satellites (`BP_FrackingSatellite_C`) are in from the start, with the same fields and the same purity default. Your world has 34 resource well extractors, and the flow analysis needs them. With them, every extractor in your world that stands on a node resolves: 87 miners, 7 oil extractors, 34 resource well extractors. Water extractors stand on water volumes, which have no purity, and geysers only matter for power.

## Alternatives considered
- **The app reads the game install itself:** always current, but every running instance needs the game's path, a 20 GB install, CUE4Parse and the workaround. It also puts a library with a vulnerable dependency into the API's dependency tree, and makes tests depend on a game install.
- **Community node data:** no game-file reading at all, but it means someone else's format, licence and update schedule. The game ships the files and the type map for exactly this kind of use.
- **The player enters resource and purity per miner:** no tooling, but your world has 254 extractors.

## Consequences
- **Game updates need one extra manual step:** run the tool, then import the table. The import reports which nodes are new, changed or gone, and a save that points at an unknown node shows it.
- **The tool is tested against its translation logic** (type-map patch, purity default, table format) with small inputs. Running it against the real game stays a manual check, because CI has no game install.
- **CUE4Parse's dependency must be safe to build:** with warnings as errors, NuGet's vulnerability warning fails the build. `Microsoft.Bcl.Memory` gets pinned to a fixed version in `Directory.Packages.props` (transitive pinning is already on).
- **The type-map workaround is our own code.** If CUE4Parse learns to read the game's type map, or the game's generator changes, the workaround can go. The tool should fail clearly when it can't load the type map, not produce a partial table.
- **The version label is still typed in by hand,** as with the Docs file. Checking it against the build number in a save is possible later.
