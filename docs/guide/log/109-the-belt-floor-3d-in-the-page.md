# Step 109 · The belt floor in 3D in the page, for touch screens

**Goal:** the arrangement's 3D view on touch screens, in line with what the app already has (asked 2026-11-25). Branch `feat/arrangement-3d-touch`, from `main` after PR #38.

## What was wrong on a touch screen

- **A 95 × 90 % pop-up with a 180 px panel on its right.** On a phone, the panel left the scene a strip.
- **What something is showed only on hover.** A touch screen has no hover, so a tap on a belt said nothing.
- **The hint spoke of a mouse:** "drag to turn, scroll to zoom; click".
- **On a phone, held upright**, the floor didn't fit across: the camera stood back only as far as on a square screen.

## What you decided (2026-11-25)

- **In the page, as the area's 3D view**, not over it. The pop-up goes, on every screen.
- **A tap shows the label** of what it's on, at the finger, as a tap on the arrangement's map does (step 92).

## What was built

- **A *Map / 3D* switch** above the map, where the *Belt floor in 3D* button was, while the run shown has belts. In 3D the scene takes the map's place, and the map waits below, so coming back keeps where it was. Switching to 3D closes the open tab where it would lie over the view, as before the area's 3D view.
- **A *Levels* tab** in the sidebar while the 3D view shows, with what the pop-up's panel had:
  - the *Machines* switch;
  - *Unpick factories*;
  - *All*, *None* and a box per level.

  On a phone the tab is the sheet, as every tab is. It goes when you switch back to the map.
- **A tap** shows the label of what it's on at the finger. A tap on a factory also picks it, as a click does. A tap on nothing closes the label. With a mouse, pointing shows it, as before.
- **A hint in either form**, as the area's: "Drag to look, pinch to zoom, two-finger drag to move. Tap a factory to pick it, and anything to see what it is." With a mouse: "Drag to turn, scroll to zoom, right-drag to move…"
- **Further back on a phone held upright** (`floorDistance`), by as much as the screen is narrower than high, as the area's `framingDistance`. Unchanged on a screen as wide as high or wider.
- **The site's colours behind the scene**, as the area's 3D view and the map frame have: `--scene` is the surface colour. Before, the pop-up fell back to a fixed dark grey.
- **The Info tab** says *3D* is there, and that factories may stand on their footprints (step 107).

### Choices to overrule

- **Where a tab lies over the page** (an iPhone held upright, a tablet), it also lies over the *Map / 3D* switch, as over every button in that row. Close the tab first. The layout tests found it; they now close the tab before switching back.
- **The view stays 3D when another run is picked**, if that run has belts; one without belts shows the map.
- **The *Levels* tab sits between *Factories* and *Info*.**

## After your look: lines that ended in nothing (2026-11-25)

In 3D, green, pink and yellow lines ended in nothing, while a miner's ended in its red drum. Since step 108, lines also start at machines no factory owns and at a factory outside the area, and only miners and extractors had a mark; a station or drone port had none in 3D either. The yellow Caterium line outside the area did end at the right place: the smelter of *Caterium Ingots* nearest *Quickwire*.

You chose a mark for every end, on the map and in 3D:

| Where a line ends | Mark | Label |
|---|---|---|
| Miner or extractor | drum, as before | as before |
| Station or drone port | the map's dark dot, now in 3D too | its name, e.g. "Drone Port" |
| A machine of a factory outside the area | a dot in that factory's colour | "**Caterium Ingots** · factory outside the area", then "Smelter" |
| Machines no factory owns | a grey dot | "**Undefined factory**", then "Smelter and 3 more" |

- `arrangement-model.ts`: `nodeMark` now says what every end is (`SourceKind`), never nothing; `spotMark` and `wayMark` follow. A merged spot's id ("…Smelter_2+3>factory") is looked up by its first building.
- `arrangement-page.ts`: `ownerOf`, from the traced factories (`machineOwners`); on the map a station keeps its small dark dot, now with its label.
- `belt-floor-3d.ts`: `ownerOf` as an input; `mark` draws a drum for a node and a dot for the rest.
- Glossary: *Undefined factory*.

Tests, written first and failing (5): a station or drone port marked with the dark dot and named; a machine of a factory outside the area in its colour, by its name; machines no factory owns grey, as an undefined factory, inside the area and outside it. The two "marks nothing at a station" tests went with the rule. 445 web tests pass; the arrangement's Playwright tests pass on every device (51).

## The Levels tab, and the floor seen from below (2026-11-25)

