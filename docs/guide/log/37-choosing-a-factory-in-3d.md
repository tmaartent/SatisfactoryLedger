# Step 37 · Choosing a factory in 3D

**Goal:** part 2b(1) of the factory workflow, as you chose it on 2026-11-10: select a factory's machines in the 3D view, with a click to toggle one, a shift-drag box to add and an alt-drag box to take out, then save it by name. The old output-belt dialog goes. Parts (2) a live preview of the traced factory and (3) the factory page follow.

## What you decided (2026-11-11, after the first draft)

- **A machine belongs to one factory at most.** This supersedes BR-W5 ("a machine can be shared"). Defining a factory with a machine another factory has is refused.
- **Overlaps stored before** the rule are only reported: the 3D view shows them.
- **Factories are outlined in 3D:** the edges of each factory's machines in its own colour, with a legend. A machine of another factory can't be chosen.
- **Generators can be among a factory's machines** (BR-W2), with the **power** they make (MW now and at 100%) beside its items. Folded into this step. Sinks, uploaders and stations stay outside any factory.
- **Not the output item's colour.** The Docs file has a colour only for fluids and gases (`mFluidColor`); every solid has the default white there, and its in-game look is in its icon texture. You kept one colour per factory, so neighbouring factories always differ.

## How it works

```
Snapshot page   "Define a factory" → the map → "Open an area in 3D" → drag a rectangle
3D view         "Define a factory"   (a mode: outside it a click still only shows a building)
                  click a machine          → toggle(chosen, id)
                  shift-drag a box         → boxSelect(chosen, machines on screen, box, 'add')
                  alt-drag a box           → boxSelect(…, 'remove')
                  name + Save              → POST /factories { world, name, machines } → the factory's page
```

- **Only machines** can be chosen (`kind === 'machine'`), since a factory is its machines (BR-W2). A click on a belt or a splitter still shows it in the panel.
- **A box takes a machine when its middle lies inside the box on screen.** The middle is the centre of its boxes' bounding box, projected through the camera. Machines hidden by the height cut-off aren't counted, so you can pick one floor at a time. One behind the camera is skipped.
- **Chosen machines are blue.** Outside the mode, the one looked at glows yellow; inside it nothing glows, so a click turns a machine blue and a second click takes it out again (your review: a glow on the one clicked, blue only once you clicked the next, was confusing). The panel then lists how to choose (click, shift-drag, alt-drag) and what can't be chosen (another factory's machines; sinks, storage containers and stations) and names only the machine or generator clicked (a click on a belt, splitter or container names nothing), with the factory it's already in, if any; its recipe, status and contents are left out. `paint()` replaces `highlight()`: each mesh keeps its own status colour in `userData.material`, so painting is one pass with no state to undo.
- **Factories already defined** (`GET …/factories` for this snapshot) are outlined: their machines get edges in the factory's colour (`EdgesGeometry` as a child of the machine's mesh, so the height cut-off hides it too). The body keeps its status colour. The panel lists the factories in the area, with links, and a machine's card names its factory. A machine in two factories, from before the rule, names both and says so.
- **On the map too**, every factory's machines are always edged in its colour (`machineOwners`, the same as in 3D), and the *Highlight a factory* list shows each colour. Highlighting one zooms to it, dims the rest and draws its edges bold. `factoryColour` gives a factory's colour by id.
- **From a factory's page** to its map and 3D view: *Map* opens `…/map?factory=<id>`, highlighted and zoomed to; *3D* opens `…/area?factory=<id>`, the area around its machines and generators (`areaAround`, 15 m past the outermost ones).
- **The factories among the findings:** the snapshot's *Factories* tab moved into its findings, after *Machines*, as a table: each factory (its colour, a link to its page), how many machines, what it makes and what it needs from outside (the items its outputs and inputs carry). The *Machines* and *Generators* tabs gain a *Factory* column, linked. A factory's own page leaves both out (`FindingsView.factories` not given).
- **While defining**, another factory's machine isn't toggled by a click, and a box leaves it out.
- **One machine, one factory** (BR-W5) is checked by the API: `ClaimedMachines.Among(chosen, latest, the world's other factories)` matches each factory's stored machines in the latest snapshot (`MachineMatch`, so a rebuilt machine still counts as its factory's) and returns the chosen ones already taken. `DefineFactoryHandler` answers 409 `World.MachinesTaken`, with a `taken` list of `{ id, factory }`. It runs after the name check, for both the machine request and the old output-belt request.
- **Generators** (BR-W2): the domain's `Consumer` gets `IsGenerator`, set at import for the classes the Catalog knows as generators. `FactoryDefinition.CanBeChosen` accepts a machine or a generator. In the trace, a chosen generator takes in its fuel and water and makes its waste, so its fuel line is the factory's own and its waste belt is an output. A generator *not* chosen stays an end, as before. `SnapshotDocument.IsMachineOrGenerator` lets the define check, `MachineMatch` and the map accept them. A factory's findings list its generators and its `power`: each generator's MW at its clock speed at 100%, times its utilisation now. The map marks a consumer `isGenerator`, so the 3D view can choose it. The factory page shows the power, and its findings a Generators tab when it has any.
- **Saving** posts to the world of the snapshot you're looking at. The API checks the ids against the world's **latest** snapshot (BR-W11). Choosing in an older snapshot gives a 422 listing the machines it doesn't find, shown under the name field.

