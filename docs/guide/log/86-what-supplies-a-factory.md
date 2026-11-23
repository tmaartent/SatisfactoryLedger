# Step 86 · What supplies a factory, before arranging

**Goal:** before a rearrangement, see how the factories relate: picking a factory lights up what supplies it, the factories, miners, freight platforms and drone ports, each factory a whole, with the existing belts and pipes between them showing the connection (asked 2026-11-23). Branch `feat/ctrl-drag-arrangement`, uncommitted, after step 85.

## Three tries first, all undone

1. **Every belt drawn, a picked factory's lit up** (step 85's last round): too realistic, and the factories it trades with stayed faded. Its walk (`ConnectedIn`, step 72) stopped *before* another factory's machine, so nothing said which factory was on the other end.
2. **The belt work as a run counts it**: per link, belts × plots between middles, from a new endpoint, as lines between plot middles. Hard to read, not the relations. Reverted whole: endpoint, domain addition, page code.
3. **A walk both ways that included the machines it reached**: on the owner's Adaptive Control Units it lit up its four suppliers, but also Fused Modular Frames and Radio Control Systems, fed from the same splitters (the walk went up to a splitter and down its other branch), and Assembly Director Systems, which it feeds. The owner wants its suppliers only.

## How it's built

- **Domain** (`FactoryDefinition.SuppliesIn`): walking **upstream only** from the factory's machines, through logistics, owned or shared, to the providers where its lines start (miners, extractors, stations, drone ports) and the machines of the factories feeding them, no further. Upstream at a splitter is its input, so its other outputs, and the sibling factories on them, are never reached; nor is anything downstream. Test-first, 4 tests: the line up to a supplier's machine and no further, no sibling on the same splitter, nothing it feeds, the miner at a line's start. `ConnectedIn` is as it was (step 72's 3D view).
- **Its own lines count, its internal belts don't** (after AI Limiters, 2026-11-23: its miners lit up, but not the lines to them). A factory owns a line from miners that feed only it (BR-W3), and the page left out everything owned. Now the walk decides: after going up, it comes back down from the sources it found, along the way it went, and only those pieces count. A belt between two of its own machines has no outside source upstream, so it's left out; its own ore line has the miner upstream, so it's in. Its own machines aren't in either. On the owner's world AI Limiters now draws 114 pieces and 20 splitters out to its 2 miners, and leaves out its 40 internal pieces. Test-first: its own line from the miner, not its belts between its machines; not its own machines.
- **The factory preview** returns it as `supplies` (`FactoryPreview`, `FactoryTraces.Preview`). On the owner's world, Adaptive Control Units' supplies reach exactly Computers, Circuit Boards, Heavy Modular Frames and Automated Wiring.
- **`relationsOf`** (`arrangement-model.ts`): from each picked factory's supplies, which factories light up (the picked ones and every factory owning a machine its supplies reach) and which parts lie between (the rest), each by the factory picked first where two share it.
- **What each line carries** (asked after): the lit pieces that meet end to end are one line (`lineGroups`: ends in the same metre, height included, so stacked belts stay apart; a splitter ends a line). Its items (`lineItems`) are what's on its belts at save time and what its handover into the factory brings (the preview's `inputs`), for a pipe or an empty belt. The icons sit in a pill at the line's middle (`.line-items` in `styles.scss`, a Leaflet marker), their names on hover. Only lines of 30 m or more get one (`linesToLabel`): a factory's manifold is many short stubs from its splitters into its machines, and labelling each piled icons up on the factory (the owner's screenshot, 2026-11-23).
- **The page before a run** (`ArrangementPage.drawWorld`): no belts but those between, drawn as built in the picked factory's colour; splitters, mergers and containers only on those lines; miners and extractors, stations and sinks always, lit up where a line starts, faded otherwise; every factory's machines on top, lit up or faded as a whole. While picking, a factory lit up takes the colour of the factory picked it supplies, and the rest are grey: lit in their own colours, two of Adaptive Control Units' suppliers (Circuit Boards in salmon, Computers in mint) couldn't be told from the faded factories, whose stacked floors add up to a colour, and the two that looked lit only shared its pink by chance (the eight colours repeat). Asked once per factory picked (`connectedParts`), only before a run.

Written into ADR-0027 and the glossary (*Supplies*).

### Technical choices to overrule

- **A pipe network** is one in the model (BR-A6): every pipe feeding it counts as a supplier of a factory it feeds.
- **A merger** brings everything merged into it: each source merged in is a supplier.

## Tests

- .NET: `SuppliesIn`, 6 tests (2 for its own lines and machines). All .NET tests pass, exit code 0. `api/openapi.json` regenerated (`supplies` in the factory preview).
- Web: `relationsOf`, 3 tests; `lineGroups`, `lineItems` and `linesToLabel`, 3 tests; the page asks the factory preview when a factory is picked. 340 web tests pass, exit code 0; lint and the format check pass; the build is clean.

Checked on the owner's world through the API (Adaptive Control Units, above); not tried in a browser.
