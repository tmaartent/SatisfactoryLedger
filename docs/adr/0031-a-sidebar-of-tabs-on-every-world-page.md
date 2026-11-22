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
  | World map | Info *(2026-11-23)*, Factories, Snapshots, Explanation |
  | Area in 3D | Info, Factories, Explanation |
  | Factory | Info (its 3D view's), Explanation |
  | Snapshot (findings) | Explanation |

  The map's own factories list and snapshots column and the 3D view's panel are gone: the page gets the width.
- **The map's Info tab is the overview of the findings** *(decided 2026-11-23, step 67)*: the cards from the top of the findings page. A count of a status (at 100%, starved, output blocked, without a recipe; a generator's alike) is a button: clicked, the map dims everything else, outlines those buildings boldly, rings each one and zooms to fit them. Clicked again, all show. Showing a status lets go of a highlighted factory, and the other way round. *The findings page stays for now; its list is covered by the map and the factory pages.*
- **A tab is declared in the page's template,** as `<ng-template appSidebarTab="info" label="Info" icon="info">`. Its content keeps the page's bindings and styles. A root service (`SidebarTabs`) holds the tabs, which one is open and whether the sidebar is collapsed.
- **Clicks in the page show the tab that answers them,** opening the sidebar if it was collapsed:
  - a building, belt, pipe or lift clicked in 3D shows Info, with *Explain why* for a held-back machine;
  - starting to define or edit a factory shows Info, with the steps;
  - asking for an explanation shows Explanation.
- **While defining a factory, its name, Save and Cancel are in the page's header,** as on a factory's own page. Switching tabs can't hide them.
- **The tab last asked for is remembered across pages:** when the next page has a tab by that key, it's shown; otherwise the page's first tab is.
- **World in the toolbar goes back to the snapshot last looked at, on the Snapshots tab** *(decided 2026-11-23, step 68)*: any page of a snapshot (its map, 3D view, a factory) makes it the one remembered, in the browser's storage, so it's there when the site is opened again. Deleting it forgets it. Before any snapshot is looked at, World is the map of none, as before.

## Alternatives considered
- **A panel per page** (as before): no shared code, but every page takes width its own way, and the responsive version would need each one solved separately.
- **Always lying over the page:** no width taken, but on a desktop it covers the map you're looking at beside it.

## Consequences
- **On narrow screens (below 960 px) the open tab lies over the page,** left of the rail. A bottom sheet for phones is left for when the site is made responsive.
- **The Leaflet map and the 3D views follow their own size** (`ResizeObserver`), since opening and collapsing the sidebar changes it.
- **Its tooltips are the browser's `title`:** Material's tooltip in the shell would put the CDK overlay in the initial bundle, over its 500 kB budget.
