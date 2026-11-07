# Step 24 · What drones burn

**Goal:** a drone port's fuel at a real rate, instead of "whatever the line's other consumers leave" (BR-A8, step 14). A fuel line shared with other ports or generators is then analysed with real numbers, and a port short of fuel shows up.

## What you decided

- **The rate is the game's own figure** (from the latest trip): trip energy ÷ fuel energy × 60 ÷ the latest trip's duration. A drone that sat docked waiting has a long last trip, so a low rate, as the port screen shows it.
- **A port with a known rate takes its fuel like a generator**: an ordinary consumer, up to that rate.
- **A drone refuels at its home port if fuel is in that slot, otherwise at the port it's paired with.** A hub's rate is the sum over its drones.
- **Drones only.** Truck stations keep the old rule.

All in BR-A16 and [ADR-0022](../../adr/0022-drone-fuel-from-the-latest-trip.md).

## What the big save showed (the research)

A file-based C# app (`dotnet run drones.cs`) on `Clean Stop.sav`:

1. **The trip cost is a formula, measured flat.** The Docs file's drone port has `mTripPowerCost = 24000` and `mTripPowerPerMeterCost = 6`. Every drone's `mCurrentTripPowerCost` matches 24,000 + 6 × 2 × the *horizontal* distance between its ports: 63,641 against 63,640, 74,341 against 74,340. With height it's 0.2% off, so the game leaves it out.
2. **Every home port has a latest trip** (`FGDroneStationInfo.mLatestDroneTrips`), all 16 of them. Step 14 counted 5; I didn't dig into why that differs.
3. **10 of the 16 home ports have an empty fuel slot**, and every one is paired with a port holding rocket fuel. Port …4764680 refuels three drones. So the fuel often goes in at the far end, which led to the third decision.
4. **Drones fly with partial loads** (one trip carried 469 items, 4.69 of 9 stacks). That ruled out computing trips from the cargo moved.

Rates in your world come out between 0.03 and 2.27 rocket fuel a minute per drone. The 0.03 is the drone whose last trip took 22,145 s.

## How it works

```
Catalog        DronePort entry: trip cost (24,000 MJ + 6 MJ/m), and the energy of every solid fuel
SaveFile       a home port's route: the paired port, the flat distance, the latest trip's seconds
World.Domain   DroneRoute.FuelPerMinute, DroneRefuelling.BurnRates (where each drone refuels, summed per port)
               Provider/Consumer.BurnRate → FlowUnknownSource/FlowSink.BurnRate
               SteadyState: fuel at a known rate is accepted firmly (shared over its belts) and asked for upstream
World          snapshot version 8: routes on home ports, the Catalog's drone port; burn rates when building the graph
               findings: DronePorts (what the drones burn, what the belts bring), least supplied first
Simulation     the flow document carries BurnRate to the analysis service
web            a "Drone ports" tab: fuel, drones burn, belts bring (with a bar), how much is short
```

**The solver change in two parts.** In `TakeEverything`, a fuel belt into a port with a known rate gets a firm `_accept` (no `_spare`). Several fuel belts share the rate like merger inputs. Then in `AskedItems`, a port with a known rate *asks* for its fuel, as a machine does. Without that, a container that nothing feeds (BR-A9) never sent the fuel. The findings test caught this: the hub got 0.

## Files

| Where | What |
|---|---|
| `Catalog.Domain/DronePort.cs` | New entry: `DronePort`, `DroneFuel`, `DronePortId` |
| `Catalog/Importer/DronePortGroupDto.cs`, `GameDataGroupDto.cs` | The `FGBuildableDroneStation` section |
| `Catalog/Translator/SatisfactoryTranslator.cs` | `TranslateToDronePorts`: the trip cost; every solid item with energy as a fuel |
| `Catalog/Persistence/…`, `Migrations/…_AddDronePorts.cs` | `drone_ports` table with fuels as jsonb; import counts |
| `Catalog.Contracts/ICatalogQueries.cs`, `Queries/CatalogQueries.cs` | `GetDronePortsAsync`, `CatalogDronePort`, `CatalogDroneFuel` |
| `World.SaveFile/DroneRoutes.cs`, `SaveFileReader.cs`, `SavedWorld.cs`, `GameClasses.cs` | `SavedDroneRoute` on a home port |
| `World.Domain/DroneRoute.cs`, `DroneRefuelling.cs` | The formula and the refuelling rule |
| `World.Domain/Consumer.cs`, `Provider.cs`, `Flow/FlowNetwork.cs`, `Flow/WorldFlow.cs`, `Flow/SteadyState.cs` | `BurnRate` through to the solver |
| `World/Snapshots/SnapshotDocument.cs`, `ImportSave.cs` | Version 8, `DroneDocument`, `DronePortDocument`, `DroneBurnRates` |
| `World/Analysis/SnapshotAnalysis.cs` | `DronePortFinding` |
| `World.Simulation/FlowNetworkDocument.cs` | `BurnRate` on source and sink documents |
| `web/src/app/world/findings.*` | The drone ports tab |
| `web/src/app/catalog/catalog-page.ts` | The import result shows drone ports too (missed at first: you saw no change after importing) |

## .NET points worth noticing

