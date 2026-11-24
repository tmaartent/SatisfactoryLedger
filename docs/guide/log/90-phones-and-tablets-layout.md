# Step 90 · The world map on phones and tablets: the layout

**Goal:** make the world map usable on phones and tablets (asked 2026-11-24). This step is the layout. Step 91 adds picking an area by touch, step 92 tapping a building. The owner's decisions are in the new ADR-0033. Branch `feat/touch-screens`, from `main` after PR #25.

## What was built

- **On a phone (narrower than 600 px), the sidebar is a bar of tabs along the bottom.** The open tab is a sheet above that bar:
  - it opens at half the screen's height, with the map above it;
  - a swipe up on its handle raises it to full height, never over the toolbar;
  - a swipe down lowers it to half height, or from there closes it to the bar;
  - a tap on the handle switches between half and full height;
  - the snapshot picker stays above the bar, with a tab open or not (at first it showed only with a tab open; changed after trying it, see below).
- **The legend is folded behind a button** in the map's corner on a phone.
- **The hint about shift and ctrl is hidden on touch screens** (`(hover: none)`).
- **The toolbar** keeps its logo without the title on a phone; the page's padding is smaller and leaves room for the bar.
- **Heights in `dvh`** on every World page, so a phone's address bar doesn't cut off the bottom.
- **Tablets** keep the sidebar on the right, lying over the map below 960 px, as before.

## How

- `sidebar/sheet.ts`: `afterSwipe(height, down)`, the pure rule for where the sheet goes. A move under 40 px is a tap.
- `sidebar/sidebar.ts` and `.html`: the handle with `pointerdown` and `pointerup`; a `height` signal (`half`/`full`); a host class `closed`.
- `sidebar/sidebar.scss`, `app.scss`: the phone layout. On a phone the sidebar is `position: fixed` at the bottom, with a `max-height` of the screen less the toolbar. A full sheet is as high as the screen and shrinks (flex) to what's left, so it can't go over the toolbar.
- `world/map/map-page.*`: `legendOpen` and its button; the phone and touch media queries.

**Centred in what the tab leaves in view** (asked 2026-11-24, after trying it: Alclad Aluminum Sheets lay half under the sheet). The map centred what it zoomed to on the whole map, though the open tab lies over part of it: the lower part on a phone, the right part on a tablet.
- `world/map/visible-part.ts`: `coveredSides(map, tab)` says how much of each side of the map the tab lies over, cutting the side that leaves the most in view. Nothing when the tab stands beside the map, or when what's left is too little to centre anything in, as under a full sheet.
- `MapPage.fitVisible` pads those sides out of every zoom: to a factory picked, a status, the first overview, a finding's buildable. Leaflet's `fitBounds` takes `paddingTopLeft` and `paddingBottomRight` for this.
- When the tab folds, opens or changes height, the same thing is fitted again (`fitAgain`) in the new view, unless the player has dragged or zoomed since, or let go of the highlight. The sheet's height moved from the sidebar into `SidebarTabs.sheet` for this.
- The map finds the tab with `document.querySelector('app-sidebar .tab')`: the sidebar is the shell's, not the page's.

**After trying it on a phone** (2026-11-24):
- **The cards were too big:** the findings' cards (`findings-summary.scss`) are compact everywhere, with the figure in headline-small rather than display-small, a small label and less padding. The map's Info tab has them three to a row.
- **The factory page was 728 px wide on a 390 px screen:** its two tables, *Input* and *Output*, widened the page, so the browser laid everything out at that width, the sheet and the 3D view included. The sheet ran past the screen's right edge, and the 3D view's centre lay right of what was shown. Its tables, and two of the Catalog's, now scroll sideways in a `.table-scroll` box, as the Catalog's others already did. The 3D view's distance slider got room for its thumb, which Material centres on the track's end. Measured over the DevTools protocol, nothing on the page is wider than the screen now.

- **No snapshot picker on a factory's page:** it's about one factory of its snapshot, so picking another means nothing there. On a desktop that went unnoticed, but on a phone the picker stands atop the sheet. The shell leaves it out there, as on the optimal arrangement (`App.picksSnapshot`); ADR-0031 says so.
- **The 3D view on a phone** (asked 2026-11-24: the tab covered it, its hint spoke of a right-drag, and the factory lay off to the right):
  - **The tab starts closed** on a page with a 3D view: `SidebarTabs.closeOnPhone()`, called by `AreaPage`, closes it when `matchMedia('(max-width: 599px)')` matches, the phone's breakpoint in CSS. Clicking something in the view opens the Info tab again (`show('info')`, as before). Coming back to the map, the tab stays closed until a tab is tapped.
  - **A hint for touch:** two spans in the hint, `.mouse` and `.touch`, one hidden by `@media (hover: none)`, as the map's shift-and-ctrl hint.
  - **Framed at its buildings:** the camera looked at the middle of everything drawn, by `Box3.setFromObject`. That includes belts out of the factory, which run on in the scene until the area's clipping planes cut them off: a clipping plane hides what lies beyond it but doesn't change the geometry, so a long belt pulled the middle its way. It now looks at the middle of the buildings, and measures a floor by its diagonal, as the camera sees it from a corner.
  - **Far enough back on a screen higher than wide:** three.js sets a camera's angle of view upright (`fov`), so on a phone's view (about 0.68 as wide as high) far less fits across. `world/area/framing.ts`: `framingDistance({ across, high }, aspect)` stands back by as much as the screen is narrower for the factory's width, not for its height: a tower fits upright as it is.
  - **The real cause, found on the owner's phone:** the canvas was shown at twice the stage's size, and the stage's `overflow: hidden` cut it to its top-left quarter, so the scene's middle lay at the stage's bottom-right corner. `area-page.scss` gave the canvas `width: 100%`, but three.js makes the canvas in code, and Angular scopes a component's styles by an attribute (`_ngcontent-…`) that only elements in its template get: the rule never reached it. With `setSize(w, h, false)` three.js sizes the drawing buffer at `w × pixelRatio` and leaves the CSS alone, so an unstyled canvas shows at its buffer's size: right at 1×, as on a desktop and in headless Chrome, but twice too big at a phone's 2×. Both 3D views (`AreaPage.start`, `BeltFloor3d.start`) now set the canvas's size inline; the rule that never applied is gone. Found by emulating a pixel ratio of 2.625 over the DevTools protocol: the canvas measured 792 × 1170 CSS pixels in a 396 × 585 stage.
