# Step 52 · The snapshots beside the map

**Goal:** the world map is the main view of the site, and the snapshots list held little (asked 2026-11-19). Branch: `feat/optimal-arrangement`, on top of step 51 (committed and pushed), uncommitted.

## What you asked, and what changed

- **The snapshots are a side bar next to the map** (`SnapshotsSidebar`): **Import save** on top, then each snapshot by its **world name**, linking to its map, with a **delete** button. The snapshot whose map is shown is highlighted. Saved date, counts and analysis status are gone from the list.
- **The site starts on the map with no snapshot loaded** (asked after; at `/` and `/snapshots`): only the resource nodes, every one, in their purity's colour, the view fitted to them, and the side bar beside it. It's the same map page (`MapPage`), its snapshot `id` now optional: with none, its requests for the snapshot, its map, analysis and factories don't run, and the controls that need a snapshot (*Findings*, *Open an area in 3D*, the toggles, *Highlight a factory*) don't show; the legend shows only the nodes. (A first version opened the newest snapshot's map instead.)
- **Importing a save opens its map**; before, its snapshot page.
- **Deleting the snapshot shown** goes back to the start, the map of the resource nodes. Deleting another one stays where you are. The question before deleting is unchanged (`SnapshotDeletion`).
- **The site uses the whole width of the window** (asked after): the pages were at most 1400 px wide, centred; now only a 16 px margin each side (`app.scss`). Every page widens, the tables too.
- **A tidier map** (asked after):
  - **The factories are listed to the right of the map**, each with its colour. Picking one highlights it and zooms to its machines, as the drop-down did; picking it again highlights none. A factory whose machines can't be traced is greyed out, as it was disabled in the drop-down. The icon after its name opens its page. The *Highlight a factory* drop-down is gone; `?factory=` still highlights one when the map opens.
  - **No more *Colour belts by what they carry***: belts are grey, pipes teal, bottlenecks red. `mainItem` and `itemColour` in `map-model.ts` had no other caller and are deleted, with their two tests.
  - **No more checkboxes to hide machines, belts, providers and the rest** (Leaflet's layers control). *Free resource nodes* stays.
- **A factory's page shows the factory in 3D** (asked after): first the **overview** cards (machines, at 100%, starved, output blocked, without a recipe; generators alike) with its power, then the **3D view** of the factory, then **Input** and **Output**. The findings tabs at the bottom are gone, and so is the *3D* button. The 3D view is the 3D page itself (`AreaPage`) inside the factory's page, with everything it can do (asked: "the 3D view still needs all the functionality"): pointing at a building and its panel, the height cut-off, defining a factory, editing and deleting one. Embedded, it has no title or back arrow of its own and is 65% of the window high. Saving a factory there asks for the factory's findings again.
  - **Edit and Delete at the top** (asked after), where *Map* was; *Map* is gone. *Edit* edits the factory in the 3D view below, its machines chosen and its name filled in (`AreaPage.edit`, the one method the page calls on it, through a template reference `#area`). *Delete* asks first, then goes to the map. The 3D view on a factory's page has no *Define a factory* and no *Factories here*: the factory is defined already.
  - **Editing in the header** (asked after): while editing, the factory's name at the top is the text field, and *Save* and *Cancel* stand where *Edit* and *Delete* were; Enter saves too. The small name field and its buttons in the 3D panel are gone there (they stay on the 3D page). The 3D view keeps the editing (`defining`, `factoryName`, `canSave`, `saveError`, `save()`, `stopDefining()`, now public), and the page reads and drives it through `#area`, so there's one editing state, not two to keep in step.
  - **Less in the 3D view on a factory's page** (asked after): no count of machines and buildings in the area, and a clicked machine's panel has no factory link, no position and no *Show on the map*. The 3D page itself keeps them.
  - **A legend of the machines' colours** in the 3D view, here and on the 3D page: *Running* green, *Starved* red, *Output blocked* orange, *No recipe* grey. The colours were there; what they meant wasn't said.
  - The overview cards are a component of their own (`FindingsSummary`), shared with the snapshot's findings.
- The toolbar says **World** instead of *Snapshots*. The old list page (`SnapshotsPage`) is deleted, its two deletion tests moved to the side bar's spec.

## Choices made (overrule if you like)

- **The saved date shows as a tooltip** on a snapshot's name. Two snapshots of the same world have the same name, and that's the only way to tell them apart in the side bar.
- **The map's back arrow became a *Findings* button.** It led to the snapshot page, which is now a step forward from the map, not back. The snapshot page's back arrow goes to its map, not to the list that's gone.
- **On a narrow screen (under 800 px)** the side bar stands above the map, full width; under 1000 px the factories go under the map.
- **No recipe is light grey (`#9e9e9e`), the belts' splitters and lifts blue-grey (`#78909c`)**: close. I left the colours as they are, shared with the map; a stronger colour for *No recipe* is a one-line change if they're hard to tell apart.
- **Delete is hidden while editing**, so only *Save* and *Cancel* show then.
- **The power line** sits under the overview, not under a heading of its own.
- **The factory list's open icon** is my addition: the drop-down only highlighted, but a click on a factory's machine on the map already opens its page, and the list is the natural place for that too.
- The side bar is on the map page only (with or without a snapshot), not on 3D, the arrangement or a factory's page.

## Files

New: `web/src/app/world/snapshots-sidebar.ts`, `.html`, `.scss`, `.spec.ts`; `map/map-page.spec.ts`. Changed: `app.routes.ts`, `app.html`, `app.ts`, `app.scss`, `app.spec.ts` (World, Catalog), `styles.scss` (`.with-sidebar`), `map/map-page.html`, `.ts` (`id` optional, `snapshotParams`, `toggleFactory`) and `.scss`, `map/map-model.ts` and its spec, `factory-page.ts`, `.html` and new `.spec.ts`, new `findings-summary.ts`, `.html`, `.scss` (out of `findings.html`/`.scss`; `findings.spec.ts` now ignores the line breaks in a card), `area/area-page.ts`, `.html`, `.scss` (`embedded`, `saved`, `edit`, `statusLegend`; the editing state public), `snapshot-page.html`. Deleted: `snapshots-page.ts`, `.html`, `.spec.ts`. 182 web tests green, build clean. The side bar's, the map page's and the factory page's specs were written first and seen failing; the factory page's spec stands the 3D view in with a stub, since jsdom has no WebGL. Not seen in a browser.

## Angular / web points

| Point | Java / TypeScript |
|---|---|
| **A component reused across routes**: going from one snapshot's map to another's, Angular keeps the same `MapPage` and the same side bar inside it, and only the `id` input changes. So the side bar reloads its list itself after an import; it isn't created again. | Like a Spring MVC controller, a singleton that gets each request's parameters, rather than a new object per page. |
| **A resource that doesn't run**: when `params` returns `undefined`, a `resource` stays idle and never calls its loader. So `snapshotParams` gives `undefined` without an `id`, and the four snapshot requests simply don't happen. | Like a Spring `Optional` parameter guarding a repository call, but declarative: the request is wired once and switches itself off. |
| **Leaflet in a unit test**: jsdom has no canvas, `getContext` returns null, and Leaflet's canvas renderer fails on its first draw. The spec stubs `getContext` with a `Proxy` whose every property and call returns itself: a context where everything does nothing. | Like a Mockito mock with `RETURNS_SELF`. |
| **A file input in a test**: `input.files` is read-only in jsdom, so the spec defines it with `Object.defineProperty`, shaped like a `FileList` (`length`, `item()`): the code calls `item(0)`, which a plain array doesn't have. | Mockito stubbing a final getter needs similar tricks. |
