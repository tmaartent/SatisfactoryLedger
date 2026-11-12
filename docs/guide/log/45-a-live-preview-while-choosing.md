# Step 45 · A live preview while choosing a factory

**Goal:** part 2b(2). While you choose a factory's machines in 3D, see what they'd make a factory before saving: the belts and pipes it would own, and its outputs and inputs. Branch: `main`, uncommitted.

## What you decided (2026-11-12)

- **The trace, not the numbers:** what tracing gives (owned logistics, outputs and inputs with their items), fast and without the analysis. *Now* and *At 100%* stay on the factory page.
- **Panel and 3D:** a summary in the define panel, and in 3D the logistics it would own highlighted with a marker where each line crosses.

After trying it (2026-11-12): the panel's *"0 belts, pipes and other logistics it would own"* said little and the line counts confused. A line is the factory's own only when every machine on it is chosen (BR-W3), so a selection of part of a factory, or of machines along a manifold that runs past others, owns none: on Cooling Systems, 4 of its 18 machines own 0, all 18 own 57. So the count is gone, and **per item the panel shows what leaves and arrives now, in items a minute**, from the snapshot's stored analysis (the same rules as the factory page: `FactoryOutputs.Over`, `FactoryInputs.Arriving`). The preview doesn't ask for an analysis; before it's stored, the items show without rates. The panel's headings say what the balls are: *Where items leave* (green) and *Where items come in* (orange).

## How it works

```
choose machines ─ 300 ms pause ─► POST /snapshots/{id}/factory-preview { machines }
                                   FactoryTraces.Preview: FactoryDefinition(chosen).TraceMembers(world, fluids)
                                   ◄─ { logistics: [ids], outputs: [...], inputs: [...] }   nothing stored
panel: Out, now: item 472.5/min … · In, now: item 2,585/min …   (from the stored analysis; no rates before it's ready)
3D:    owned belts and pipes drawn again, thicker, in teal · owned splitters, containers painted teal
       a green ball where an output crosses, an orange one where an input does
```

- **Same trace as a saved factory.** `Preview` uses the domain's `TraceMembers`, as `TraceAll` does; the two now share how connections are summarised (`Summaries`). On your world the preview of Cooling Systems' 18 machines, Ficsite Trigons' 96 and the Rocket Fuel Power Plant's 360 gives exactly their stored traces (57, 1,191 and 61 logistics; outputs and inputs alike).
- **A POST that stores nothing.** A selection can hold hundreds of ids, too many for a query string. The API test checks nothing is stored. Ids that aren't machines or generators are left out.
- **It waits for a pause.** The request goes 300 ms after the selection last changed (an effect with a timer, cleared on the next change), so a box dragged over a floor sends one request, not one per machine.
- **Belts in 3D are merged meshes** (per kind and metre of height, for speed), so one belt can't be recoloured. The preview draws the owned ones again in its own layer. Splitters, mergers and containers are meshes of their own and are painted.
- **Where a line crosses**, `handoverPoint` (tested): on a belt or pipe across, its end at the factory's side; on one of its own, its far end; otherwise where the building inside stands.

## Files

| Where | What |
|---|---|
| `World/Factories/FactoryTraces.cs` | `Preview`; `Summaries` shared with `TraceAll` |
| `World/Factories/FactoryPreview.cs`, `PreviewItem.cs`, `FactoryPreviewRequest.cs` (new) | The response (`Leaving`, `Arriving` per item) and request |
| `World/Analysis/FactoryOutputs.cs`, `FactoryInputs.cs` | `Over` internal; `Arriving` out of its local function, internal |
| `World/Factories/FactoryEndpoints.cs` | `POST /snapshots/{id}/factory-preview` |
| `web/src/app/world/area/factory-preview.ts` (new) | `handoverPoint` |
| `web/src/app/world/area/area-page.*` | The preview resource, its 3D layer, the panel summary |
| `api/openapi.json`, glossary | `PreviewFactory`; *Factory Preview* |

## Tests

Application (`FactoryTracesTests`, written first): a preview of chosen machines says which logistics they'd own and what they hand over · a preview of ids that are no machines owns and hands over nothing · a preview says per item what leaves and arrives now by the stored analysis (15 iron plates a minute) · before the analysis is ready, it names the items without rates. API (`WorldFactoryApiTests`): previewing returns what the factory would own and hand over, without storing it · a preview in an unknown snapshot is not found. Web (`factory-preview.spec.ts`, written first): an output from a machine onto a belt crosses where the belt starts · an input on a belt into a machine crosses where it ends · an output into its own container is at the container.

The 3D drawing has no test, as before (three.js needs a canvas). **Not tried in a browser yet.**

**Totals:** 890 .NET tests, 86 web tests (with step 46: 897 and 89).

## Points worth noticing

| | Java / TypeScript | Here |
|---|---|---|
| Debouncing a signal | RxJS `debounceTime` | `effect((onCleanup) => { const t = setTimeout(…); onCleanup(() => clearTimeout(t)); })`: the effect reruns on every change and cleans up the last timer first |
| A resource that only loads sometimes | An `if` before the call | `resource({ params: () => … ? {…} : undefined })`: `undefined` params leave it idle, with no value |

## Choices worth a second look

1. **About 1–2 seconds per preview** on your world: each request loads the snapshot (2.8 MB) and builds its graph. Fine after a pause; if it feels slow, the graph could be kept in memory per snapshot.
2. **Inside a pipe network, pipes aren't highlighted.** The model treats a connected pipe network as one hub, and only the pipes at its machine ends stay connected; the ones inside aren't in the graph. Those end pipes are highlighted, the rest not.
3. **The height cut-off** cuts the preview's belts and markers at the same roof as the other belts (the clipping plane), but doesn't hide them by floor as it hides buildings.

## Next

Part 2b(3): the factory page's outputs and inputs per connection, or something else you choose.
