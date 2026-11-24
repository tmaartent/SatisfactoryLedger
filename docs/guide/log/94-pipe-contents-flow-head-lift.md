# Step 94 · What's in a pipe, what flows, and its head lift

**Goal:** show what's in each pipe, how much flows through it, and its head lift (asked 2026-11-24: "Can you also show flow rate and headlift? Is that in the save?"). Branch `feat/pipe-head-lift`, from main.

## What the save has

Read from the owner's save before deciding anything (ADR-0034):

| Wanted | In the save? | Shown from |
|---|---|---|
| Contents | Yes: `mFluidBox` per pipe, pump and junction (m³), the fluid per network | the save |
| Flow rate | No: the game works it out as it runs | the model (BR-A6) |
| Head lift | No, neither per pipe nor per pump | an estimate (BR-A22) |

The Docs file has each pump's head lift twice (`mDesignPressure` 20 and 50 m, as its description says; `mMaxPressure` 22 and 55 m); "Head Lift: 10 m" for extractors and some machines is in their descriptions only. Which way a pump pushes isn't stored as such, but its ports are: in the owner's save 199 pumps had an extractor on the side of `Connection0` and machines on that of `Connection1`, 16 the other way round.

## The owner's choices

- Head lift: **estimate it per pipe** (of: pumps only, per pipe, leave it out).
- **Every fluid output lifts 10 m**, also those the Docs file doesn't say it of (of: every output, only those named, pumps only).
- **A pump resets the reach**, as the game says "Head Lift does not stack" (of: resets, adds up).
- **A warning only**: the flow still sends fluid through (of: warning, warning and no flow).
- Contents and model flow in the same step.
- After the first run on the save: **gas needs no head lift** ("Gasses are rocket fuel, nitrogen, excited photonic matter, dark matter residue, ionized fuel"). The Docs file marks exactly those five `RF_GAS`, so the Catalog reads it from there.
- After the second run: empty pipes "are not used? Some blueprints are all purpose and if the current recipe does not need the pipe, then it is empty". So a fluid port counts as a fluid output only while its building puts out fluid: a machine whose recipe makes one, an extractor of one, a station (`PipeHeadLifts.PutsOutFluid`).
- After the third run the owner tried **`mMaxPressure`** (Mk.1 22 m, Mk.2 55 m), then chose **`mDesignPressure`** (20, 50 m), as the pump's description gives it.

## What was built

