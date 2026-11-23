# ADR-0027: The optimal arrangement, solved with CP-SAT in the analysis service

- **Status:** Accepted
- **Date:** 2026-11-12
- **Builds on:** [ADR-0018](0018-analysis-as-a-separate-service-over-rabbitmq.md) (the analysis service and its messaging), [ADR-0026](0026-steady-state-as-a-sequence-of-linear-programmes.md) (OR-Tools in `World.Simulation`).

## Context
The owner wants a proposal for where the world's factories would best stand, so that the belt work between them and their inputs (other factories, stations, resource nodes) is least (BR-O1). It's a facility layout problem: rectangles that mustn't overlap, placed to minimise weighted grid distances. On the owner's world: 72 factories, 500 fixed places, 436 links. Making the problem takes World about 20 seconds; a good layout takes a solver a minute or more.

## Decision
- **Solver:** OR-Tools' constraint solver **CP-SAT** (already a dependency, ADR-0026), in `World.Simulation/ArrangementSolver`. `NoOverlap2D` keeps factory rectangles (widened by the gaps) off each other and off blocking places; absolute values give the grid distances; the objective is belts × distance plus the weighted spread of the arranged factories (the width and depth of the rectangle around them, kept by four bounding variables), then, to break ties, how far factories move. A time limit per run; the result says whether it's optimal or the best found.
- **Domain:** the problem and its cost in `World.Domain/Arrangement` (no libraries), in grid cells.
- **Where it runs:** in the **analysis service**, as the flow analysis does (ADR-0018). The owner chose this over running it in the API.
  1. `POST /snapshots/{id}/arrangements` stores a pending run and publishes `ArrangementRequested` through the transactional outbox, to the queue `world.arrangements-to-compute`.
  2. The service fetches the problem from `GET /internal/world/arrangements/{id}/problem`; World makes it from the snapshot, its world's factories and the snapshot's analysis, and keeps it with the grid it was made on.
  3. The service solves it and sends the proposal back (`PUT …/proposal`, or `…/failure`).
- **Storage:** every run is kept (the owner chose) until the player deletes it (added 2026-11-18; a proposal that comes back for a deleted run gets a 404, which the service already takes as gone), in `world.arrangements`: settings, status, the problem and the proposal as jsonb. A run goes with its snapshot.
- **The service's limits, raised for this job:** Wolverine cancels a handler after 60 seconds by default (`DefaultExecutionTimeout`), and the service defaults give every HTTP client a standard resilience handler with a 10-second attempt timeout. An arrangement takes its solver's time plus half a minute to fetch the problem, so the service's handler timeout is 15 minutes, and its World client's handler is replaced (`RemoveAllResilienceHandlers`, marked for evaluation, EXTEXP0001) with one of 2-minute attempts and 5 minutes in all.

## Alternatives considered
- **In the API, in the background or while waiting:** less code, but OR-Tools and minutes of CPU in the API process; the owner chose the service.
- **Simulated annealing or a force-directed layout by hand:** no library, but no proof of quality, and overlap handling is ours to get right. CP-SAT does both.
- **Shrinking the problem under 10 seconds** instead of raising the client's limits: fragile as the world grows.

## Consequences
- Two messages, two queues, one service. Arrangements and analyses don't wait on each other: each queue is handled one message at a time.
- A run whose message is lost stays pending; there's no requeue at startup as there is for analyses (ADR-0017).
- The proposal depends on the snapshot's analysis for its weights: a run can only be requested once that's ready.

## Amendment 2026-11-23: opened from the map with ctrl and drag
The findings page, the arrangement's only way in, is gone (step 81). Now a drag with **ctrl** held on the world map opens the arrangement with that area to arrange already drawn (decided by the owner, step 83), as a shift-drag opens the area in 3D. The area comes in the address (`?x0=…&y0=…&x1=…&y1=…`, centimetres); given one, the page shows no earlier run, so none of their areas lies over it, and a run can be made at once. Its settings are the page's defaults; picking an earlier run brings its settings, as before.

## Amendment 2026-11-23: keys to draw, settings in the sidebar
Decided by the owner (step 85), as on the world map:
- **Shift and drag draws the area** to arrange, **ctrl and drag inside it keeps a part out** (⌘ on a Mac). The *Draw the area* and *Keep out* buttons are gone; above the map a line says which key does what, and how many parts are kept out.
- **The settings stay on the page,** above the runs, with *Arrange* and *Reset* above the map (a Settings tab was tried and undone the same day).
- **A factory is picked by clicking it on the map** (decided by the owner, step 87), before a run and with one: one at a time. Clicking a factory picks it alone, letting go of the one picked before, even when it was lit up as that one's supplier; clicking it again lets go. The run's 3D belt floor picks alike. *A Factories tab, listing them with how far each moves, was there the same day before; it went.*
- **Before a run, each building is the ground it covers**, in its factory's colour, as on the world map, instead of a dot; a station, miner, extractor or sink too. One function draws a building on both maps (`buildingShape`).
- **Before a run, picking a factory shows what supplies it** (decided by the owner, step 86), each factory a whole: the factory and every factory that feeds it light up, all their machines, in the picked factory's colour (a factory's own is one of eight, often shared, so suppliers lit in their own couldn't be told apart), the rest grey; the existing belts and pipes between them are drawn as built, with the splitters on them and the miners, extractors, freight platforms and drone ports where they start, in the picked factory's colour; the rest fades. Suppliers only: not what it feeds, nor a factory fed from the same splitter. What supplies it is `FactoryDefinition.SuppliesIn`, upstream only, returned by the factory preview as `supplies`: the sources and the pieces between them and it, its own lines from miners included (it owns those when nothing else uses them), not its belts between its own machines. Each line between says what it carries: the icons of its items in a pill at its middle. *Tried first the same day: every belt drawn, a picked factory's lit up (too realistic, and the related factories didn't light up); the belt work per link as a run counts it (hard to read); and a walk both ways, which lit up factories fed from the same splitters and the one it feeds. All undone.*
- **No switches:** where the factories stand now is never drawn over a run, and the belts and pipes on the floor below always are.
- **No snapshot picker above the sidebar:** the arrangement is about the snapshot it was opened from.
- **The map is held to the map picture**, as the world map is (ADR-0020).