## Files

| Where | What |
|---|---|
| `web/src/app/world/area/selection.ts` (new) | `toggle`, `rectBetween`, `boxSelect`: what a click and a box do, without three.js |
| `web/src/app/world/area/factory-outlines.ts` (new) | `machineOwners`, `factoriesInArea`, `factoryColour`, `outlineColours`: who owns which machine, and each factory's colour, the same in every area and on the map |
| `web/src/app/world/area/area-model.ts` | `areaAround`: the area around a factory's machines |
| `web/src/app/world/factory-page.html` | *Map* and *3D* links |
| `web/src/app/world/findings.*`, `snapshot-page.*` | The *Factories* tab among the findings; a *Factory* column for machines and generators; the snapshot's own Factories tab is gone |
| `web/src/app/world/map/map-page.*` | Factory edges in their colour, bold when highlighted; swatches in the list; the button keeps its label and the drawing hint sits on the map, so the header doesn't shift |
| `World/Factories/ClaimedMachines.cs`, `TakenMachine.cs` (new) | The chosen machines another factory already has |
| `World/Factories/DefineFactoryHandler.cs`, `DefineFactoryOutcome.cs`, `FactoryEndpoints.cs` | `MachinesTaken` → 409 `World.MachinesTaken` |
| `World.Domain/Consumer.cs`, `FactoryDefinition.cs` | `IsGenerator`; `CanBeChosen`; what a member makes and takes: a machine its recipe, a generator its waste, fuel and water |
| `World/Snapshots/SnapshotDocument.cs`, `BuildableDocument.cs` | `IsMachineOrGenerator`; `IsGenerator` set from the Catalog's generators |
| `World/Factories/MachineMatch.cs`, `DefineFactoryHandler.cs`, `FactoryEndpoints.cs` | Generators can be matched and chosen; `World.NotMachines` now says "machines or generators" |
| `World/Analysis/SnapshotAnalysis.cs`, `FactoryAnalysisResponse.cs`, `FactoryPower.cs` (new) | A factory's generator findings, its power, and its generators' waste at 100% |
| `World/Map/MapBuilding.cs`, `WorldMap.cs` | `IsGenerator` |
| `web/src/app/world/factory-page.*`, `findings.*`, `shared/display.ts` | Power on the factory page (`megawatts`); the Generators tab only when there are any |
| `web/src/app/world/area/area-page.ts` | The mode, `chosen`, the name, `save()`; `listenForBoxes`, `machinesOnScreen`, `paint` |
| `area-page.html`, `area-page.scss` | The button, the dragged box, the *New factory* section in the panel, the hint per mode |
| `web/src/app/world/snapshot-page.*` | "Define a factory" links to the map; the dialog is gone |
| `web/src/app/world/define-factory-dialog.*` | Deleted |
| ADR-0025, BR-W5, BR-W12, glossary, possible enhancements | One factory per machine; the dialog is gone, the old request stays in the API |

## Tests

Written first (`selection.spec.ts`): a click adds a machine not selected yet · takes out one that is · leaves the selection it started from as it was · a box is the same whichever corner it starts from · adds every machine inside it, edges included, with shift · takes them out with alt.

`factory-outlines.spec.ts`: a machine's owners are the factories it's in · none for a machine no factory has · all of them for a machine stored in two, from before the rule · the factories shown are those with a machine in the area · a factory keeps its colour in every area · colours repeat past eight factories · a factory highlighted on the map has the colour its machines are outlined in, in 3D · none for a factory the world doesn't have. `area-model.spec.ts`: the area around a factory's machines reaches a margin past the outermost ones · is nothing when none are in the snapshot.

`ClaimedMachinesTests` (World.Tests): a machine another factory has is taken by that factory · one no factory has is free · one rebuilt within a metre is still taken by the factory of the one it replaced. `WorldFactoryApiTests`: a machine of one factory can't be in another (409 `World.MachinesTaken`, naming the machine and *Iron plates*).

