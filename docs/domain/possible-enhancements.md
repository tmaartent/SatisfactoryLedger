# Possible enhancements

Ideas the owner wants kept, but not planned yet. Each says what it is, why it's worth doing, and what it would take. When one is picked up, it gets business rules, an ADR where needed, and a step log.

## Flow analysis

### An exact steady state, as a linear programme
*Raised 2026-11-05, step 13.*

**What:** compute the steady state as one system of equations and inequalities, solved with a linear-programming solver, instead of sweeping until nothing changes (`SteadyState`). Flows per connection and item are the variables. Capacities, recipes (input and output in fixed ratios at a utilisation between 0 and 1), conservation at every node, and what providers offer are the constraints.

**Why:**
- It's exact, deterministic, and always ends. A degenerate part, where ore supply and downstream room bind at almost the same point, no longer drifts for thousands of sweeps (BR-A10: such parts are now reported as approximate).
- The same technique answers planning questions later: what's the most a factory can make, which belt to upgrade first, how to clock a line.
- The owner works with linear programming in energy demand and consumption forecasting.

**What it would take:**
- **The hard part:** the game's *even* splitting of splitters and mergers (BR-A4, A5) isn't a plain flow constraint. An optimum isn't unique, and an LP picks any corner. It needs extra constraints or a second objective, for example lexicographic max-min fairness, solved in rounds.
- **A solver library for .NET:** for example Google OR-Tools (`Google.OrTools`, with GLOP for LP), or HiGHS through its .NET bindings. That's a new dependency, so it needs an ADR.
- **Size:** a world like the owner's big one has about 45,000 connections and a few items each, so tens of thousands of variables. That's fine for an LP solver.
- **Testing:** the current sweep solver and its tests stay as the reference. Both can run on the same networks and are compared within the tolerance.

### Loops the iterative solver finds at zero (step 23)

The owner's nitric acid and non-fissile uranium blenders each need what the other makes. "Both at zero" is a steady state, and the sweeps, which start from zero, find it. Starting from the top (every connection offering plenty) found the loop running, but shifted results elsewhere: four inputs of one machine locked at the same 68%. A linear programme maximising flow finds the largest steady state directly, with no start point. That's one more reason for it.

### What a truck station burns
*Raised 2026-11-05, step 14. Drones done in step 24 (BR-A16, ADR-0022).*

**What:** compute the fuel a truck station's trucks burn per minute, as step 24 does for drones, instead of taking it from what's left (BR-A8).

**What it would take:**
- **The rate:** the Docs file gives a truck's fuel use only in MW on autopilot (`mAutopilotFuelConsumption`), so it depends on how long its recorded route takes, and how much of it is driven.
- **The route:** recorded per truck in the save. Not verified: the owner's big world has no truck stations; Development.sav has two.

### Drones that wait at a port
*Raised 2026-11-07, step 24.*

A drone's latest trip (BR-A16) is one sample: a drone that waited docked for hours shows a tiny rate, one that just made a fast trip after waiting a long time shows the full one. The game shows the same number, so it's honest, but a few trips averaged would be steadier. The game keeps one (`mTripInformationSampleCount = 1`), so the save can't give more.

### What a generator really burns

Generators are assumed to run at full power (ADR-0021). In the game they throttle down to what their power grid demands. The save has the power circuits; their demand would give each generator its real load, and so its real fuel burn and waste.
