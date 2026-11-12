# Step 41 · Open a factory from the map

**Goal:** on the map, click a factory's building to go straight to that factory's page (asked 2026-11-11). Branch: `main`, uncommitted.

## What was built

- **A click on a factory's building opens its page**: a machine, or a generator chosen as one of its machines (BR-W2). The building is found in `machineOwners`, as for its edge colour, and the click navigates to `/snapshots/:id/factories/:factoryId`.
- **Its tooltip says so**: *Factory **Name** · click to open*. Buildings in no factory have neither.
- **Not while drawing an area for 3D**: then a click keeps drawing, as before.
- **The factory's name is escaped** in the tooltip. Leaflet takes tooltips as HTML, and a name is the player's own text: a `<` or `&` in it would otherwise be read as markup.

Web only; no API change.

## Files

| Where | What |
|---|---|
| `web/src/app/world/map/map-page.ts` | `factoryLine`, `opensFactory`; used for machines and generators |
| `web/src/app/world/map/map-model.ts` | `escapeHtml` |

## Tests

`map-model.spec.ts`: a name shows in a tooltip as written, not as markup (written first; it failed to compile without `escapeHtml`). The click itself has no test: the map page has no component test (Leaflet needs a real layout), as before. **Not tried in a browser yet.**

**Totals:** 895 .NET tests, 79 web tests.

## Points worth noticing

| | Java / TypeScript | Here |
|---|---|---|
| Stopping a click reaching the map | `event.stopPropagation()` | `L.DomEvent.stopPropagation(event)`: Leaflet's own events go through its event system, not only the DOM's, so the map's handlers would still see a plain DOM stop |

## Choices worth a second look

1. **One click navigates**, without a popup in between: the tooltip says what a click does.
2. **Generators of a factory aren't edged in its colour** on the map, unlike its machines; they do open the factory. Easy to add if you want them edged too.

## Next

Part 2b(2): a live preview of the traced factory while choosing in 3D.