Generators, domain (`FactoryDefinitionTests`, around a test nuclear plant fed by a rod maker and a pump): a generator can be one of its machines · a factory of generators only can be traced · the fuel line to its own generator is its own and no output · the waste its generator leaves is an output · its generator asks for its fuel and water from outside · a generator not chosen stays the end of the line, the fuel into it an output. Application (`GeneratorFactoryTests`, the fixture saved with a nuclear plant): traced with the plant as its machine · its generators are its generator findings · 2,500 MW now and at 100% · the waste (10/min) is its output · a factory without generators makes no power. `WorldMapTests`: a generator says it is one. `WorldFactoryApiTests`: a generator can be one of a factory's machines (201). `display.spec.ts`: power in MW.

Two of my first expectations were wrong and the model right: a fuel belt that also touches a machine of no factory isn't the factory's own (BR-W3), so the input is the plant ← that belt, not the rod maker; and an input lists everything its machines ask for that the factory doesn't make, so the water input lists the rods too (the step 27 rough edge).

The three.js part (projection, pointer events) has no unit test; it needs a browser. **Not tried in the running app yet:** please try a click, a shift-drag and an alt-drag on your world.

**Totals:** 866 .NET tests, 76 web tests.

## Points worth noticing

| | Java / TypeScript | Here |
|---|---|---|
| A signal holding a `Set` gets a new `Set` on every change | React state: the same rule. A Java bean would fire a property change on `add` | `toggle` and `boxSelect` return a new set; `chosen` is a `ReadonlySet`, so `chosen().add()` doesn't compile. In the primer |
| A capture-phase listener runs before the target's own | A servlet `Filter` before the controller | OrbitControls pans on shift+left-drag (three 0.187.0, `onPointerDown` checks `enabled` first). The stage switches it off on the way down. In the primer |
| `setPointerCapture` | Swing's drag events keep coming to the component that got the press | The box keeps following the pointer when it leaves the stage, and the canvas's click handler doesn't see the release |
| A closed set of outcomes gains a case | A sealed interface with records, and a `switch` that must cover them | `DefineFactoryOutcome.MachinesTaken`: the private constructor keeps the set closed, but C# doesn't check the endpoint's `switch` for completeness the way Java 21 does for a sealed interface, hence its `_ => throw` arm |
| A cross-aggregate rule in the application layer | A uniqueness check in a Spring `@Service` before `save` | `ClaimedMachines` beside the name check: no single `FactoryDefinition` knows the others |
| Pure logic split from the rendering | A Spring service tested without the controller | `selection.ts` beside `area-model.ts`; the page only wires three.js to it |

## Choices worth a second look

1. **A mode to define.** Outside it, a click only shows a building, as before; inside it, a click also toggles. Without a mode, every look at a machine would change the selection.
2. **The middle of a machine decides**, not any part of it. A machine half inside the box isn't taken when its middle lies outside.
3. **The old define request stays in the API** (`outputBelt` + `inputItems`, `GET …/output-belts`, BR-W12). Nothing in the web app uses it any more. Removing it, with `OutputBeltSuggestions`, is a separate step if you want it gone. `OutputBeltTrace` stays either way, for converting old factories.
4. **One machine, one factory, checked only on define.** There's no editing yet; when there is, the same check applies. Checking in the latest snapshot means a machine taken in an older snapshot but gone in the latest one counts as free.
5. **The first factory's colour wins** on a machine in two (old) factories; the card names both.
6. **Eight colours**, repeating past eight factories in a world; the legend tells two factories with one colour apart.
7. **Generators keep the name *machines*** in the API, the stored factory and the domain (`FactoryDefinition.Machines`); the glossary says generators are included. Renaming to *members* everywhere would touch the stored jsonb and the contract.
8. **A generator not chosen stays an end**, so a fuel line from factory A's refinery to factory B's generators is A's own, an output of A, and an input of B. A generator can't tell "another factory's" from "nobody's" inside one factory's trace.
9. **Power used** by a factory's machines isn't counted; only power made.
10. **The map's factory edges** don't include generators yet: the map draws generators in its consumer layer, which doesn't know factories.
11. **No pre-filled name.** The dialog suggested what the belt carried; there's nothing like that before the preview of part (2).
12. **Edge widths on the map:** 1 px for a machine of no factory (its status colour), 2 px in a factory's colour, 3.5 px for the highlighted factory. The fill stays the status colour, so a machine's state still shows.
13. **15 m around a factory** in its 3D view, so the belts in and out show; the map's own drawn rectangle is unchanged. `?factory=` is read by the map with `linkedSignal`, so the list can still choose another factory.
14. **The Factories tab waits for the analysis**, like the other findings: while a snapshot is still being analysed (about ten seconds) or when its analysis failed, the factories don't show. *Makes* lists items by name, not rates; rates are on the factory's page.

## Next

Part 2b(2): a live preview of the traced factory (outputs, inputs, machines that count as its own) while choosing, which needs a trace-without-storing endpoint.
