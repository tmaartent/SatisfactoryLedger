# Step 107 · Factories on their footprints

**Goal:** one plot per factory (decided 2026-11-18) works for factories built up on several floors, but not for factories laid out flat. Those don't fit a 6 × 6 plot (asked 2026-11-25). The owner chose to try each factory on its own footprint, with the roads staggered between factories of different sizes. Straight roads in rows went on the enhancement pile. Branch `feat/arrangement-touch`, after step 106.

## What you decided (2026-11-25)

- **A choice per run: Plots or Footprints.** Plots stays the default, and older runs keep plots.
- **On footprints, a factory takes the whole foundations its machines cover**: the rectangle around their footprints, rounded out to foundations. It's moved, never turned.
- **The roads are the gaps.** A road's width (E–W and N–S, as set) stays free beside each factory, so the roads stagger rather than run in rows and columns.
- **Option B, rows of varying depth with straight roads one way**, is on the pile: [possible enhancements, *Factories on their footprints in rows, with straight roads*](../../domain/possible-enhancements.md#factories-on-their-footprints-in-rows-with-straight-roads).

### Choices to overrule

- **Only the machines count** toward a footprint, not the factory's own belts or walkways. That's the same as the step 47 decision ("around its machines only"), after the factory's own belts blew Rocket Fuel up to 1.8 × 1.5 km.
- **The plot sizes are kept but ignored on footprints.** The page hides the two Plot fields when Footprints is picked. The API still takes them, so a run can switch back.
- **A road is kept only on a factory's east and north side** against another factory, the way the solver already widens rectangles. Between two factories that's always at least one road. Against the area's edge there may be none.

## How it works

The solver didn't change. `ArrangementSolver` already packs rectangles of any size with `NoOverlap2D`, each widened by `GapX`/`GapY` on its far sides. On plots, both gaps were 0 because the road was inside the cell. Only the World module's grid changes:

| | Plots | Footprints |
|---|---|---|
| A cell (`ArrangementGrid`) | a plot + its road (10 × 10 foundations by default) | one foundation (8 m), no road |
| A factory | one plot, where its machines' middle is (`PlotOf`) | the foundations its machines cover (`PlotsTouching`) |
| The roads | inside each cell | the problem's gaps (`ArrangementSettings.Gaps`, in foundations) |
| Belt floor (`ArrangementSettings.Floor`) | squares per plot, road squares | one square a cell, no road squares |
| Refused when | fewer free plots than factories | the factories cover more foundations than are free |

The belt router works per foundation anyway (ADR-0028), so it routes on footprints unchanged. A factory's lifts start on the free squares under it, which are now exactly its footprint. The map and 3D already drew a proposed factory's machines in the middle of its rectangle (step 49), and on footprints they fill it.

## On your Learning world, in the model

Your one run (the area, supply lines, Fast, roads of 4 foundations), made again on footprints:

| | Plots (6 × 6) | Footprints |
|---|---|---|
| Factories | 20 | 20 |
| Belts and pipes, together | 5,480 m | **5,296 m** |
| Unrouted · crossings · levels used | 0 · – · 3 | 0 · 0 · 3 |
| Spread of the proposal | 448 × 448 m | 584 × 560 m |
| The largest factory | 48 × 48 m (every one) | Wire, Cables, Copper Sheets **40 × 416 m**; Wire, Iron Plate, Iron Rods 16 × 456 m; Concrete Below 128 × 56 m |
| Took | 28 s | 16 s |

The belt work isn't comparable between the two: it's counted in cells, which are plots in one and foundations in the other (the footprint run says 1,472 → 878.5, −40%; the plot run 146.75 → 75.25, −49%). The metres of belt are comparable. The spread is larger because the long, flat factories now take their real length instead of 48 m. Both runs stopped at the best found in time, not a proven optimum.

## Files

| Where | What |
|---|---|
| `World/Arrangement/ArrangementSettings.cs` | `Footprints`; `Floor` and `Gaps` for each choice |
| `World/Arrangement/ArrangementGrid.cs` | On footprints, a cell is one foundation with no road |
| `World/Arrangement/WorldArrangement.cs` | A factory's rectangle and the gaps per choice |
| `World/Arrangement/StoredArrangement.cs`, `ArrangementRequest.cs`, `ArrangementSummary.cs`, `ArrangementEndpoints.cs` | The choice asked for, kept with the run and returned |
| `World/Arrangement/ArrangementProblems.cs` | The reason for no room, in foundations on footprints |
| `World/Persistence/Migrations/*_ArrangementFootprints.cs` (new) | `footprints` on `world.arrangements`, `false` for older runs |
| `World.Domain/Arrangement/BeltFloor.cs` | `FoundationMetres` and `Foundations` public, for the grid and the gaps |
| `World.Domain/Arrangement/ArrangementProblem.cs` | `FreePlots` renamed `FreeCells`: a cell is a plot or a foundation |
| `api/openapi.json` | `footprints` on the request and the summary |
| `web/src/app/world/arrangement/arrangement-page.{ts,html}` | *Plots / Footprints* in the settings; plot fields hidden on footprints; taken from a picked run |
| `web/src/app/world/arrangement/arrangement-model.ts` | A run's line says "footprints" instead of the plot size |
| BR-O1, ADR-0027 (amendment), glossary (*Footprints (arrangement)*), possible enhancements | The rule, the decision, the term, option B |

## .NET and C# points

| Here | Java / Spring | TypeScript |
|---|---|---|
| **A new last record parameter with a default** (`bool Footprints = false` on `ArrangementSettings`, `bool? Footprints = null` on `ArrangementRequest`): every existing `new ArrangementSettings(…)` in tests still compiles, and a request without the field binds to `null` (primer, *.NET basics*, *Optional parameters on a positional record*). | Java records have no default values: you'd add a second constructor. | An optional property `footprints?: boolean`. |
| **`public const` is copied into the caller** (`BeltFloor.FoundationMetres`, used from another assembly): see primer, *.NET basics*, *A `public const` is copied into the caller*. | The same as a `static final` compile-time constant. | No equivalent: an `export const` is a live binding. |
| **A tuple-valued property, deconstructed** (`(int gapX, int gapY) = settings.Gaps;`) instead of a small `Gaps` record. | You'd write a record `Gaps(int x, int y)`. | `const [gapX, gapY] = settings.gaps`, with a tuple type. |
| **`dotnet ef migrations add ArrangementFootprints`** generated `AddColumn<bool>(…, nullable: false, defaultValue: false)` from a non-nullable `bool` property, so existing runs read as plots. | A Flyway script by hand, `ADD COLUMN footprints boolean NOT NULL DEFAULT false`. | – |
| **`<mat-button-toggle [value]="false">`** with `[(ngModel)]` on a boolean signal: the brackets bind the value as an expression, a boolean. Without them `value="false"` would be the string `"false"`, which is truthy. | – | The same pitfall as any attribute vs property binding. |

## Tests

Written before the code. They weren't run on their own before it, so there's no red run to show: they couldn't have compiled without `Footprints` on the settings and the summary.

- **World** (`WorldArrangementTests`, test save): on footprints a factory takes the whole foundations its machines cover (the cell is 8 m, the rectangle holds smelters A and B and constructor C, 21 m apart, in more than one foundation) · the roads are the gaps between factories in foundations (8 m and 16 m make 1 and 2) · the belt floor is one square a foundation, with no roads of its own.
- **Domain** (`ArrangementProblemTests`): the room tests renamed to cells (*factories have no room when there are fewer free cells than they take*).
- **API** (`WorldArrangementApiTests`, through RabbitMQ and the service): on footprints the factories are arranged on the foundations their machines cover, kept with the run, every belt routed.
- **Web** (`arrangement-model.spec.ts`): a run on footprints says "footprints" instead of a plot size.
- **Results:** 1,247 .NET tests pass (exit 0); 437 web tests pass (exit 0); lint, the format check and the e2e type check pass. Playwright wasn't run. Tried on the Learning world through the running stack (above).
