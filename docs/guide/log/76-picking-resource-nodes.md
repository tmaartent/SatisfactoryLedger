# Step 76 · Picking which resource nodes the map shows

**Goal:** a tab beside the map with a row per resource and a column per purity, from impure to pure. Each cell is the node's pin head; a click hides that resource at that purity on the map, or shows it again. A shown cell is edged and filled in its purity's colour, a hidden one grey edged (asked 2026-11-23; the fill after a first try with the edge alone, which wasn't visible enough). Branch `feat/node-filter`, from `main` after #21.

## How it's built

- **`node-filter.ts`** (`world/map`), no Angular in it:
  - `Purities`: impure, normal, pure, the columns' order.
  - `nodeKinds(nodes, name)`: a row per resource, sorted by its name, counting its nodes per purity.
  - `nodeKey(resource, purity)`: one kind of node as a key, `Desc_OreIron_C:pure`.
  - `shownNodes(nodes, hidden)`: the nodes of every kind not hidden. `MapPage.drawNodes` draws those.
- **`HiddenNodes`** (`hidden-nodes.ts`), a root service like `LastSnapshot`: the kinds hidden, as a signal, kept in `localStorage` (`satisfactory-ledger.hidden-nodes`) inside try/catch, so blocked storage only means it's forgotten when the site closes. It keeps what's **hidden**, not what's shown: a resource a game update adds shows at first. `toggle` reuses `selection.ts`'s `toggle`.
- **The tab** (`map-page.html`, `appSidebarTab="nodes"`, order 2, after Snapshots): *Show all* / *Hide all*, a header row of the purities, then the rows. A cell is a button (`aria-pressed`, its label and tooltip *Pure Iron Ore · 3 nodes*); a purity a resource has no node of is an empty space, nothing to click. A shown cell is filled in its purity's colour too, the badge standing out on it by its white ring and shadow; a hidden cell's badge is faded (opacity 0.35). The cell's border and fill and the badge's background read the purity's colour from `--purity`, set by Angular's `[style.--purity]` binding; the badge looks like the pin's head on the map (white ring, shadow).
- **The switch above the map is gone:** it and the tab would have said different things. *Hide all* does what turning it off did.
- **Order 2:** at 0.5, before Snapshots, the tab was the first one without a snapshot, and the sidebar opened on it instead of the snapshot list.

Written into ADR-0031.

### Technical choices to overrule

- **The name and the count only on hover** (and for screen readers): your three columns stay three.
- **Remembered per browser,** not per snapshot or per player.
- **A well's satellites count as the well's resource**, with the nodes: a row is a resource, whatever extracts it.
- **The tab comes last**, after Snapshots.
- **A hidden cell's badge is faded** (0.35), on top of the grey edge asked for: shown and hidden then differ at a glance.
- **No impure Caterium cell:** the game has none (9 normal, 8 pure, in the map tool's table and the Catalog alike), so that cell is empty.

## Tests

10 new: the purities' order, a row per resource with its counts, hidden kinds left out (`node-filter.spec.ts`); none hidden at first, a toggle hides and shows, kept across a reload, hide all and show all (`hidden-nodes.spec.ts`); and on the map page the tab's cells, a click hiding and showing the pin, *Hide all* and *Show all*. Two tests that list the map's tabs now list Resource nodes too. 304 web tests pass, exit code 0; lint and the format check pass; the build is clean. Not tried in a browser.