- **Switching to 3D opens the *Levels* tab on a computer; on a touch screen the tabs close**, so the scene can be seen, and a tap on *Levels* opens it. `SidebarTabs.showBeside3d('levels')`, by `(hover: none)`; before, the tab closed only where it would lie over the view.
- **The floor is half see-through** (`opacity` 0.5) and doesn't write depth, so it hides nothing behind it: turned to look from below, the scene stays in view.
- Tests: the sidebar shows the view's own tab with a mouse, opening it, and closes on a touch screen (`sidebar-tabs.spec.ts`, written first, failing on compile); Playwright: the Levels tab is open with a mouse and closed on a touch screen. 447 web tests, 55 arrangement Playwright tests pass.

## The Info tab, compacted (2026-11-25)

Five paragraphs became five short lines, each led by what it's about (*Area*, *Keep out*, *Arrange*, *Runs*, *3D*), still in a mouse and a touch form, and the note that it's a proposal only. Angular drops a text node of only whitespace between two elements, which ate the space in "**Area:** shift-drag"; the colon now stands outside the bold, so the space is real text.

## No Explanation tab here (2026-11-25)

The shell added its *Explanation* tab on every World page; the arrangement has no findings to explain. `App.explains` leaves it out on the arrangement, as `picksSnapshot` leaves out the snapshot picker; ADR-0031 is amended. Test, written first and failing: the shell has no Explanation tab on the optimal arrangement (`app.spec.ts`). The test of no snapshot picker there now allows for no sidebar at all on the test's empty page.

## How

- `world/arrangement/arrangement-page.{ts,html,scss}`:
  - `view`, `hasBelts`, `shows3d`, `showView`;
  - the levels: `allLevels`, `ticked` (a `linkedSignal`, so every level is ticked again for each run), `levels`, `showMachines`, `tick`;
  - the 3D view in the map frame, and the *Levels* tab inside `@if (shows3d())`.
- `world/arrangement/arrangement-model.ts`:
  - `beltLevels` and `shownLevels`, moved out of the pop-up, which computed them itself;
  - `floorDistance`.
- `world/arrangement/belt-floor-3d.ts`:
  - `tipAt`, used by `point` for a mouse and by a tap;
  - `pointerleave` closes the label for a mouse only, since a finger leaves the canvas as it's lifted;
  - `frame` uses `floorDistance`.
- `world/arrangement/belt-floor-3d-dialog.ts`: **deleted**.

## Angular, TypeScript and browser points

| Here | Java / Spring | Elsewhere in TypeScript |
|---|---|---|
| **A tab that comes and goes with `@if`**: the `appSidebarTab` directive adds its tab when created and removes it when destroyed, so the tab lives as long as its `@if` (primer, *Frontend › Phones and touch screens in CSS*). | A bean in a custom scope, created and destroyed with it. | React: a component that registers in `useEffect` and unregisters in its cleanup. |
| **A single-choice `mat-button-toggle-group` is a radio group**: its toggles are `role="radio"` and, with an icon beside the label, had no accessible name; `aria-label` names them (primer, same section). | — | Any ARIA radio group. |
| **A finger leaves the element as it's lifted**: a touch fires `pointerleave` right after `pointerup` (primer, same section). | — | Pointer Events, in any framework. |
| **`linkedSignal(() => this.allLevels())`**: a writable signal that resets whenever what it's linked to changes; here, every level ticked again for each run. | A field reset in a listener on the source. | — |

## Tests

- **Unit** (`arrangement-model.spec.ts`, written first, failing on compile):
  - lists every level a belt runs on, from the floor up, each once;
  - shows every level and factory while every level is ticked, and only the levels ticked otherwise;
  - stands back as before on a screen as wide as high or wider, and as much further back as a phone is narrower.
- **Playwright** (`e2e/arrangement.spec.ts`, *the optimal arrangement in 3D*, on two phones, a tablet and a desktop):
  - shows the belt floor in the map's place, in view;
  - says how to look around with the fingers or the mouse it has;
  - picks the levels in a *Levels* tab, there only while the 3D view is.

  At first the toggle couldn't be found: it was a radio with no name.
- **Not tested automatically:** the label on a tap, which needs a ray cast into the WebGL scene at a known spot.
- **Results:**
  - 442 web tests pass (exit 0);
  - lint, the format check, the build and the e2e type check pass;
  - Playwright: 257 passed, 48 skipped, none failed.
- **Not yet tried on a real phone.**