- **Catalog:** which fluids are gases: `Item.IsGas` from `mForm` `RF_GAS`, beside `ItemForm.Fluid` (which 55 places read as "goes over pipes"), migration `AddItemGas`, `CatalogItem.IsGas`.
- **Catalog:** pumps, with their head lift: `Pump`, `PumpId`, `PumpDto`/`PumpGroupDto` (the `FGBuildablePipelinePump` section, valve left out), `TranslateToPumps`, a `pumps` table (migration `AddPumps`), the import's pump counts, `ICatalogQueries.GetPumpsAsync` → `CatalogPump`.
- **Save reader:** a pipe's, pump's and junction's `Contents` are its fluid in m³ (`FluidStockOf`, as a buffer's); a pump's `PumpsInto` is what its `Connection1` is linked to (`GameClasses.PumpOutletPort`, `IsPump`).
- **Snapshot version 15:** `BuildableDocument.PumpsInto`, `SnapshotDocument.Pumps` (copied from the Catalog), `SnapshotDocument.Gases` (the gases it uses). A version 14 snapshot reads as before, without an estimate (`Pumps` is null).
- **Domain (`World.Domain/Pipes`):** `HeadLift.Reaches(pieces, links, outputs)`, with `PipePiece`, `PumpLift` and `FluidOutput`. It passes each reach along the pipe links; a reach only grows, so a piece is passed on again only when it grew. A pump passes fluid to its outlet only, at the higher of what came in and its own height plus head lift. `PipePiece.Headroom(reach)`: negative when short. A piece with `CarriesGas` gets no reach.
- **Map (`Map/PipeHeadLifts`):** builds the pieces from the snapshot (a pipe's top is its highest curve point; a pump's, junction's or buffer's where it stands), the fluid outputs from the connections out of machines, extractors and stations into a pipe (at the pipe's end nearer the building), and gives `MapConveyor.HeadLiftToSpare`, in metres to a tenth. A network carries gas when any of its pieces holds one at save time (`CarryingGas`); an empty network counts as a liquid.
- **Analysis (`Analysis/PipeFlows`):** `AnalysisResponse.PipeFlows`, per pipe that's drawn: its own flow in the model, or (`OfNetwork`) what its network takes in, the sum of what flows into the pipes and buffers feeding its hub; with the pipe's capacity. Whole world only.
- **Web:**
  - map tooltip of a pipe: "In it (m³): Water × 92.66", "In the model: Water 60 of 300 m³/min" or "In the model: its network takes in …", "Head lift in the model: 5.7 m to spare" or "7 m short: fluid may not get across";
  - the same in the 3D Info panel;
  - a pipe short of head lift is **orange**, on the map (thicker) and in 3D, with a legend line in both. `describePipeFlow`, `describeHeadLift`, `shortOfHeadLift` in `map-model.ts`; `tubeColour` in `area-model.ts`.

### Technical choices to overrule

- **A pipe's saved fluid stays out of the model.** Belts' contents tell an empty station's cargo (BR-A9, `Logistics.Carrying`); pipes' could too, but that would change the model's results unasked. `BuildableDocument.ToDomain` leaves them out.
- **A pipe inside a network shows its network's intake**, said as such, since the model has no flow per inner pipe (BR-A6).
- **Pumps whose direction isn't known** (linked to nothing on `Connection1`) pass fluid both ways without lifting it. **Valves** pass both ways.
- **Stations** feeding a pipe count as fluid outputs, with 10 m: "every fluid output".
- **Orange** for a pipe short of head lift; red stays for bottlenecks and loose ends.

## What it found

On the owner's save (re-imported after re-importing the Docs file): all 6,482 pipes have contents (5,324 hold something).
- **Before the gas rule:** 1,143 of 6,076 pipes short, 744 by over 100 m. The worst: rocket fuel from two blenders at −31 and −64 m up to 15 fuel generators at 255 to 342 m, through 322 pipe pieces without a pump. That runs in the game: gas needs no head lift.
- **With it:** 13 of 4,155 pipes short; 9 of them empty, each 18.5 m short: refineries stacked at 43 to 351 m on Pure Iron Ingot (iron ore and water in, ingots out), whose fluid output makes nothing.
- **Counting only ports that put out fluid:** 4 of 3,278 short.
  - An empty pipe 11.5 m above what a Mk.2 pump at −3 m lifts crude oil to (47 m), from an oil extractor to a particle accelerator on Oil-Based Diamonds. Empty in the save, which fits.
  - 3 water pipes, 2.7 to 3.9 m short, past Mk.2 pumps, holding about 11 m³ each in the save.
- **With `mMaxPressure`** (tried, not kept): 1 of 3,278 short, the empty crude oil pipe, by 6.5 m.

## Fix on the way: a 3D area with only belts and pipes

Found by the owner on the Learning world: a 3D area with pipes but no buildings showed nothing. Since step 90 the camera frames an area's buildings only, so a belt running on out of a factory doesn't pull it away; with no buildings it never moved. Now such an area is framed at its belts and pipes where they lie in it (`framing.ts`, `pointsToFrame`, tested on its own: three.js can't run in the tests). Checked in headless Chrome on the owner's area. A separate commit, `fix(web)`.

## Also on the way (asked 2026-11-24, each its own commit)

- **The sidebar starts collapsed on a touch screen** (`(hover: none)`), where the open tab lies over the page; a tap on something in the page opens it, as before. `SidebarTabs.collapsed`.
- **The snapshot picker's arrow stands at its right edge:** Material's label span takes the room between the icons (`::ng-deep .mdc-button__label`, `snapshot-picker.scss`). Measured in headless Chrome: the button spans 904 to 1232 px, the arrow 1197 to 1215.
- **The rectangle drawn with ctrl on the map is blue**, the one with shift stays yellow, so it says which page opens: the arrangement or 3D. `dragColours` in `map-model.ts`. The select button's rectangle on a touch screen stays yellow: there the page is chosen after drawing.
- **Power is an output while choosing a factory's machines:** the preview's headings are *Output* and *Input*, and with generators among the machines, *Output* starts with the power they make now, as the items show what leaves now (the owner chose it over showing both now and at 100%, as the factory's page does). `FactoryPreview.Power`, from `SnapshotAnalysis.PowerOf` (now `internal`, shared with the factory page).

## Kept for later

The community Pipeline Manual (v1.4, 2022) confirms the rules above, and adds more: a 12% head-lift tolerance (`mMaxPressure`), unpowered pumps, buffers' head lift, valves, and design rules such as feeding machines from above. Read, discussed and put on the maybe pile: [possible enhancements, Pipes](../../domain/possible-enhancements.md#fluid-behaviour-and-pipe-design-checks-from-the-pipeline-manual).

## .NET points

- **Throw helpers** (`ArgumentOutOfRangeException.ThrowIfNegativeOrZero`) in `Pump.Create`: Guava's `Preconditions`, with the argument's name filled in by the compiler.
- **`Assert.Equal(double, double, precision)`**: `Math.Round(…, 1)` still gives a `double` like 5.7000000000000002.
- **EF Core migration** `AddPumps`: a new table and three columns for the import counts, generated from the model (`dotnet ef migrations add`), like a generated Flyway script.
- **`is { } outlet && lifts.TryGetValue(…, out decimal lift)`**: a property pattern and an `out` variable in one condition, both in scope in the `?:` after it.

## Tests

- Catalog: `PumpTests` (3), the translator reads 20 and 50 m (`mDesignPressure`) and leaves the valve out, the import counts pumps.
- `HeadLiftTests` (12): a fluid output's 10 m, along pipes, a pump's lift, no stacking, the higher reach past a pump, no flow back through a pump, a pump of unknown direction, two outputs, no output, headroom.
- Save reader: a pipe's fluid (92.66 m³ in the fixture), a pump's outlet, a pipe pushes into nothing.
- World: the map's contents and head lift of the fixture's water pipe (5.7 m to spare), none on belts, none in a version 14 snapshot; the analysis's pipe flows (60 of 300 m³/min own; 300 for a pipe past the fixture's pump, of its network).
- Fluid outputs: the Generators fixture's turbofuel packager lifts the pipes it feeds while unpackaging turbofuel, and not on Packaged Water, which makes no fluid.
- Gas: the translator reads the five gases and no liquid or solid as one; `HeadLiftTests` a pipe carrying gas has no reach; the map gives no head lift to the fixture's water pipe when the Catalog says water is a gas.
- 1,223 .NET tests and 381 web tests pass (exit code 0); lint, the format check and the build pass.
- On the owner's save in headless Chrome: the tooltip of a pipe past the converter reads "In it (m³): Excited Photonic Matter × 5.3 · In the model: its network takes in Excited Photonic Matter 46 m³/min · Head lift in the model: 7 m short: fluid may not get across".
