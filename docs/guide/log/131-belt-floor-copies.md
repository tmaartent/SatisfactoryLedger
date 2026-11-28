# Step 131 · The belt floor's copies: one home for the spots' names, one area chooser

Step 1 of card C of the architecture review of 2026-11-28 (`docs/architecture/reviews/2026-11-28.md`), spec #80, grilled 2026-11-28. Two refactors, no behaviour changed. Step 2, the belt floor view with its Leaflet and 3D adapters, is still open.

## Design

- **Decided by the owner (2026-11-28):** the minimum size of an area stays per page, and the shared chooser takes it as a parameter: 200 cm a side on the world map, 1000 cm on the arrangement.
- **The area chooser.** `map/area-selection.ts` owns choosing an area on a map, now for a mouse as well as a touch screen.
  - New: `AreaDrag<K>`, the drag with a key held. Its interface is `new AreaDrag({ minimum, kindOf, style, drawn })`, `attach(map)` and the `drawing` signal. The page says what a press draws (`kindOf`: the keys held and what the page allows, or null and the map pans), how the rectangle looks, and what to do with the area chosen. The chooser does the rest: switching panning off and on, the rectangle, the minimum, and the corners in the game's centimetres.
  - `AreaSelection`'s interface changed. It takes the minimum instead of a `keeps` callback, and `drawn()` is the `Area` in centimetres instead of two `L.LatLng` corners, which both pages turned into an area at once. `DrawnCorners` is gone.
  - The "a side shorter than the minimum is a tap" rule is in one function, `areaDrawn`, private to the module, used by both choosers.
- **The two `areaBetween`s were the same rule.** The arrangement model's took two map points, converted them with `fromMapPoint`, then did the area model's min and max. It's deleted, along with its test. The chooser asks the area model's `areaBetween`, and its spec covers the conversion: (15, 30) to (10, 10) on the screen is x 1000 to 1500 cm, y 1000 to 3000 cm.
- **The spots' names** have one home, `spotNames(catalog, ownerOf)` in `arrangement-model.ts`, next to the `NodeNames` it returns and the `spotMark` and `wayMark` that read it. The flat view (`ArrangementPage.drawBelts`) and the 3D one (`BeltFloor3d`) call it. The model's interface widens by that one function.
- **Who asks it now:** `MapPage` and `ArrangementPage` each make an `AreaDrag` and an `AreaSelection` with their minimum, and lost `listenForArea` (the world map also lost `stopDrawing` and `toArea`). Each page's `drawing` signal is now the chooser's.
- **Deletion test:**
  - Delete `AreaDrag`, and the press, pan, rectangle and minimum logic comes back in both pages, about 40 lines each, as before, free to drift apart.
  - Delete `spotNames`, and the same five-line object comes back in both views. The function is shallow: it adapts `CatalogNames` to `NodeNames`. It earns its place by being the one place a sixth name would be added, which is what the card asked for.

## How it was built

1. **The chooser, red first.** `area-selection.spec.ts` is the agreed seam. It puts a real Leaflet map on the page at zoom 0, so a point on the screen is a metre on the map, and drags over it with DOM events, as the page specs do. The first test, *chooses the area dragged over, in the game's centimetres, whichever way it was dragged*, didn't compile until `AreaDrag` existed. Moving the pages' mouseup code into it made it green.
2. Then the minimum: *chooses nothing for a drag shorter than the minimum a side: it counts as a tap* (1000 cm, a 500 by 2000 cm drag), and *chooses an area as long as the minimum a side*. Then *pans the map again once let go*, and *draws nothing for a press without the key, and the map pans*.
3. **The touch selection:** *keeps the area drawn, in the game's centimetres, for the page to offer* and *keeps nothing for a drag shorter than the minimum a side: it counts as a tap*. Both were red until `AreaSelection` took the minimum.
4. **The pages moved onto it**, and the arrangement model's `areaBetween` was deleted. The guards were `map-page.spec.ts` and `arrangement-page.spec.ts`, unchanged: shift and ctrl drags, the touch selection, a tap without a drag, keeping a part out only once there's an area. That's the first `refactor(world):` commit.
5. **`spotNames`**, in a second `refactor(world):` commit. It has no spec of its own, since no seam was agreed for it. The arrangement page spec and the e2e pages guard it.

