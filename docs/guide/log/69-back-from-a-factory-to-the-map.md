# Step 69 · Back from a factory to the map

**Goal:** the back arrow on a factory's page went to the findings page, which is on its way out. It now goes to the map with the factory highlighted and zoomed to (asked 2026-11-23). Branch `feat/map-info-tab`, uncommitted, with step 68.

## How it's built

- **`factory-page.html`:** the arrow links to `/snapshots/{id}/map?factory={factoryId}`, labelled *Back to the map*.
- **No change to the map:** `?factory=` already highlights a factory when the map opens (step 52), and `fitToHighlighted` (step 67) zooms to its machines.

### Technical choice to overrule

- **The sidebar stays on the tab last shown,** not Factories. Picking another factory there, or a count in Info, lets go of this one.

## Tests

| File | What |
|---|---|
| `factory-page.spec.ts` (1 new) | The arrow goes to the map with the factory, not to the findings |

267 web tests pass, exit code 0; the build is clean. Not tried in a browser.

## Angular notes

- **`[queryParams]` beside `[routerLink]`** builds `?factory=f-1`. The map reads it as an input (`withComponentInputBinding`), as it reads the route's `:id`. Spring MVC binds `@RequestParam` and `@PathVariable` the same way, by name.
