# Step 14 · Flow analysis, part 2c: stations, stock and fuel

**Goal:** the stations of trains, drones and trucks in the world, with what they hold, so the analysis stops treating every line a train unloads onto as empty. Then the slowdown on your big world that came with them.
**Result:**
- **Stations:** every platform, drone port and truck station is in the world, as a source or a consumer, as its ports say. Stations and containers carry their stock, and a drone port or truck station takes its fuel only from what's left.
- **Two solver flaws found:** the big world showed both. With them fixed, it runs in **7 s** instead of 165 s, and only the known iron-wire part stays approximate. Starved machines went from 1,290 (before stations) to 854.
- **Tests:** 490, all green.

**Decisions you made:**
- the ports decide whether a station unloads or loads;
- fuel is taken from what's left, because the save doesn't say how much a station burns (option 2). Computing it is on the enhancements pile;
- build the "spare" mechanism now (A), and then investigate the slowdown.

**Decisions I made, for you to overrule:** see "Choices worth a second look".
**Spikes:** two throwaway tests on a copy of your big save, deleted afterwards. Their findings are in "The spike" below.

## What was built

```
World.SaveFile   GameClasses: stations (role from linked ports), stock and fuel inventories per class
                 SaveFileReader.StockOf: inventory stacks → item, count
                 SavedBuildable.Stock, .Burns
World.Domain     Provider(offer, stock, burns) · Consumer(burns) · Logistics(stock)
World.Domain.Flow
                 FlowUnknownSource.Burns, FlowSink.Burns
                 SteadyState: spare next to accept (fuel takes what's left)
                              a machine shares an item over its belts like a merger
                              an unknown source offers what its belts can carry, and sends in
                              proportion to what's asked
World            SnapshotDocument version 3: stock and fuel per buildable; version 2 still read
Fixture          Development.sav with your stations: two freight platforms, a drone port, two truck stations
```

## How it works

**The adapter** reads a station's ports:
- belts leaving its outputs make it **unload**: a `SavedProvider`, holding what's in the inventory it unloads from (`inventory`, or `OutputInventory` for a drone port);
- belts into its inputs only make it **load**: a `SavedConsumer`.

The fuel in a drone port's or truck station's `FuelInventory` is what it **burns**. Storage containers get their stock from `StorageInventory`.

**In the analysis:**
- a station that unloads is a source whose rate isn't known (BR-A9), confirmed when its stock lasts 30 minutes;
- it takes everything loaded onto it, except its fuel: that's *spare*. It's filled only from what the line's other consumers leave (BR-A8, README section 2c).

## The slowdown, and two flaws it showed

With the stations in, your big world took **165 s** and 2 large parts didn't settle. Two throwaway probes showed why. Both flaws were in the solver, not the world:

1. **Two inputs from two stations waited for each other.**
   - A blender takes casings only as far as heavy frames are offered, and frames only as far as casings are offered.
   - A source of unknown rate offered only what was accepted, so both stayed at 0.
   - Now it offers **what its belts can carry**, as BR-A9 always said. 19 silent stations started delivering.
2. **Several belts bringing the same item flip-flopped.**
   - A machine accepted from each belt "its demand minus what the other belts brought".
   - Both belts read last sweep's numbers, so they swapped every sweep. That was the 15,328-buildable part: manufacturers fed quartz over several belts.
   - Now a machine **shares an item over its belts like a merger**: evenly, by what each offers. That part settles in 21 sweeps.

Along the way:
- a source without stock now divides what it sends **in proportion to what the machines downstream ask**, not evenly over the items;
- **damping** (moving acceptance only halfway each sweep) was tried and dropped: it didn't help, and it hid the second flaw.

| Big world | Before stations | Stations, before the fixes | Now |
|---|---|---|---|
| Unsupported stations | 280 | 0 | 0 |
| Parts | 1,016 | 802 | 802 |
| Not settled | 1 | 3 | **1** (the iron-wire part) |
| Simulation | 3 s | 165 s | **7 s** |
| Starved / output blocked / at 100% | 1,290 / 563 / 571 | 1,138 / 450 / 836 | **854 / 703 / 867** |
| Sources of unknown rate delivering | – | 38 | 57 |