- **The snapshot picker always shows** (asked 2026-11-24): with the tab closed it was hidden, and so was which snapshot the map shows. `sidebar.scss` no longer hides `.above` on `:host(.closed)`; the page's bottom padding (`app.scss`, 116 px) and the map's height (`calc(100dvh - 232px)`) leave room for the picker and the bar. Measured at 412 × 900: the map ends at 784, the picker begins at 794.

**To open it on a phone** (asked 2026-11-24): the AppHost starts the dev server with `--host 0.0.0.0`, so it listens on every network the PC is on, at `http://<the PC's address>:4200`. Only the dev server needs to: it passes `/api` on to the API itself (`proxy.conf.js`), so the API, the database and the rest stay on localhost.
- `WithRunScript("start", ["--", "--host", "0.0.0.0"])`. The `--` is npm's. Aspire 13.6.5 runs `npm run start <args>` without it, and npm then takes `--host` as its own setting: only `0.0.0.0` reached `ng serve`, which stopped at once. A probe script that wrote its arguments to a file showed it.
- In C# the parameter is a `string[]`, not `params`: unlike Java's varargs, loose arguments don't compile (CS1501), so they're a collection expression `[...]`.
- Windows Firewall already lets Node.js in on this PC's network.

**A machine answers a hover, click or tap anywhere on it** (asked 2026-11-24: only its edge did, on the phone not at all). The map draws on a canvas (`preferCanvas`), where Leaflet itself tells which shape is under the pointer, by a polygon's `_containsPoint`. That casts one ray across all of a building's boxes together, even-odd: where two boxes overlap, the crossings cancel out and the point counts as outside. `world/map/ring-hit.ts`: `inAnyRing` tests each box by itself; `world/map/building-outline.ts`: `BuildingOutline`, a `L.Polygon` subclass whose `_containsPoint` uses it, made by `buildingShape` for the world map and the optimal arrangement alike. It overrides a member Leaflet's typings leave out, so it reaches `_parts` and `_pxBounds` through a cast. Measured in headless Chrome, Leaflet's mouse moves are throttled to one per 32 ms, so a scan has to wait between moves; a single-box machine hovered whole with or without the change, so the change shows only on machines of several boxes. The owner confirmed clicking works.

**Also: a tier mark isn't a number the model wrote** (asked 2026-11-24). An explanation said to "rebuild the Conveyor Belt Mk.4 as a Mk.5", and its panel warned of the number 5. `NumberCheck.Unsupported` already passed over the numbers in names the facts give, as "Conveyor Belt Mk.4"; but the belt to rebuild as is the model's own advice, not in the facts by name. Now a tier mark (`Mk.5`, `Mk 5`, `Mk. 5`) is passed over wherever it stands, by a second `[GeneratedRegex]`; a number beside it, as "2 of them", is still flagged. Tests in `NumberCheckTests`.

The primer has a new section, *Frontend › Phones and touch screens in CSS*: media queries for width and for hover, `dvh`, host bindings and pointer capture.

### Technical choices to overrule

- **600 px** is the phone's breakpoint, as Angular Material's toolbar uses.
- **40 px** is the shortest swipe; less is a tap.
- **The sheet opens at half height** on a phone, as the Info tab is open at first on every screen. It could start closed.
- **The legend is folded on phones only;** a tablet shows it open (it's large there, but fits).

## Tests

- `sidebar/sheet.spec.ts`: up to full height on a swipe up, down a step on a swipe down, closed below half height, a tap switches.
- `sidebar/sidebar.spec.ts`: the sheet stands at half height at first and at full height once swiped up; swiped down from half height it closes to the bar; it opens at half height again after being closed from full.
- `world/map/map-page.spec.ts`: the legend's button opens and closes it.
- `sidebar/sidebar-tabs.spec.ts`: before a 3D view, the tab closes on a phone and stays open on a wider screen (`matchMedia` stubbed).
- `world/map/ring-hit.spec.ts`: a point lies on a building where its boxes overlap, where only one lies, and not beside it.
- `world/area/framing.spec.ts`: the camera's distance is the same on a square or wide screen; on a tall one, further for a wide factory, not for a tall one.
- 362 web tests pass (exit code 0); lint and the format check pass; the build is clean.

**Looked at in headless Chrome** over the DevTools protocol, at 390×844 (a phone) and 820×1180 (a tablet), with touch emulated:
- the page is no wider than the screen;
- with the sheet closed, the map ends above the bar of tabs;
- at full height, the sheet stops below the toolbar.

Not yet tried on a real phone.
