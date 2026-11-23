# Step 85 · The optimal arrangement works like the map

**Goal:** four changes to the optimal arrangement's page (asked 2026-11-23):
- its map is held to the map picture as the world map is;
- the map picture is loaded by one shared service;
- shift-drag draws the area and ctrl-drag keeps a part out, instead of the *Draw the area* and *Keep out* buttons;
- then, after the owner looked: the settings stay on the page after all; the snapshot picker goes from this page; the factory list moves to a sidebar tab, there from the start; and the two switches go, as where the factories stood isn't wanted and the belts always are.

Branch `feat/ctrl-drag-arrangement`, uncommitted, after step 84.

## How it's built

- **`MapArt`** (`world/map/map-art.ts`), a root service: the map picture as a `resource`, none on a 404. The world map and the arrangement both read `inject(MapArt).art`, so it's loaded once for both.
- **`holdToExtent(map, extent, leastZoom)`** (`world/map/hold-to-extent.ts`): what `MapPage.holdToMapArt` did, as one function for both maps. It takes only the four methods of a Leaflet map it uses (`Holdable`), so its test hands it a fake map with a size, which jsdom can't lay out. The extent is `mapExtent`, the picture widened where buildings stand past it. The arrangement holds its map when the picture or the world arrives, and on a resize.
- **Keys to draw** (`ArrangementPage.listenForArea`), as on the world map: the press is caught in the capture phase, shift draws the area, ctrl (⌘) draws a part to keep out, but only once there's an area. Panning is switched off right there; the page used to do it from an effect, too late for the press that starts the drag. The drag keeps what it draws from its start. Leaflet's own shift-drag zoom is off. Above the map: *Hold shift and drag to draw the area to arrange*, then *Hold ctrl and drag inside the area to keep out a part · 2 kept out; shift to draw it anew*.
- **The settings stay on the page** (a Settings tab was tried: two columns, one *Arrange*; then undone): the form as before, *Arrange* and *Reset* above the map with the line saying which key does what.
- **No snapshot picker** above the sidebar on this page (`App.picksSnapshot`): the arrangement is about the snapshot it was opened from.
- **The Factories tab** (`appSidebarTab="factories"`), from the start: with a run, the factories it arranged and how far each goes (`moves`, as the list beside the map had); before one, `factoriesToArrange` (`arrangement-model.ts`): those whose machines' middle lies inside the area drawn, or every factory with machines before one is drawn, by name. The middle is that of the rectangle around their machines' positions; the arrangement takes their footprints', near enough for a list. Picking factories fades the rest, before a run too: the world's machines of other factories are drawn faint.
- **Before a run, buildings are their footprints**, not dots (asked after the rest): each machine the ground it covers, in its factory's colour and faded as before when others are picked; a station in dark grey. The world map's private `shape` became `buildingShape` (`world/map/building-shape.ts`), which both maps use: the footprint where the class has one, else a dot. The world map's `shape` hands over to it, so its callers didn't change.
- **Tried, then replaced in step 86:** the whole world drawn before a run, and a picked factory's belts as built, lit up in its colour (the factory preview's `connected`). Too realistic, and the factories it trades with didn't light up; step 86 treats each factory as a whole and draws only the belts between them.
- **The two switches are gone:** *Where they stand now* (the world's machines over a run, and each factory's rectangle as built), never drawn now; *Belts and pipes on the floor below*, always drawn now. `showNow`, `showBelts` and `machinePoints`, which only the rectangles used, are gone.
- The back arrow goes to the map, not to the findings page's old address.

Written into ADR-0027, ADR-0020 and ADR-0031.

### Technical choices to overrule

- **Ctrl without an area does nothing**: a part kept out lies inside the area.
- **Shift with an area draws a new one**, keeping the parts kept out.
- **Before a run, the Factories tab lists what a run would arrange**, judged by the machines' positions rather than their footprints.
- **Picking before a run fades the other factories' machines** on the map, as picking does with a run.

## Tests

New: the service's picture, and none on a 404 (2); holding to the picture, letting go without one, waiting for a size (3); on the arrangement page, the map picture, shift draws the area (no button), ctrl keeps a part out only once there's an area, the settings and *Arrange* on the page and no Settings tab, the Factories tab before any run, picking a factory and letting go, no switches (7); `factoriesToArrange`, every factory before an area, those inside, none without machines (3); the shell has no snapshot picker on the arrangement (1); `buildingShape`, a footprint where there is one, else a dot of the size asked (2); 337 web tests passed then (the `connectedColour` tests and the one for asking what's connected went with step 86), exit code 0; lint and the format check pass; the build is clean. Not tried in a browser.