**Test runs:** web 498 passed (42 files, 7 of them new in `area-selection.spec.ts`, 1 deleted from `arrangement-model.spec.ts`); lint, typecheck and build clean; e2e on recorded answers 261 passed, 48 skipped.

## Angular / TypeScript points

| Here | In Java / Spring |
|---|---|
| **A generic class over string literals** (`AreaDrag<'area' \| 'exclusion'>`): the page's kinds are a union type, so `drawn: (kind, area) => this.use(kind, area)` is checked against `use(as: 'area' \| 'exclusion', …)` with nothing declared (primer: *A generic class over string literals*). | `AreaDrag<K extends Enum<K>>`, with an enum per page. |
| **An options object instead of a constructor with four parameters** (`new AreaDrag({ minimum, kindOf, style, drawn })`): the names are at the call site, and TypeScript checks the object against `AreaDragOptions<K>`. | A builder, or a record of the options; Java has no named arguments. |
| **Callbacks, not subclassing:** each page hands the chooser what differs (what a press draws, what an area chosen does) as functions. | A strategy interface, or the Template Method pattern with an abstract class; lambdas for a functional interface come closest. |
| **`Pick<CatalogNames, …>` as a parameter type**: `spotNames` asks only for the four methods it calls, so it doesn't depend on the injectable service as a whole (primer: *`Pick<CatalogNames, 'item' \| …>`*). | Extract a `CatalogNameLookup` interface and have the service implement it. |
| **Aliasing a signal** (`protected readonly drawing = this.areaDrag.drawing`): the template keeps reading `drawing()`, and it is the chooser's signal, not a copy. It must be declared after `areaDrag` (primer: *Field initialisers that use `this`*). | Exposing a collaborator's observable property from the bean; the field order rule is Java's too. |
| **A real Leaflet map in jsdom**: jsdom lays nothing out, so the container is 0 by 0 and a point on the screen maps from the map's centre. At zoom 0 on `L.CRS.Simple`, (10, 20) on the screen is x 1000 cm, y 2000 cm. No mocks needed. | Like testing a Swing component headless: the events work, nothing is painted. |

## Choices to overrule

- **The mouse drag and the touch selection live in one file**, `map/area-selection.ts`, as two classes. The issue said "beside the touch selection". The web has no one-type-per-file rule, and the private `areaDrawn` is shared by both.
- **`AreaSelection.drawn()` now holds an `Area` in centimetres.** That's a wider change to its interface than the ticket named, but both pages converted the corners at once, and keeping the corners would have meant a second copy of the minimum rule.
- **`kindOf` carries each page's own conditions:** the world map draws nothing before it has a snapshot id, and the arrangement draws nothing over a run shown, nor a part kept out before there's an area. They stay in the pages, since they're about what each page allows, not about choosing an area.
- **`spotNames` takes the catalog's names and `ownerOf`**, rather than the page passing the whole `NodeNames` object. The local is `nodeNames`, so it doesn't shadow the function.
- **The arrangement model's `areaBetween` test is deleted, not moved**: the chooser's spec asserts the same conversion through the chooser's interface (replace, don't layer).
- **Card C's Status line** names the step and branch for step 1; the PR goes in once it's opened.

**Files:** `web/src/app/world/map/area-selection.ts`, `area-selection.spec.ts` (new); `web/src/app/world/map/map-page.ts`; `web/src/app/world/arrangement/arrangement-page.ts`, `arrangement-model.ts`, `arrangement-model.spec.ts`, `belt-floor-3d.ts`; `docs/guide/primer.md`; `docs/architecture/reviews/2026-11-28.md`; this log.
