# Step 10 · The map tool: resource nodes from the game install

**Goal:** the tool from [ADR-0015](../../adr/0015-resource-nodes-from-the-game-map.md). Read every resource node's resource and purity from the game install and write the node table the Catalog will import.
**Result:** `tools/SatisfactoryLedger.MapExtractor` reads your install in about 7 s and writes 577 nodes: 459 resource nodes and 118 resource well satellites. Every extractor in your big world that stands on a node finds it in the table: 87 miners, 7 oil extractors, 34 resource well extractors. 20 new tests, all green. Solution total: 352 green. Importing the table into the Catalog is the next step.
**Decisions you made:** option 2 of ADR-0015 (a tool writes a table), and do it now.
**Decisions I made, for you to overrule:** resource well satellites are included now (ADR-0015 amended), and the points under "Choices worth a second look".

## How to run it

```bash
dotnet run --project tools/SatisfactoryLedger.MapExtractor -- \
  --game "C:\Program Files (x86)\Steam\steamapps\common\Satisfactory" --game-version 1.2 --out resource-nodes.json
```

```
577 resource nodes written to …\resource-nodes.json
  Impure: 136
  Normal: 224
  Pure: 217
```

Exit code 0 on success. 1 when arguments are missing, and the help is printed. 2 when the game files can't be read: no install there, a type map it doesn't support, or a node without a resource. In that case no table is written.

## The node table

```json
{
  "version": 1,
  "gameVersion": "1.2",
  "nodes": [
    { "name": "BP_ResourceNode35", "kind": "resourceNode", "resource": "Desc_OreIron_C", "purity": "normal",
      "position": { "x": 256611.65625, "y": -197978.5625, "z": 3561.465087890625 } },
    …
  ]
}
```

The name is the one the game and the save use. Positions are in game units (centimetres). `BP_ResourceNode35`'s position matches the node position in your test world's save.

## What was built

| File | What it does | Tested how |
|---|---|---|
| `UsmapPatcher` | Rewrites the game's optional properties (type 28, written without an inner type) as strings, so CUE4Parse can load the type map. Refuses anything but the shipped format: version ≤ 1, uncompressed. | 6 tests on a small type map built in the test |
| `ResourceNodeTable` | From raw map nodes to table entries: the class name of the resource, purity (`RP_Inpure` → impure, not stored → normal), the kind of node. Refuses an unknown purity, a node without a resource, and duplicate names. Writes the JSON. | 14 tests |
| `GameMap` | The only class that uses CUE4Parse. Mounts the paks with the patched type map, loads `Persistent_Level.umap`, and reads `BP_ResourceNode_C` and `BP_FrackingSatellite_C` exports with their root component's location. | By hand against your install (CI has no game) |
| `Program` | Arguments, help and exit codes. | By hand |

Also:
- **`Directory.Packages.props`:** CUE4Parse 1.2.2.202611, and `Microsoft.Bcl.Memory` pinned to 9.0.14 (GHSA-73j8-2gch-69rq).
- **`SatisfactoryLedger.slnx`:** a `/tools/` folder.
- **Architecture test:** no part of the app references the tool or CUE4Parse.
- **README:** "After a game update".

## Checked by hand

- **Against your install:** 459 normal nodes with the same purity counts as the spike, 118 satellites, every node with a position.
- **Your big world's extractors against the table:** all miners, oil extractors and resource well extractors resolve. The 160 water extractors stand on water volumes (`FGWaterVolume…`), as expected.
- **Purity default for satellites:** 17 store impure and 76 store pure, so the 25 with nothing stored are normal. This is the same deduction as for normal nodes, made separately because satellites are a different class.

## .NET points worth noticing (all now in the primer)

| .NET | Java/Spring (TypeScript) | Note |
|---|---|---|
| `--` in an XML comment breaks a `.csproj` (MSB4025) | Same in a `pom.xml` | My usage line `dotnet run --project …` in a comment did that. |
| Top-level statements, `return 1;` as the exit code | `System.exit(1)` | `args` is in scope, and local functions at the end can use it. |
| Raw string literals (`"""`) | Java text blocks | The help text. |
| `BinaryPrimitives.ReadUInt32LittleEndian` | `ByteBuffer.order(LITTLE_ENDIAN)` | The type-map reader. |
| A `PackageVersion` for an indirect package | `<dependencyManagement>` | Lifts a vulnerable transitive dependency. NU1903 with warnings as errors fails the build otherwise. |

## Choices worth a second look

1. **Resource well satellites are included now,** beyond ADR-0015's first scope. They have the same fields and the same purity rule, and your 34 resource well extractors need them for the flow analysis. Geysers (power only) and water volumes (no purity) stay out.
2. **The patched type map is written to a temporary file and deleted afterwards.** This CUE4Parse version only takes a path for a type map.
3. **The table isn't committed to the repo yet.** The next step needs it as a test fixture for the Catalog import, and adds it then.
4. **The version label is typed in** (`--game-version`), like the Docs import. The tool could read the game's build number from the install. Not done yet.
