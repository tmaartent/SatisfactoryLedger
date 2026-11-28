# Step 132 · One belt floor view, drawn flat on the map and in 3D

Step 2 of card C of the architecture review of 2026-11-28 (`docs/architecture/reviews/2026-11-28.md`), spec #80, PR #86. Three refactors that changed no behaviour, then three fixes the owner decided (2026-11-28) where the two views, or the two pages, didn't agree.

## Design

- **The belt floor view.** `world/arrangement/belt-floor.ts` owns what the belt floor of a run shows. Its interface is one function, `beltFloor({ arrangement, colourOf, names, focused, levels })`, which returns the marks to draw, each with where it is, its colour, whether it stands out, fades or hides, and its tooltip:
  - `factories`: each factory on the plot proposed;
  - `belts`: each routed belt or pipe, with its `spot` (a miner, extractor, station or machines where it starts or ends) and, for the map, its `lifts` (with the spot's mark at a spot, and the amber ring where it leaves the floor);
  - `ways`: the existing belts outside the area that routed belts join, with the building they start at;
  - `joints`: the supply lines' splitters and priority mergers, with the arrows of their ways in and out;
  - `crossings`: the last-resort crossings.
- It is built from the arrangement model's pure functions (`spotMark`, `wayMark`, `joinedWays`, `lineJoints`, `crossingMarks`, `suppliedBelts`, `relatedFactories`, `shownFactories`, `levelShown`, `beltTooltip`, `factoryTooltip`). The model's interface is unchanged but for `NodeNames`, now exported for the view's input.
- **The two adapters.** `ArrangementPage.drawRun` and `drawBelts` turn the marks into Leaflet shapes; `BeltFloor3d.draw` turns them into three.js pieces. What a mark looks like (a circle or a drum, a square or a cube, how thick, how see-through) stays in each adapter; which marks there are, their colour, tooltip and fading are the view's.
- **Levels in 3D.** `levels` is the 3D view's input: the levels picked in the Levels tab. A mark on no level picked is `hidden`; one on another level than those picked is `faded`. A belt is drawn in 3D one piece per level, so its mark has `fadedOn(level)`. The map leaves `levels` out, so nothing hides there.
- **Fading in 3D without drawing again.** Each mark has a `key` in the view, its own whatever is picked. Each three.js piece keeps its mark's key in its `userData` (and, for a belt, the level of its piece). When the factories or levels picked change, `show()` asks the view again, for the run the pieces were drawn from, and fades or hides each piece as its mark says. Drawing again would frame the camera anew and drop a splitter's arrows. (Until the review fixes, a piece found its mark by its place in the view's lists.)
- **The settings and the run text** left `arrangement-model.ts` for `run-settings.ts`: the foundations and foundation heights the player sets sizes in, the solver's time, and `runStatus`, `runSummary` and `saving`. Not into the view: they're about how a run is asked for and listed, not what the floor shows, so the view would have held two jobs.
- **Deletion test:**
  - Delete `beltFloor`, and both views stitch the model's functions together again, each its own way: the colours, the fading by the factories picked, the tooltips and which spot is marked where, written twice and free to drift. The view is deep: one call, five lists of marks, from fifteen of the model's functions and constants.
  - Delete `run-settings.ts`, and its functions go back into the model or into the run picker and the page. It is shallow, a home for nine small functions, and earns its place by keeping the arrangement model about the arrangement.
- **A real seam:** two adapters, the Leaflet one and the three.js one, so something varies across it.

## How it was built

1. **The settings and the run text moved first**, with their tests, into `run-settings.spec.ts`: a `refactor(web):` commit on its own. The arrangement model's spec lost the same tests (111 tests in the two files, as before).
2. **The view, red first.** `belt-floor.spec.ts` is the agreed seam: it hands the view a run of a miner feeding smelters that feed a foundry, and asserts the marks that come out. The first test, *are each in the colour of the factory they serve, saying what they carry and on which level*, didn't compile until `beltFloor` existed.
3. Then one slice at a time, each red before it went green: the belts that stand out and fade with a factory picked; the colour of a belt between two factories (then one per view; see *Decided by the owner*); the levels in 3D; the spots (*marks a miner red at the foot of its lift*); the existing belts joined; the joints and the crossings; the lifts on the map and their amber ring; the factories on their plots. 22 tests.
4. **The map moved onto it** (`drawRun`, `drawBelts`): the second commit. Guards: `arrangement-page.spec.ts` and the e2e pages.
5. **The 3D view moved onto it**: the third commit. It has no spec of its own, as agreed: the adapters are thin and untested. Guards: the e2e pages on recorded answers.

**Test runs:** web 521 passed (44 files, 22 new in `belt-floor.spec.ts`); lint, typecheck and build clean; e2e on recorded answers 261 passed, 48 skipped.

## Angular / TypeScript points

| Here | In Java / Spring |
|---|---|
| **`interface FactoryMark extends Shown, Keyed`**: an interface takes in the fields of two others, so `faded`, `hidden` and `key` are declared once (primer: *`extends` on interfaces, `Omit`, `keyof`, `ReturnType<typeof f>`*). | `interface FactoryMark extends Shown, Keyed`, the same in Java, though a Java interface would have getters. |
| **`Omit<SpotMark, keyof Shown \| keyof Keyed>`** for a way's spot, and `Omit<CrossingMark, 'level'> & Shown & Keyed`: a type made from another, fields left out or added, without writing it out again. | A new class or record with the fields copied; Java has no type operators. |
| **`ReturnType<typeof jointArrows>`**: the type of what a function returns, without naming it. | Naming the return type as a class first. |
| **A key in three.js `userData`** (`userData['mark'] = { key, level }`): each piece remembers which mark it draws, and `show()` finds it again in a new view by that key (primer: *A mark's key kept in three.js `userData`*). `BeltMark.fadedOn(level)` is a closure over the mark, called later. | A `Map<String, Object>` on each node holding an id; a lambda stored in a field for `fadedOn`. |
| **`'fadedOn' in found`**: the `in` operator narrows a union to the members that have that field, here a belt's mark. | `instanceof BeltMark`; TypeScript narrows on the shape instead of the class. |
| **A spec without Leaflet or WebGL:** the view returns plain objects, so the test builds an `ArrangementResponse` and compares marks with `toEqual`. | A plain unit test of a service that returns DTOs, no Spring context. |

## Decided by the owner (2026-11-28)

The refactors kept each view as it was, and three differences came to light. The owner decided each; each is a `fix(web):` commit of its own, with a failing spec first, except fix 2 (below). They come **after** the refactors, where `CODING_STANDARDS.md` asks for a behaviour change to come before the refactor it belongs to: the differences only surfaced while refactoring, when the two views' wiring was put side by side in one module, and were decided after the refactors were done.

1. **A belt between two factories takes the colour of the factory it leaves, on the map and in 3D.** 3D gave it the colour of the factory it goes to. The mark lost `colourIn3d` and carries one `colour` (`beltEnds`). Red first: *between two factories, are in the colour of the one they leave, one colour for the map and 3D* compares the whole mark, which still had `colourIn3d`.
2. **The supply lines' joints and the last-resort crossings fade on the map too**, with their belt, as in 3D. The view already said `faded` for them; only the Leaflet adapter changed, which has no spec, so this fix had no red test at the view: its two tests were renamed to say *on the map as in 3D*. The e2e pages guard the adapter.
3. **Still in view beside the sidebar is what neither its bar nor its open tab covers, closed or open, on both pages.** One function, `coveredBySidebar(page)` in `world/map/visible-part.ts`, reads the sidebar's own box (the rail, or on a phone the bar of tabs, the snapshot picker and the sheet) and the open tab's (which on a tablet lies over the page outside that box), and keeps the most each side loses. The area page (`seenRange`) and the world map (`fitVisible`) call it. Before, the area page read only the sidebar's box, so on a tablet it missed the tab lying over the stage; the world map read only the open tab, so on a phone it missed the bar of tabs. Red first: its spec in `visible-part.spec.ts` didn't compile until the function existed; five tests, the jsdom boxes stubbed with `vi.spyOn(element, 'getBoundingClientRect')`.
   - **Its home:** beside `coveredSides`, which it uses, rather than in `sidebar/`, which would then import from a World page's module. The deletion test: delete it, and each page reads the sidebar from the DOM again, its own way, as before.

**Test runs after the fixes:** web 526 passed (44 files, 5 new in `visible-part.spec.ts`); lint, typecheck and build clean; e2e on recorded answers 261 passed, 48 skipped.

No rule in `docs/domain/business-rules.md` says what colour a belt is, what fades or what the sidebar covers (BR-O2 says only that the last-resort crossings are marked on the map and in 3D), so none changed.

## Review fixes

From `/code-review` of PR #86: the code in one `refactor(web):` commit, behaviour unchanged, and one `fix(web):` before it.

- **`fix(web):` the sidebar's minimum together.** `coveredSides` counts nothing when one box leaves less than 120 px in view; `coveredBySidebar` now does the same for what the bar and the open tab cover together (`tooLittleLeft`, shared by both). Red first: *is nothing when the bar and the tab together leave too little to show anything in*, a bar over the left and a tab over the right that leave 100 of 300 px between them.
- **The 3D pieces don't depend on which effect runs first.** A piece found its mark by its place in the view's lists (`shown.belts[i].spot!`), safe only because the draw effect ran before the show effect. Now each mark has a `key`, the piece keeps it, and `show()` asks the view for the run the pieces were drawn from (`drawn`), not the one the inputs hold now. No index, no `!`. The show effect no longer reads the arrangement. Spec: *keep their names in the view whatever is picked*.
- **What a belt says it carries is the view's:** `BeltMark.label`, the item and the point halfway along, from `beltsToLabel`; the map drew it from `beltsToLabel` itself. Spec: *say what they carry halfway along, with a factory picked, when they stand out with it and are 30 m or more*.
- **One type for `faded` and `hidden`**, `Shown`, which the five marks extend, `onLevels` returns and the 3D view reads; `WayMark.spot` is `Omit<SpotMark, keyof Shown | keyof Keyed>`; one constant, `noFactoryColour`, for `#e0e0e0`.
- **Names:** `standingOut` for the belts that stand out (was `shown`), `onPicked` for a level's showing; in the 3D view `marks` for the view's marks and `floor` for the floor's mesh (they were `floor` and `ground`).
- **One way to say every level:** `levels` is required, null for every level; the map passes `levels: null`.
- **`runSummary`'s comment** wrapped at 120 characters.

**Test runs after the review fixes:** web 529 passed (44 files); lint, typecheck and build clean; e2e on recorded answers 261 passed, 48 skipped.

## Choices to overrule

- **The factories on their plots are marks of the view too**, beside the belt floor's own marks: both views drew them with the same colour, tooltip and fading. The machines on them stay in the adapters, as geometry.
- **The marks' keys are made of their place in the run** (`belt 3`, `joint 3 merger`, `way 0`), not of ids the API gives: a routed belt has none. They're stable for one run, which is all `show()` asks of them.
- **`lifts` is the map's only:** in 3D the lifts are part of the belt's stretches (`beltStretches3d`).
- **`run-settings.ts` holds both the settings and the run text**, one module: `runSummary` says a run by its settings, in foundations.
- **`arrows` are worked out for every joint**, though only a click in 3D shows them.

**Files:** `web/src/app/world/arrangement/belt-floor.ts`, `belt-floor.spec.ts`, `run-settings.ts`, `run-settings.spec.ts` (new); `arrangement-model.ts`, `arrangement-model.spec.ts`, `arrangement-page.ts`, `belt-floor-3d.ts`, `run-picker.ts`; `web/src/app/world/map/map-page.ts`, `visible-part.ts`, `visible-part.spec.ts`; `web/src/app/world/area/area-page.ts`; `docs/guide/primer.md`; `docs/architecture/reviews/2026-11-28.md`; this log.
