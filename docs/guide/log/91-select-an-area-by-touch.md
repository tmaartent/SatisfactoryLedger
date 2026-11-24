# Step 91 · Selecting an area by touch

**Goal:** pick an area on a touch screen, where no key can be held (ADR-0033, asked 2026-11-24). On the world map, shift-drag opens an area in 3D and ctrl-drag arranges it; on the optimal arrangement, shift-drag draws the area and ctrl-drag keeps a part out. A phone or a tablet has neither key. Branch `feat/select-mode`, on top of step 90 (`feat/touch-screens`, PR #26).

## What was built

- **A select button in the map's top right corner**, on the world map and on the optimal arrangement, only on a touch screen (`@media (hover: none)`). It turns select mode on: the map stops panning, and a one-finger drag draws a yellow rectangle. Tapped again (it shows a cross), select mode ends.
- **On letting go, the page offers what to do with the rectangle:**
  - the world map: **3D**, **Arrange**, **Cancel**; a choice opens the page and ends select mode;
  - the optimal arrangement: **Area**, and once there is an area **Keep out**, then **Cancel**; select mode goes on after a choice, so the next part to keep out can be drawn at once.
- **A tap without a drag** offers nothing: a rectangle as small as a key-held drag ignores (2 m on the map, 10 m on the arrangement) is a tap.
- **On a phone** the choices stand at the bottom of the map, in words only: at the top they ran into the zoom buttons and the select button.
- **On a touch screen, the arrangement says how by touch:** its introduction speaks of the select button and *Keep out* instead of shift and ctrl, and the shift-and-ctrl hint under the settings is hidden, as on the map.

## How

- `world/map/area-selection.ts`: `AreaSelection`, the world map's and the arrangement's. Two signals, `selecting` and `drawn` (the two corners let go of, as Leaflet coordinates), and `attach(map)`, `toggle()`, `clear()`. Each page says, by a function it passes, which drag is an area and not a tap, and turns the corners into its own kind of area (`toArea` on the map, `areaBetween` on the arrangement).
- **Pointer events, not Leaflet's mouse events.** Drawing with a key held listens to Leaflet's `mousedown`, `mousemove` and `mouseup`. A finger moving fires none of those: a browser makes a mouse event of a tap, not of a drag. `AreaSelection` listens to `pointerdown`, `pointermove`, `pointerup` and `pointercancel` on the map's container, and turns a pointer's place into a map coordinate with `map.mouseEventToLatLng`, which takes any event with `clientX` and `clientY`. Only the primary pointer draws (`isPrimary`): a second finger, as in a pinch, doesn't.
- **`touch-action: none`** on the map while drawing or selecting (`.map.drawing`): with Leaflet's dragging off, the browser would otherwise take a finger's drag to scroll the page.
- `styles.scss`: `.area-select` and `.area-choices`, in the global styles. In each page's own styles they were copied into both pages, and the map's stylesheet went over Angular's budget of 4 kB per component. Two rules there needed more weight than a class: Material's styles come after the global ones, so `button.area-select` outranks Material's `position: relative` for a mini FAB, and `button .mat-icon` its rule for a button's icon.

### Technical choices to overrule

- **The button only on a touch screen.** With a mouse, shift and ctrl stay the way (asked 2026-11-23: no button). It could show everywhere.
- **The arrangement page stays in select mode after a choice**, the world map doesn't: there a choice leaves the page.
- **Icons**: `highlight_alt` for the button, `crop_free` for *Area*, `block` for *Keep out*, `view_in_ar` and `grid_view` for *3D* and *Arrange*.
- Defining a factory in 3D (shift-drag adds a box of machines, alt-drag takes them out) has no touch way yet; a tap on a machine adds or takes out one at a time.

## Tests

- `world/map/map-page.spec.ts`, *on a touch screen, with no keys to hold*: the button turns selecting on; a drag offers 3D and Arrange; each opens its page with the area; Cancel lets go of the area and of selecting; a tap offers nothing.
- `world/arrangement/arrangement-page.spec.ts`, the same heading: the select button and a drag draw the area to arrange; Keep out is offered only once there is an area, and keeps a part out; selecting goes on after a choice.
- 371 web tests pass (exit code 0); lint, the format check and the build pass, with no budget warning.
- **Looked at in headless Chrome** at 360, 390 and 412 pixels wide with touch emulated, by real touch events over the DevTools protocol (`Input.dispatchTouchEvent`), not synthetic ones: the drag draws the rectangle and the map doesn't move; the choices fit between the legend's button and the map's right edge; the arrangement's introduction speaks of the button.
