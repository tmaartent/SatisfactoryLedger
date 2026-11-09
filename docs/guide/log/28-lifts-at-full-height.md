# Step 28 · Lifts at full height

**Goal:** lifts in the 3D view as tall as they are, instead of 2 m cubes (your review of step 26).

## How to use it

Import your **save** again: snapshot version 11 gives lifts their height. In older snapshots lifts stay cubes.

## How it works

- **The save** keeps a lift's top as `mTopTransform`: a transform (translation, rotation, scale) in the lift's own coordinates. The reader turns its translation by the lift's rotation and adds it to where the lift stands.
- **A lift gets a curve** of two points, base and top, with no tangents: the cubic Hermite curve between them is then a straight line. So a lift is a conveyor like a belt:
  - **in 3D**, a tube, merged with the belts. The height cut-off and the clipping at the roof apply to it too. A tall box would have stuck through the floors above when you look at one floor.
  - **on the 2D map**, a line of zero length: a dot where the belts meet, with the belt colours and its contents in the tooltip. Before, it was a logistics marker.
- **Snapshot version 11**; a version 10 snapshot still reads, with lifts without a curve.

## Files

| Where | What |
|---|---|
| `World.SaveFile/SaveFileReader.cs` | `LiftCurveOf`: base to top |
| `World/Snapshots/SnapshotDocument.cs` | Version 11 |
| `docs/adr/0024-areas-in-3d-with-threejs.md` | The fallbacks, with lifts |

No web change: the 3D page and the map already draw every conveyor with a curve.

## Points worth noticing

| .NET / C# | Java / Spring | Here |
|---|---|---|
| Pattern matching through a property tree: `…?.Value is not ArrayProperties top \|\| … is not VectorD { Value: var translation }` | Chained `instanceof` checks with pattern variables (Java 16+), or a visitor | `SaveFileReader.LiftCurveOf` |

## Tests

| Where | Tests |
|---|---|
| `SaveFileReaderTests` | A lift runs straight up from where it stands to its top (4 m in the test world) · a splitter or merger has no curve |
| `WorldMapTests` | A lift is drawn as a belt, straight up · splitters and mergers aren't conveyors, and are on the map where belts meet them |
| `SnapshotDocumentTests` | Version 11 · a version 10 snapshot still reads |

Red first: the lift had no curve (a `NullReferenceException` on `Curve!`). **Totals:** 754 .NET tests.

## Choices worth a second look

1. **A dot on the 2D map** instead of the logistics marker. Lifts could get both, but then 3D would draw a cube too.
2. **A lift can't be clicked in 3D**, like belts (they're merged for speed).
