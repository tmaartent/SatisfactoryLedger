# ADR-0031: A sidebar of tabs on the right of every World page

- **Status:** Accepted
- **Date:** 2026-11-22

## Context
Each World page had its own side panel. The map had the snapshots on its left and the factories on its right. The 3D view had its panel with the clicked building, the steps for defining a factory and the factories in the area. The factory page had the 3D view's panel inside it. Step 62 then added a fourth: the explanation, docked right in the shell, 380 px wide. Every panel took width for itself, which would make the site hard to make responsive.

## Decision
*(Decided 2026-11-22.)*
- **One sidebar, in the shell, on the right of every World page:** a rail of icons, one per tab, and the open tab beside it. Clicking the open tab's icon collapses the sidebar to the rail; clicking any icon opens that tab. **Open by default.** The Catalog has no tabs, so no sidebar.
- **Each page brings its own tabs:**

  | Page | Tabs |
  |---|---|
  | World map | Info *(2026-11-23)*, Factories, Unused supply, Unknown supply *(both 2026-11-23)*, Resource nodes *(2026-11-23)*, Explanation; Snapshots until 2026-11-23, now above the tabs |
  | Area in 3D | Info, Factories, Explanation |
  | Factory | Info (its 3D view's), Explanation |
  | Optimal arrangement | Explanation; no snapshot picker above (a Factories tab, 2026-11-23, went the same day: a factory is picked by clicking it) |
  | Snapshot (findings) | Explanation; the page is gone since 2026-11-23, step 81 |

  The map's own factories list and snapshots column and the 3D view's panel are gone: the page gets the width.
- **The map's Info tab is the overview of the findings** *(decided 2026-11-23, step 67)*: the cards from the top of the findings page. A count of a status (at 100%, starved, output blocked, without a recipe; a generator's alike) is a button: clicked, the map dims everything else, outlines those buildings boldly, rings each one and zooms to fit them. Clicked again, all show. Showing a status lets go of a highlighted factory, and the other way round. A click anywhere else on the map lets go of both, so nothing is faded *(decided 2026-11-23, step 80)*; a click on a shape that does something itself (a factory's machine, a held-back machine) doesn't. *The findings page stayed until step 81 (2026-11-23), when the owner found everything on the map: `/snapshots/{id}` now leads to the map. Its import warnings (recipes the catalog doesn't know, buildings the analysis leaves out) moved to the Info tab, under what the snapshot holds; its lists of generators and drone ports went with it. The optimal arrangement's page stays; since step 83 a drag with ctrl held on the map opens it.*
- **The map's Info tab counts the bottlenecks too** *(decided 2026-11-23, step 77)*, in a row of their own under the machines and generators: clicked, the map rings them halfway along and zooms to fit them, as for a status. Only the map shows that row; the findings and factory pages don't give the overview a count.
- **The findings page's Unused supply and Unknown supply are tabs of the map** *(decided 2026-11-23, step 77)*, after Factories: a list rather than a table, as the sidebar is narrow. Each entry is its source with its icon, linked to it on the map (`?focus=`), and its items: what an unused source gives of what it could, the most left over first; what an unknown one is assumed to give, and how long its stock lasts.
- **The map's Resource nodes tab picks which nodes it shows** *(decided 2026-11-23, step 76)*: a row per resource, a column per purity from impure to pure, each cell the node's pin head (the resource's icon on its purity's colour, in a white ring). A click hides that resource at that purity on the map, or shows it again; a cell is edged and filled in its purity's colour while shown (filled asked too, as only the edge wasn't visible enough), grey edged with its badge faded while hidden. *Show all* and *Hide all* above. What's hidden is kept in the browser, so a resource the game adds shows at first. It replaces the *Resource nodes* switch above the map. The tab is there with or without a snapshot, after Snapshots, so the sidebar still opens on Snapshots without one.
- **The snapshots are above the tabs, on every World page** *(decided 2026-11-23, step 79)*: every tab is about the snapshot shown, so picking it is apart from them. A picker atop the sidebar names the snapshot shown; it opens the list of snapshots, each linking to its map, with a delete button, and beside it is the import of a save. Both say in a tooltip what they're for, and before there's any snapshot, that there's none yet and how to make one. When the sidebar is collapsed to its rail, or the screen is narrow, only their icons show. The shell puts it there (`<app-sidebar>` takes what goes above, `ng-content`); its code loads once the page is up (`@defer`), so the shell's first load stays as small. It replaces the map's Snapshots tab. The optimal arrangement has none: it's about the snapshot it was opened from *(decided 2026-11-23, step 85)*.
- **Under the overview, the map's Info tab says what the snapshot holds** *(decided 2026-11-23, step 79)*: the counts of the findings page's *What it holds* (machines, logistics, providers, consumers, connections), one component for both.
- **A tab's title is a band of its own** *(decided 2026-11-23, step 79)*: its icon and title, larger, in the primary colour, on a band with a line under it, so it stands apart from the tab's content.
- **A factory's Info tab starts with what comes in and what leaves** *(decided 2026-11-23, step 82)*: per item, only what crosses the factory's edge, now and at 100%; what its machines make and take inside is in neither. The tab is its 3D view's, so the page hands it a template for its top (`AreaPage.infoTop`); the 3D view of an area drawn on the map gets none.
- **A tab is declared in the page's template,** as `<ng-template appSidebarTab="info" label="Info" icon="info">`. Its content keeps the page's bindings and styles. A root service (`SidebarTabs`) holds the tabs, which one is open and whether the sidebar is collapsed.
- **Clicks in the page show the tab that answers them,** opening the sidebar if it was collapsed:
  - a building, belt, pipe or lift clicked in 3D shows Info, with *Explain why* for a held-back machine;
  - starting to define or edit a factory shows Info, with the steps;
  - saving a factory in the 3D view of an area stays there and shows Factories, with it among the others, so the next one can be defined *(decided 2026-11-23, step 70; it opened the factory's page)*;
  - asking for an explanation shows Explanation.
- **While defining a factory, its name, Save and Cancel are in the page's header,** as on a factory's own page. Switching tabs can't hide them.
- **The tab last asked for is remembered across pages:** when the next page has a tab by that key, it's shown; otherwise the page's first tab is.
- **World in the toolbar goes back to the snapshot last looked at** *(decided 2026-11-23, step 68; it opened the Snapshots tab too, until step 79 put the snapshots above the tabs)*: any page of a snapshot (its map, 3D view, a factory) makes it the one remembered, in the browser's storage, so it's there when the site is opened again. Deleting it forgets it. Before any snapshot is looked at, World is the map of none, as before.

## Alternatives considered
- **A panel per page** (as before): no shared code, but every page takes width its own way, and the responsive version would need each one solved separately.
- **Always lying over the page:** no width taken, but on a desktop it covers the map you're looking at beside it.

## Consequences
- **On narrow screens (below 960 px) the open tab lies over the page,** left of the rail. A bottom sheet for phones is left for when the site is made responsive.
- **The Leaflet map and the 3D views follow their own size** (`ResizeObserver`), since opening and collapsing the sidebar changes it.
- **Its tooltips are the browser's `title`:** Material's tooltip in the shell would put the CDK overlay in the initial bundle, over its 500 kB budget.
