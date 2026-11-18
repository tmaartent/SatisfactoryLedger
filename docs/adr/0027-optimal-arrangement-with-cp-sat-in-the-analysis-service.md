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
- **Storage:** every run is kept (the owner chose), in `world.arrangements`: settings, status, the problem and the proposal as jsonb. A run goes with its snapshot.
- **The service's limits, raised for this job:** Wolverine cancels a handler after 60 seconds by default (`DefaultExecutionTimeout`), and the service defaults give every HTTP client a standard resilience handler with a 10-second attempt timeout. An arrangement takes its solver's time plus half a minute to fetch the problem, so the service's handler timeout is 15 minutes, and its World client's handler is replaced (`RemoveAllResilienceHandlers`, marked for evaluation, EXTEXP0001) with one of 2-minute attempts and 5 minutes in all.

## Alternatives considered
- **In the API, in the background or while waiting:** less code, but OR-Tools and minutes of CPU in the API process; the owner chose the service.
- **Simulated annealing or a force-directed layout by hand:** no library, but no proof of quality, and overlap handling is ours to get right. CP-SAT does both.
- **Shrinking the problem under 10 seconds** instead of raising the client's limits: fragile as the world grows.

## Consequences
- Two messages, two queues, one service. Arrangements and analyses don't wait on each other: each queue is handled one message at a time.
- A run whose message is lost stays pending; there's no requeue at startup as there is for analyses (ADR-0017).
- The proposal depends on the snapshot's analysis for its weights: a run can only be requested once that's ready.
