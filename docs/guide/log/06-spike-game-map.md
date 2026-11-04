# Step 06 · Spike: resource nodes from the game's map

**Goal:** Q18. The save links a miner to its node (`BP_ResourceNode35`), but not to the node's resource or purity. Can we read those from the game's own files?
**Result:** yes. The game's main map package holds all 459 resource nodes, with their resource and purity. `BP_ResourceNode35` is **iron ore, normal**. It takes one workaround, explained below. How to build this into the product is ADR material (see the end).
**Code:** a throwaway console app outside the repo, reading your game install read-only.

## What's in the game install

| File | What it is |
|---|---|
| `FactoryGame/Content/Paks/FactoryGame-Windows.utoc/.ucas` | The game's packed files (Unreal's IoStore format). 40,876 files, not encrypted. |
| `CommunityResources/FactoryGame.usmap` | The *type map*. Cooked UE5 games store properties without names or types ("unversioned"), so a reader needs this file to know the layout of every class. Coffee Stain ships it for modders. |
| `CommunityResources/Headers.zip` | The game's C++ headers, also for modders. |

The reader is [CUE4Parse](https://github.com/FabianFG/CUE4Parse) **1.2.2.202611** from NuGet: C#, Apache-2.0, actively maintained. FModel uses it too.

## What it took

1. **The shipped type map doesn't load in CUE4Parse.** It fails with `ArgumentOutOfRangeException` in `UsmapProperties.ParsePropertyInfo`. Parsing the file by hand showed why: the game's type map writes *optional* properties (type 28) without an inner type, and CUE4Parse expects one. The 44 optional properties are all engine test or editor classes, none of them resource nodes. For the spike, a patched copy writes them as plain strings, and then CUE4Parse loads the whole map.
2. **All resource nodes are in one package.** That's `Map/GameLevel01/Persistent_Level.umap`, not in the 4,520 generated world-partition cells. Scanning everything took 2½ minutes; loading just that package is enough.
3. **Each node (`BP_ResourceNode_C`) has** `mResourceClass` (e.g. `Desc_OreIron_C`), `mPurity`, `mResourcesLeft` and its position. Node names match the save: `BP_ResourceNode35` in the map is the node the save's miner points at.
4. **A missing `mPurity` means normal.** Unreal only stores values that differ from the class default object, like the save does. 119 nodes store `RP_Inpure` and 141 store `RP_Pure`, so impure can't be the default. The 199 without a value are normal. The headers don't show a default (no initializer, and the constructor isn't shipped), so this is deduced. It matches the wiki: iron ore has 39 impure, 42 normal and 46 pure nodes, and that's exactly what the map gives.

| Purity | Nodes |
|---|---|
| Impure (`RP_Inpure`, the game's own spelling) | 119 |
| Normal (not stored) | 199 |
| Pure (`RP_Pure`) | 141 |

## Not covered yet

- **Other kinds of nodes.** Resource wells (the pressurizer's satellite nodes), geysers and water have their own classes and weren't read. Water extractors sit on water volumes with no purity; their rate is fixed.
- **Every node name in a real save.** Your test world has one miner. In the big save, nodes with names like `BP_ResourceNode101_1893` need checking: do all 254 extractors resolve?
- **A vulnerable dependency.** CUE4Parse pulls in `Microsoft.Bcl.Memory` 9.0.0, which has a known high-severity vulnerability (NU1903). If we adopt it, Central Package Management can pin a fixed version.

## .NET and Unreal points worth noticing

| Point | Java/Spring (TypeScript) | Note |
|---|---|---|
| Delta serialization: only non-default values are stored | Jackson `@JsonInclude(NON_DEFAULT)` | Both the save and the map do this. "Missing" must be read as "the default", and the default must be known. |
| Unversioned properties plus a type map (`.usmap`) | Protobuf without the `.proto` | Without the schema the bytes can't be interpreted. |
| Reading a NuGet library's API through reflection (`GetConstructors()`, `Enum.GetNames`) | `javap`, or the IDE's "go to declaration" | To check the API of the installed version, not the main branch (CLAUDE.md). |

## The decision this leads to

Where should node extraction live?

1. **An import that reads your game install** (`Catalog` reads `Persistent_Level.umap` directly). It's always current, but the app then needs CUE4Parse, the type-map workaround and the path to your game.
2. **A small tool in the repo (`tools/`) that writes a node table** (`resource-nodes.json`) per game build. The Catalog then imports that table like the Docs file. CUE4Parse and the workaround stay in the tool, and the app and its tests only see a small JSON file.
3. **Community node data** instead of the game's files. No game-file reading at all, but it depends on someone else's data and update schedule.
