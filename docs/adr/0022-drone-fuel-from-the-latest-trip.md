# ADR-0022: A drone port burns its drones' fuel at the rate of their latest trips

- **Status:** Accepted
- **Date:** 2026-11-07

## Context
Since step 14, a drone port took the fuel it burns only from what the line's other consumers leave (BR-A8, option 2), because the save doesn't say how much a drone burns. A fuel line shared with generators or other ports was therefore analysed without real numbers.

Two things in the big world made a rate possible:

- **The trip cost is a formula.** The Docs file gives the drone port `mTripPowerCost = 24000` MJ and `mTripPowerPerMeterCost = 6` MJ. A drone's `mCurrentTripPowerCost` in the save matches 24,000 + 6 × twice the **flat** distance between its two ports, to the megajoule, for every drone that was on a trip (63,641 against 63,640; 74,341 against 74,340). Height is left out.
- **The save keeps each port's latest trip** (`FGDroneStationInfo.mLatestDroneTrips`, one sample: `mTripInformationSampleCount = 1`). All 16 home ports in the big world have one.

The big world also showed that **10 of the 16 home ports have an empty fuel slot**, and every one of them is paired with a port that has rocket fuel in its slot. A drone "refuels at any of its connected Ports" (Docs file). Some of those ports are hubs that refuel several drones.

## Decision
- **Fuel per minute = trip energy ÷ the fuel's energy × 60 ÷ the latest trip's duration** (decided 2026-11-07). That's the game's own figure on the port screen. A drone that sat docked waiting has a long latest trip, and so a low rate.
- **A drone refuels at its home port when fuel is in that port's slot, otherwise at its paired port** when fuel is in that one's (decided 2026-11-07). A port where several drones refuel burns for all of them.
- **A port with a known rate takes its fuel like a generator** (decided 2026-11-07): an ordinary consumer, up to that rate, and it asks for it like a machine, so a source whose rate isn't known sends it. Without a known rate it takes from what's left, as before.
- **Unknown stays unknown:** a port with a drone that has made no trip yet, or a fuel the Catalog has no energy for, keeps the old rule.
- **Drones only** (decided 2026-11-07). Truck stations keep the old rule.
- **The Catalog** keeps a *drone port* entry: the trip cost, and per fuel a drone can burn (every solid item with energy: a drone "can use any fuel type") its energy per item.
- **The save reader** gives each home port its route: the paired port, the flat distance to it, and the latest trip's duration.

## Alternatives considered
- **Flying non-stop:** round trip = 4 × 51 s of take-off and landing + 2 × distance ÷ the fuel's speed. That's an upper bound that works without a recorded trip, but it counts a drone that stays docked because its cargo can't be unloaded.
- **From the cargo moved:** trips = items the flow sends ÷ what a drone carries. That ties the fuel to the solver's own result, but the solver doesn't link a port to its pair, and drones fly with partial loads (one carried 4.69 of 9 stacks).
- **Splitting a drone's burn over both ports** when both hold fuel, or **always the home port** (as the wiki describes it). Always-home would leave the 10 drones that refuel at their pair unknown.

## Consequences
- Snapshot version 8 keeps each home port's route and the Catalog's drone port. Older snapshots treat drone ports as before.
- Analysis version stays 4: the new rule only applies to snapshots that carry routes, and those are new.
- One sample is jumpy: a drone that waited for hours shows a tiny rate. The game shows the same. Averaging needs more samples than the save has (enhancements pile).
- Without a fixture that has a drone flying between two ports, the save reader's route is tested on the save's values directly, and checked by hand on the big world.
