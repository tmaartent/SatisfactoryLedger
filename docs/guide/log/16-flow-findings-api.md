# Step 16 · Flow analysis, part 3: the findings over the API

**Goal:** what the analysis finds, per world snapshot, over the API: machines below 100% and why (BR-A11), bottlenecks (A12), unused supply (A13), and sources of unknown rate with how long their stock lasts (A9). Each with what a player needs to find it in the game.
**Result:** `GET /api/v1/world/snapshots/{id}/analysis`. Tests: 523, all green. The OpenAPI document (`api/openapi.json`) has the new endpoint and its schemas.
**Decisions you made:** none new. "Go ahead" on the shape I proposed.
**Decisions I made, for you to overrule:** the API's shape. See "Choices worth a second look".

## What was built

```
World/Analysis
  SnapshotAnalysis.For(snapshot)   runs SteadyState on the snapshot, turns FlowResult into findings
  AnalysisResponse                 settled · summary · approximateParts · machines · bottlenecks · unusedSupply · unknownSupply
  AnalysisEndpoints                GET /api/v1/world/snapshots/{id}/analysis → 200 or 404
```

## What the endpoint gives

```json
{
  "settled": true,
  "summary": { "machines": 5, "running": 3, "starved": 1, "outputBlocked": 1, "noRecipe": 0 },
  "approximateParts": [],
  "machines": [
    { "id": "Persistent_Level:PersistentLevel.Build_SmelterMk1_C_2147301226", "className": "Build_SmelterMk1_C",
      "recipe": "Recipe_IngotIron_C", "utilisation": 0.4167, "limit": "outputBlocked",
      "limitingItem": "Desc_IronIngot_C", "position": { "x": …, "y": …, "z": … }, "approximate": false }
  ],
  "bottlenecks": [ { "id", "className", "capacity", "flow", "offeredMore", "position", "approximate" } ],
  "unusedSupply": [ { "id", "className", "item", "offered", "delivered", "position", "approximate" } ],
  "unknownSupply": [ { "id", "className", "delivered": { "Desc_…": 60 }, "stockMinutes", "confirmed", "position", "approximate" } ]
}
```

- **Machines:** only those below 100%, the lowest utilisation first.
- **Bottlenecks:** the most offered on top of the capacity first.
- **Unused supply:** the most that could be delivered on top first.
- **Sources of unknown rate:** the ones only *assumed* supplied first, the shortest stock first. Those need checking in the game.
- **Rates and numbers:** per minute, rounded to 4 decimals (BR-A10).
- **`approximate`:** true when the finding's part of the world didn't settle. `approximateParts` lists those parts, with one buildable each to find them by.

## Tests (one behaviour each)

| Where | Tests |
|---|---|
| `SnapshotAnalysisTests` (the test world) | The summary counts the machines by how they run · nothing is approximate · a machine below 100% comes with its cause, recipe and position · a machine running fully isn't a finding · the worst comes first · unused supply says what a provider could deliver more · a source of unknown rate says what it supplies and whether its stock lasts |
| `WorldAnalysisApiTests` (real PostgreSQL) | The summary over the API, as JSON · a machine finding with `"outputBlocked"` · unused supply · an unknown snapshot is 404 |

The expected summary (3 running, 1 starved, 1 blocked) was worked out from the test world before the code existed:
- smelter B, constructor C and the packager run fully;
- smelter A is blocked;
- constructor D has no input belt.

## .NET points worth noticing (all now in the primer)

| .NET | Java/Spring (TypeScript) | Note |
|---|---|---|
| `Math.Round(x, 4, MidpointRounding.AwayFromZero)` | `Math.round` is half up | .NET rounds a midpoint to even by default. |
| `ReadFromJsonAsync<JsonNode>()` in tests | Jackson's `JsonNode` | Test the JSON shape itself: camelCase enums, field names. |
| `Results<Ok<AnalysisResponse>, NotFound>` | `ResponseEntity<…>` with `@ApiResponse` | The union tells the OpenAPI document both outcomes (as in step 02). |

## Choices worth a second look

1. **Computed on every request, no cache.** Your big world takes about 9 s. A snapshot never changes, so the result could be stored next to it. That's worth doing once the web app shows it.
2. **The whole world, not per factory.** A filter `?factory=<id>`, to see only a factory's findings, is a natural next step: tracing factories exists already.
3. **Ids and items as the save and Catalog write them** (`Persistent_Level:…`, `Desc_IronIngot_C`), no display names. The web app can look up names in the Catalog, as it does elsewhere.
4. **Positions in game units** (centimetres), as the snapshot stores them. The game's own map shows metres.
5. **Only machines below 100%** are listed; the summary counts all of them.

## Next

- Your call. Options:
  - **the web app:** a page per snapshot with these findings;
  - **findings per factory** (`?factory=`);
  - **storing the analysis** with the snapshot;
  - **comparing snapshots:** what's new since the last save. That's the "track the world over time" goal of ADR-0012.
