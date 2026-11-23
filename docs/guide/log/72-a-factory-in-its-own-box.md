# Step 72 · A factory in its own box

**Goal:** in a factory's 3D view (Concrete Below in the Learning Snapshot) the factory on the floor above was drawn too, which was confusing. The view took the factory's rectangle on the map at every height. Now it's a box in x, y and z, and inside it, whatever isn't connected to the factory is faded (asked 2026-11-23). Branch `feat/game-icons`, uncommitted, after step 71.

## How it's built

- **`Area`** (`area-model.ts`) gets an optional height range, `z0` and `z1`. `inArea` checks it when it's there, so `curveInArea` does too. `areaAround` gives it: the margin (15 m) below the lowest point and above the highest.
- **The factory's box** is built from every corner of its machines' boxes (`buildingBoxes`), not from where they stand, so it reaches from their feet to their roofs.
- **What's connected** (`FactoryDefinition.ConnectedIn`, in the domain): the factory's machines, and a walk from them through logistics in both directions, owned or shared, up to the providers, consumers and containers at the lines' ends. It stops at a machine that isn't the factory's, so a miner is connected only when no other machine lies in between: Reinforced Iron Plates gets its plates and wire from other factories' constructors and reaches no miner. Iron Ingots, Concrete Below and Steel Ingots 1 have a connected miner inside their box. The factory preview returns it as `connected` (`FactoryTraces.Preview`); `AreaPage` asks the preview for the factory's own machines (`factoryParts`). No new endpoint.
  - **First try, in the browser:** what the factory owns plus the piece across each line. On Concrete Below the sink and the storage container were faded: the line into them also touches another factory's machine, so the factory owns none of it (BR-W3), and they lie further on than the piece across. The map response has no connections, so the walk had to move to the server.
- **Faded:** buildings, their factory outlines, and belts and pipes not connected get a transparent material (opacity 0.18, no depth writes, so what's behind stays visible). Belts are merged per kind, height *and* faded or not. Faded things can still be clicked.
- **Cut off at the box:** six clipping planes (`bounds`) next to the height cut-off's plane. In a drawn area their constants are `Infinity`, so they cut nothing. A click on a belt counts only where every plane still keeps it.

Written into ADR-0024 as an amendment.

### Technical choices to overrule

- **Opacity 0.18** for what's faded.
- **Until the preview arrives, nothing is faded:** the view draws at once, and fades a moment later.
- **An area drawn on the map is unchanged:** every height, belts not cut off.
- **A shared main line lights up:** whatever the factory's lines run into is connected, so a bus shared with other factories is drawn in full colour inside the box, up to their machines.

## Tests

- **Domain** (`FactoryDefinitionTests`, 3 new): what's connected reaches along a shared line to the sink at its end; it holds the machines and their supply up to the miner; it stops at another machine.
- **Web** (2 new): the box holds nothing above or below it, and a belt is in it only with a point inside it, height included. `AreaPage` has no spec (WebGL, step 63).

.NET: build and tests pass, exit code 0. Web: 283 tests pass, exit code 0; the build is clean. `api/openapi.json` regenerated. Not tried in a browser after the fix.