| .NET / C# | Java / Spring (TypeScript) | Here |
|---|---|---|
| `(decimal)someFloat` keeps 7 significant digits | `new BigDecimal(f)` gives the exact binary value | `DroneRoutes.Of` rounds on purpose to two decimals. In the primer. |
| `Assert.Equal` on a tuple with a `Dictionary` inside compares the dictionary by reference | A Java record with a `Map` compares contents | `DroneSnapshotTests` asserts the fuels apart. In the primer. |
| A local function that captures nothing must be `static` (IDE0062, an error here) | Lambdas, capture implicit | `DronePortFindingsTests`. In the primer. |
| Property patterns in `switch`: `case FlowSink { BurnRate: { } rate, Burns: { Count: > 0 } fuels }:` | Java 21 has record patterns and `when` guards, but no property patterns like `Count: > 0` | `SteadyState.AskedItems` |
| Optional trailing record parameters (`DronePorts = null`) keep old call sites compiling | Overloaded constructors or a builder | `CatalogCopy`, `SnapshotDocument`, `FlowSink` |
| A file-based app with `#:project ../X.csproj` | jshell can't load a Maven module this easily | The research scripts, against the real save reader |

## Tests (one behaviour each)

| Where | Tests |
|---|---|
| `DronePortTests` (Catalog domain) | Knows the energy of each fuel · doesn't know what is no fuel · a fuel has energy · a trip costs energy · a newer file replaces trip cost and fuels |
| `SatisfactoryTranslatorTests`, `CatalogImportTests`, `CatalogQueriesTests` | 24,000 MJ and 6 per metre from the file · every solid fuel at its energy · an import counts drone ports · found by class name |
| `DroneRouteTests` | Fixed energy plus every metre there and back · a trip's worth as often as the latest trip · a long latest trip burns little · side by side still pays the fixed cost · a trip takes time · a fuel has energy |
| `DroneRefuellingTests` | Refuels at home when fuelled · otherwise at the pair · fuel at both: home · a hub burns for all its drones · no fuel anywhere: nothing · a drone without a trip makes its port unknown · unknown fuel energy: unknown |
| `DroneRoutesTests` (SaveFile) | Measured flat in metres · the latest trip as recorded · several trips averaged · no paired port: no route · no trip yet: a route without trip time |
| `SteadyStateTests` | Takes no more fuel than it burns · shares the line with the others · a loading port too · fuel over two belts makes up the rate · an unknown source sends a port the fuel it burns · but not for an unknown rate |
| `WorldFlowTests`, `FlowNetworkDocumentTests` | The burn rate reaches the flow node, and survives the document; none stays none |
| `DroneSnapshotTests` | Copies the trip cost and the fuels its ports hold · a port with fuel burns its drone's rate · an empty home burns at the hub · no trip yet: unknown · no Catalog drone port: unknown · keeps each route |
| `DronePortFindingsTests` | Every refuelling port with its fuel · fed by a belt gets it all · nothing feeds it gets none · worst first |
| `SnapshotDocumentTests` | Version 8; a version 7 snapshot still reads |
| Web: `findings.spec.ts` | Lists a drone port with what it burns, what reaches it, and how much is short |

Red first: the four solver tests failed on the old solver (20 instead of 10 and so on), and the "unknown source sends the fuel" test failed before `AskedItems` changed. Totals: 710 .NET tests and 35 web tests, all green.

## Choices worth a second look

1. **No fixture with a flying drone.** None of your small saves has two paired drone ports. The reader's route is tested on the save's values directly, and the snapshot and findings tests build a small world in memory. *If you want one:* in the Development world, build a second drone port, pair them, let a drone fly once with fuel in one slot, and save as `Drones.sav`. I'd then add reader tests on it.
2. **Every solid item with energy counts as a drone fuel.** The Docs file has no list of drone fuels ("can use any fuel type"). The fuel in the slot decides anyway. The wiki lists seven drone fuels; I didn't check whether the game accepts others.
3. **The fuel is what's in the refuelling port's slot**, not the drone's `mActiveFuelName`. They agree in your world. One drone has no active fuel at all; its pair holds rocket fuel.
4. **Analysis version stays 4.** Only snapshots with routes use the new rule, and those are all new.
5. **What a port gets** is read from what flows into the belts ending at it. Ports are always fed by belts, so that's what arrives.
6. **The latest trip is one sample**, as in the game. Averaging is on the enhancements pile, but the save keeps just one.

## Then: from a finding to the map

You asked for this to check whether starved machines really are starved. **Where** in every findings table (machines, bottlenecks, unused supply, unknown supply, generators, drone ports, on the snapshot page and a factory's page) is now a link to the snapshot's map with `?focus=<buildable id>`. The map zooms to that buildable, rings it in yellow, and opens its tooltip (status, contents, position).

- **By id, not by coordinates:** the map finds the exact machine or belt, so its tooltip can open. A belt is found halfway between its ends (`locate` in `map-model.ts`).
- **Same tab:** the browser's back button returns to the findings. *Choice to overrule:* open the map in a new tab instead.
- **Files:** `map/map-model.ts` (`locate`), `map/map-page.ts` (`focus` input, `focusOnBuildable`), `findings.ts` / `.html` (`snapshotId` input, the links), `snapshot-page.html`, `factory-page.html`.
- **Tests:** `map-model.spec.ts`: finds a machine where it stands · providers, logistics and consumers too · a belt halfway along · nothing for an unknown id. `findings.spec.ts`: links where a machine stands to it on the snapshot's map. The Leaflet part (zoom, ring, tooltip) isn't unit-tested; check it in the browser.

## Next

To see it in the app (the AppHost is running), do two imports. First the **Docs file**, so the Catalog gets the drone port (the migration runs on start). Then your **big save** again, because a snapshot copies the Catalog at import. Your "Desert Start Cleaned" snapshot from 18:08 was imported before the Docs file had the drone port, so it shows no drone ports. Then the *Drone ports* tab lists the ports where drones refuel, the least supplied first.
