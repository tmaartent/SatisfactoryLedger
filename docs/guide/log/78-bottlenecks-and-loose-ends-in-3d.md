# Step 78 · Bottlenecks and loose ends in red, in 3D

**Goal:** finding a bottleneck went from the map to a rectangle around it to the 3D view, and then looking for the belt. Now the bottleneck is red in 3D, and so is a belt, lift or pipe with an end connected to nothing, a *loose end* (asked 2026-11-23; the owner chose loose ends over belts leaving the selection, and the same red for both). Branch `feat/node-filter`, uncommitted, after step 77.

## How it's built

- **Bottlenecks** were already in the 3D view's data: the analysis it loads for its Info panel. Only their colour was missing.
- **Loose ends needed the import.** The save stores, for every connector, what it's linked to, or nothing (`Connector.LinkedTo`); the import kept only the links. Now:
  - `SaveFileReader` counts the ports of each belt, lift and pipe (and pump or valve) linked to nothing: `SavedLogistics.LooseEnds`. A port linked to a building the ledger doesn't read counts as linked: it goes somewhere.
  - The snapshot keeps it, for conveyors only: `BuildableDocument.LooseEnds`, null for other buildables. **Snapshot version 14**; a version 13 snapshot reads as 14 with none known.
  - The map response passes it on: `MapConveyor.LooseEnds`, 0 when not known. `api/openapi.json` regenerated.
  - Counting them afterwards from the links kept wouldn't work for pipes: the import folds each pipe network into a hub (BR-A6), and a pipe's links say which pipes touch, not which end.
- **3D** (`area-page.ts`): `tubeColour` (`area-model.ts`) gives red for a bottleneck or a loose end, else the kind's colour. Tubes are merged per colour rather than per kind, so a red one is its own mesh. The Info panel of a clicked belt says *One end* (or *Both ends*) *connected to nothing*; the legend says *Bottleneck or loose end*.

Written into ADR-0024 and the glossary (*Loose End*).

### To see loose ends

**Import the save again.** Snapshots imported before this step know no loose ends, so they show none; bottlenecks show at once.

### Technical choices to overrule

- **Pumps and valves count** (they're pipes to the ledger), but they have no curve, so they aren't drawn as tubes.
- **The map's 2D view doesn't colour loose ends**, only bottlenecks, as before.

## Tests

Test-first, except the reader's: which fixture belt has a loose end had to be found first, with a probe test (thrown away) after writing the count. The fixture save has one: `Build_ConveyorBeltMk6_C_2147274025`.
- .NET (4 new): the reader counts a loose end, and none on a belt and lift connected at both ends; the snapshot keeps them, and a version 13 snapshot reads with none; the map response says them. The snapshot's version tests now expect 14.
- Web (3 new): a tube's colour is its kind's, red for a bottleneck, red for a loose end.

.NET build and tests pass, exit code 0. Web: 312 tests pass, exit code 0; lint and the format check pass; the build is clean. Not tried in a browser.