Still silent: 84 sources. 75 of them reach only sinks, other stations or nothing (a platform unloading into a buffer container at the end of its line: nothing downstream asks, so that's right). 9 reach machines: 6 without stock, 3 with.

## Tests (one behaviour each)

| Where | Tests |
|---|---|
| `SaveFileReaderTests` | A platform with a belt on its output unloads: a provider · on its input only: a consumer · an unloading platform holds what's on it · a drone port with belts on both sides holds what drones brought, not what it sends or its fuel · a truck station unloading holds its cargo, not its fuel · a truck station that only loads is a consumer · a drone port and a loading truck station burn what's in their fuel slot · an empty fuel slot burns nothing the save can tell · a freight platform burns nothing · containers hold their stock, an empty one nothing · a station is connected to its belts |
| `GameClassesTests` | Platforms, drone ports and truck stations are stations · stock comes from the inventory that holds what can be supplied |
| `WorldFlowTests` | A station that unloads supplies what's asked and knows its stock · a container nothing feeds knows its stock · stations that unload or only load know what they burn |
| `SteadyStateTests` | A drone port takes everything its input brings and supplies its output · takes only the fuel the others leave · a loading truck station too · cargo is taken like by any consumer · a machine whose fuel only goes to a drone port still runs · two belts bringing the same item share it, and settle · a machine with each input from its own station is supplied fully, and settles · an unknown source offers what its belt can carry |
| `SnapshotDocumentTests`, `TestWorldFlowTests` | Stock and fuel are kept · a version 2 snapshot still reads · the test world's platforms and drone port reach the analysis |

**The tests can fail:**
- with station inputs accepting nothing, exactly the drone port test failed;
- before each fix, its tests were red: the two fuel tests, the two-station test (0 instead of 100%), and the two tests that didn't settle.

## .NET points worth noticing (all now in the primer)

| .NET | Java/Spring (TypeScript) | Note |
|---|---|---|
| `is InventoryItem { ItemType.Length: > 0 } item` | Record patterns, without property paths | Extended property patterns: a nested property and a comparison in one pattern. |
| `dynamic` in the spikes | (reflection) | Checked at runtime, like a JavaScript object. Fine in a throwaway, not in product code. |
| `Func<TestFlow, TestFlow>` as a test parameter | `UnaryOperator<TestFlow>` | `FuelLine(flow => flow.StationBurning(…))` builds the shared part once and lets each test add its station. |

## Choices worth a second look

1. **What a station burns comes from what's in its fuel slot now.** An empty slot means the save can't tell, and its inputs take everything.
2. **A drone port that ships rocket fuel and burns it too:** all its rocket fuel counts as fuel, so it takes only what's left.
3. **A station whose ports have no belts** isn't in the world (5 of your freight platforms).
4. **Fluid truck stations** (`Build_FluidTruckStation_C`) aren't modelled: there's none in either save to check against. One that's connected is reported as unsupported. *(Done in step 15.)*
5. **Fluid stock** wasn't found, so fluid platforms and buffers are always *assumed* supplied. *(Found in step 15: litres in platforms and fluid truck stations, `mFluidBox` in buffers.)*

## Next

- **2b, ports:** smart-splitter rules, priority mergers, and machines with two solid products. Your world has 212 smart and programmable splitters, treated as plain splitters so far. A mixed belt they sort clogs every machine it reaches, so that's likely much of the remaining 854 starved.
- **Then part 3:** findings over the API.

## The spike

**Goal:** what does the save say about train platforms, drone ports and truck stations: which way they work, and what they hold? And what do storage containers hold (BR-A9)?
**Result:** which way a platform works is clearest from its **ports**, not from its mode flag. Stock is in inventory stacks: item and count. Fluids are the exception.

### What's in the save

| Building | Class | In your world | Item ports | Stock |
|---|---|---|---|---|
| Freight platform | `Build_TrainDockingStation_C` | 216 | `Input0/1`, `Output0/1` | component `inventory`: stacks of item + count, e.g. 4,800 coal |
| Fluid freight platform | `Build_TrainDockingStationLiquid_C` | 36 | `PipeFactoryInput0/1`, `PipeFactoryOutput0/1` | `inventory`, but empty on all 36 |
| Drone port | `Build_DroneStation_C` | 28 | `Input0/1`, `Output0/1` | `InputInventory` (to send), `OutputInventory` (received), `FuelInventory` |
| Truck station | `Build_TruckStation_C` | 0 | (not checked) | (not checked) |
| Train station | `Build_TrainStation_C` | 63 | none: the platforms carry the items | – |
| Storage container | `Build_StorageContainerMk1/Mk2_C` | many | `Input0`, `Output0` | component `StorageInventory`, e.g. 957 aluminium sheets |
| Fluid buffer | `Build_PipeStorageTank_C`, `Build_IndustrialTank_C` | some | pipe ends | no inventory component |

An inventory stack, as the library reads it: `InventoryItem.ItemType` is the item's path (`…/Desc_OreGold.Desc_OreGold_C`), and its `NumItems` is the count. An empty slot has no item type and 0.

### Which way a platform works

| Freight platforms | Count |
|---|---|
| Belts on its **outputs** only (it unloads) | 91 |
| Belts on its **inputs** only (it loads) | 120 |
| No belts | 5 |
| `mIsInLoadMode` stored, as 0 | 67 |

The mode flag and the ports don't agree: 91 platforms unload onto belts, but only 67 store a mode. The save leaves out values that equal the default, and it isn't clear what the default is. So **the ports decide**:
- a platform with belts on its outputs unloads: a **source**, BR-W1's providers;
- a platform with belts on its inputs loads: a **consumer**, BR-W8.

That's also what matters for the flow: a platform in load mode with belts on its outputs would send nothing over them, and there's none of those in your world.

**Drone ports do both at once.** 8 of yours have belts on inputs and outputs: they send what comes in and unload what drones bring. Fluid platforms: 24 unload, 12 load.

### Fuel for drones (and trucks)

Drones and trucks burn fuel, so a port or station that's fed fuel is a consumer of it too. How much does a drone port burn?

| What | Where in the save | In your world |
|---|---|---|
| Fuel type | the drone (`BP_DroneTransport_C`): `mActiveFuelName` | 15 of 16 drones |
| Energy cost of its trip | the drone: `mCurrentTripPowerCost`, e.g. 63,640 MJ | 11 of 16, only while on a trip |
| Trip durations | the port's `FGDroneStationInfo`: `mLatestDroneTrips` (duration, items in and out) | 5 ports, one trip each |
| Energy per fuel item | the game's Docs file, `mEnergyValue`: packaged rocket fuel 7,200 MJ, battery 6,000 MJ, packaged ionized fuel 10,000 MJ | not imported by the Catalog yet |

Only the port a drone lives at (`mHomeStation`) burns fuel: 16 of your 28. Fuel per minute = (trip cost ÷ fuel energy) × (60 ÷ trip duration) can be computed for **5** of them:

| Port | Prediction |
|---|---|
| Rocket Fuel – Aluminium Ingots | 1.62 /min |
| Rocket Fuel – Aluminium – Producer | 2.23 /min |
| Rocket Fuel – Plastic & Rubber – Producer | 2.11 /min |
| Rocket Fuel – Main Factory – Producer | 0.41 /min |
| Rocket Fuel – Nuclear – Producer | 0.03 /min, from one trip of 22,145 s, probably stalled |

The game computes what it shows live, from the route. The save only keeps the current trip. **Decided (2026-11-05):** a station's fuel is a consumer whose rate isn't known (option 2). Computing it is on the [enhancements pile](../../domain/possible-enhancements.md).

### What the spike meant for the build

- A station that unloads becomes a **source whose rate isn't known** (BR-A9). Its stock is what's on the platform now: freight platforms from `inventory`, drone ports from `OutputInventory`. Confirmed at 30 minutes.
- A station that loads becomes a **consumer**. A drone port with both takes what its inputs bring and supplies its outputs.
- A storage container that nothing feeds gets its stock from `StorageInventory`: in the new test world, 1,561 screws in the container that feeds truck station B. (The packager's canister container, 751 in step 12, is empty now.)
- **Fluid stock wasn't found.** Fluid platforms and fluid buffers stay *assumed supplied*, without stock minutes.
- **Truck stations:** none in your world, so nothing to verify against.
