# Step 74 · Icons in the Info and Explanation panels

**Goal:** the 3D view's Info panel (on a factory's page too) named items in plain sentences: *Can't all leave: Alumina Solution (6%)*, *Holds Bauxite × 100, Water × 50*. Now each item there has its icon, and the machine's own icon in the heading is bigger. The explanation panel got the same: the machines' icon beside the question, and an icon for each item and building where it starts (asked 2026-11-23). Branch `feat/game-icons`, uncommitted, after step 73.

## How it's built

- **Items, not finished text.** The panel's sentences came from `limitText` and `describeContents`, which return strings: there's nowhere to put an icon in a string. Each now has a sibling that returns the items, and the old function is built on it:
  - `limitParts(finding)` (`limit-text.ts`): `{ before, items, after }`, the words before the items, the items with their share (`'6%'`, or null), and the words after (*", lasts 42 min"*, *" can't leave"*). `limitText` joins them with the names, as it did.
  - `contentEntries(contents)` (`map-model.ts`): `{ item, count }` per item, most first, counts rounded. `describeContents` joins them.
  - So the findings and the map's tooltips, which still use the text, say exactly what they said: their tests are unchanged and pass.
- **The panel** (`area-page.html`) draws the reason as its words around a `@for` over the items, each a 16 px `<app-game-icon>` and its name. What a machine holds, waits to leave, and what a belt carries share one `ng-template` (`#held`), used four times with `*ngTemplateOutlet`. An item and its icon don't break over two lines (`.item`, `white-space: nowrap`).
- **Sizes** (asked twice: first 32 and 16 px, then *a bit bigger*): the heading's icon (the machine, generator, belt or lift looked at) is 48 px, the items' 20 px. The heading is a flex row, so the icon sits in the middle of the text.
- **The explanation panel** (`explain-panel.html`): the machines asked about get their icon beside the question (48 px, from the finding's class name), each start's item 24 px (was 18), and each building it starts at, holds back or is shared with 24 px.
  - A trace names buildings as the game does (*Smelter*), not by class: `CatalogNames.iconOfBuildingNamed(name)` finds the class whose name it is, as `iconOfItemNamed` does for items, and `<app-game-icon [buildingNamed]>` takes it.
  - A place in a start (`Place`, `trace-starts.ts`) now keeps its building on its own (`building`) next to its name with the recipe (*Smelter (Iron Ingot)*), for the icon.

Written into ADR-0032.

### Technical choices to overrule

- **The recipe line has no icon:** a recipe has none of its own in the game (it could take its main product's).
- **The findings table and the map's tooltips keep their sentences without icons.** The findings' *Why* column could get them the same way.

## Tests

6 new: `limitParts` gives the words before and the items with their shares, and the words after; `contentEntries` lists items most first, rounded; `iconOfBuildingNamed` finds a building's icon by its name; `<app-game-icon>` shows a building named so; the explanation panel shows the machines' icon and the start's buildings'. The places in `trace-starts.spec.ts` now carry their building. `AreaPage` has no spec (WebGL, step 63). 294 web tests pass, exit code 0; the build is clean. Not tried in a browser.
