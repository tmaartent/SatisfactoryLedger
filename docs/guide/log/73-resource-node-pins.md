# Step 73 · Resource nodes as pins that grow with the map

**Goal:** the resource nodes' markers were 22 pixels at every zoom level, which is tiny next to the land when zoomed in. Now they grow as the map is zoomed in and shrink as it's zoomed out, as the game's own map does, and are pins like the game's: the ring on a stick down to the node, white, on the purity's colour. Every node gets one, not only the free ones (all asked 2026-11-23). Branch `feat/game-icons`, uncommitted, after step 72.

## How it's built

- **`nodeMarkerSize(zoom)`** (`map-model.ts`): 22 px at zoom −2, and √2 times as big per step in: half as fast as the map, which doubles per step. Clamped to 18 px (the icon still readable, zoomed all the way out) and 72 px (zoomed all the way in). The map's zoom runs from −4 to 3.
- **Sized, not redrawn:** the map's container gets a CSS variable, `--node-size`, set again on every zoom. `.node-marker` (`styles.scss`) reads its width, height, padding and ring from it. Leaflet's marker has no size of its own (`iconSize: [0, 0]`); the ring is centred on the node by `translate(-50%, -50%)`. No marker is rebuilt, however many nodes there are.
- **A pin** (`nodeMarkerHtml`, `.node-pin`): the ring's middle is one ring's width from the node, up and to the left at 60° from level. A stick (`::before`, rotated −120° about the node) runs from the node to the ring's edge, and a dot (`::after`) marks where the node lies. Ring, stick and dot are white (`--pin`), as the game's; the purity is the colour behind the icon (`--purity`, set on the pin), and a soft shadow keeps the white ring apart from light ground. Leaflet stacks markers by how far down they lie, so a lower pin covers the ring of one above it, as in the game.
- **Every node, free or not:** `drawNodes` pins all the nodes the Catalog has. `freeNodes` is gone; `nodeTakers` (`map-model.ts`) says which provider stands on which node, by the node's name, and the tooltip names it ("Miner Mk.3 on it") or says "free". Without a snapshot nothing is known of either, so the tooltip says neither. The toggle above the map is now *Resource nodes*, and the legend's dashed *Free node* and *Resource node* entries are gone: the pins speak for themselves. The miner keeps its shape under the pin's dot, edged in the purity's colour, as the legend's *Provider* entry says.
- **Along with the zoom animation:** Leaflet's `zoomanim` event says where an animated zoom goes as it starts, so the size is set then, and a CSS transition of 0.25 s, as long as Leaflet's own, lets the markers grow with the map. `zoom` covers zooms that aren't animated.

Written into ADR-0032.

### Technical choices to overrule

- **The sizes:** 22 px at zoom −2, 18 to 72 px, √2 per step.
- **The ring's dashes and the space inside it grow too** (a sixteenth and an eleventh of the size), so a big marker doesn't look thin. The stick is a twentieth thick, at least 1.5 px; the dot a seventh, at least 4 px.
- **Purity in green, yellow and red** (asked 2026-11-23): `purityColours` is now pure `#43a047`, normal `#fdd835`, impure `#e53935` (were pale yellow, grey and brown). It's the one place for them, so the edges of miners standing on nodes change too, and a free node and a taken one say the same. Green and red are close to the machines' running (#4caf50) and starved (#ef5350) colours, but a miner's edge is thin next to their fill.
- **The pin points the same way for every node:** close nodes' rings overlap, as in the game, instead of being spread out.
- **Only the resource nodes:** the buildings are drawn to scale already (their footprints), and the rest keep fixed sizes.

## Tests

3 new: the size per zoom step (−2, −1, 0), its limits at both ends, and the map setting `--node-size` on its container. The marker's tests now look for the pin and its `--purity`, green for pure. `freeNodes`'s test became `nodeTakers`'s; 2 more on the map page: a node with a miner on it gets a pin, and the legend has no node entry. 288 web tests pass, exit code 0; the build is clean. Not tried in a browser.
