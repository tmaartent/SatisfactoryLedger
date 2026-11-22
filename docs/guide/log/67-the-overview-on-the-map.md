# Step 67 · The overview on the map

**Goal:** the findings page is becoming obsolete: the map and the factory pages cover its list. But its overview at the top is worth keeping where you look, and machines without a recipe were hard to find on the map (asked 2026-11-23). Branch `feat/map-info-tab`, on top of #16, uncommitted.

## What you decided

- **The map gets an Info tab with the overview only:** the cards and the *didn't settle* warning. *What it holds* stays on the findings page.
- **A count is clicked to show its buildings:** every status, not only *without a recipe*.
- **The findings page stays** for now.

Written into ADR-0031.

## How it's built

- **`ShownStatus`** (`world/shown-status.ts`): a count picked, `{ of: 'machines' | 'generators', limit }`.
- **`FindingsSummary`:**
  - its cards come from two lists built in code (machines, generators), drawn by one `<ng-template>`;
  - with `selectable`, a count of a status above 0 is a button (`role="button"`, keyboard too, `aria-pressed`), and emits `show` with its status, or `null` when it's the one shown;
  - the totals and *fuel unknown* aren't buttons: a generator of unknown fuel has no status on the map.
  - The findings page and the factory page use it as before, not selectable.
- **`buildingsShown(analysis, shown)`** (`map/map-model.ts`): the ids of the machines or generators of that status.
- **`MapPage`:**
  - the Info tab (`order` -1, so it's first and open) with the overview, selectable;
  - `highlighted` is the shown status's buildings or else the highlighted factory's, one at a time: picking one lets go of the other;
  - machines and generators are drawn with it, dimmed when not in it, as for a factory;
  - `fitToHighlighted` (was `fitToFactory`) zooms to them, and for a status rings each one in yellow (`shownLayer`). A factory's machines are edged in its colour already.

### Technical choices to overrule

- **Info is the map's first tab,** so the sidebar opens on it. It was Factories.
- **Yellow rings,** 12 px, as the focus ring of a finding's link but smaller. They're on top of the buildings but don't take clicks.
- **Two cards a row in the tab** (the page has as many as fit).

## Tests

| File | What |
|---|---|
| `findings-summary.spec.ts` (new, 5) | A click on *without a recipe* shows those machines; a generator's count shows generators; the count shown, clicked again, shows none; no button for a count of 0 or a total; none where nothing shows them |
| `map-model.spec.ts` (3 new) | The machines of a status; the generators of one, not its machines; none when nothing is picked |
| `map-page.spec.ts` | The sidebar opens on Info, with Factories and Snapshots after it; the count clicked is marked shown and let go when a factory is picked; the Factories tests open their tab first |

259 web tests pass, exit code 0; the build is clean (464 kB). Not tried in a browser: the dimming, rings and zoom are Leaflet's canvas, which jsdom doesn't draw.

## Angular notes

- **One `<ng-template>` for every card,** drawn with `*ngTemplateOutlet="card; context: { $implicit: count }"` and read as `let-count`. A local template, like a small component without its own class. In Java's Thymeleaf it's a fragment with a parameter.
- **`role="button"` on a card,** with `tabindex` and Enter/Space: a `mat-card` isn't a button, so a keyboard needs those to reach it. Turning the card into a `<button>` would lose Material's card look.
