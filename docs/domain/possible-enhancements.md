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
