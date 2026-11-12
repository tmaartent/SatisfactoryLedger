# Step 44 · A factory's generators edged on the map

**Goal:** on the map, a factory's generators edged in its colour, as its machines are (step 37). The last of the loose ends you chose (2026-11-12). Branch: `main`, uncommitted, after step 43.

## What changed

- **One style for machines and a factory's generators**: `buildingStyle(fill, factoryColour, inHighlighted)` in `map-model.ts`. Filled by how it runs; edged in its factory's colour (2), boldly when its factory is the highlighted one (3.5); dimmed when another factory is highlighted. A building of no factory keeps its fill as its edge.
- **Machines** use it as before (the same values, moved out of `drawMachines`).
- **Generators**: a generator burning a known fuel, or one that belongs to a factory, now uses it too. Before, a generator had a thin edge in its own status colour and never showed its factory or the highlight. A factory's generator whose fuel isn't known is filled in the consumers' purple.
- Other consumers, splitters, containers and the like are drawn as before.

Web only; no API change. A click on a factory's generator already opened the factory (step 41).

## Files

| Where | What |
|---|---|
| `web/src/app/world/map/map-model.ts` | `buildingStyle` |
| `web/src/app/world/map/map-page.ts` | Machines and generators drawn with it; `consumerColour` named |

## Tests

`map-model.spec.ts`, written first (they failed to compile without `buildingStyle`): a building in its factory is edged in its factory's colour over the fill of how it runs · edged boldly when its factory is the one highlighted · dimmed when another factory is highlighted · of no factory, edged in its own fill. **Not tried in a browser yet.**

**Totals:** 884 .NET tests, 83 web tests.

## Points worth noticing

| | Java / TypeScript | Here |
|---|---|---|
| A tri-state parameter | `Optional<Boolean>` | `inHighlighted: boolean \| null`: `null` says no factory is highlighted, `false` that another one is; the tests name all three |

## Next

You choose: the live preview while choosing in 3D (2b(2)), the flow model's accuracy, or something new.
