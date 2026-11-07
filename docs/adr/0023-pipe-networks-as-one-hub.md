# ADR-0023: A pipe network is one hub in the analysis

- **Status:** Accepted
- **Date:** 2026-11-07

## Context
Pipes have no direction of their own in the save. The save reader guessed one per pipe link: away from where fluid enters, breadth first (step 12). A link with both ends equally far from a source got no direction, and so no connection.

In the big world that dropped **654 of 10,034 pipe links** (6.5%), mostly at junctions. The owner's quantum encoders showed as output blocked on Dark Matter Residue because their residue pipes were cut where the flow from two encoders met.

Guessing better doesn't work, because which way fluid flows through a stretch of pipe depends on where the demand is:
- **Away from sources** breaks a *collection* manifold (producers along a pipe, a consumer at the end): between two producers both ends point inwards.
- **Towards machines** breaks a *distribution* manifold (one water pipe feeding a row of machines): between two machines both ends point outwards.

The game itself treats each connected pipe network as one unit (`mPipeNetworkID`).

## Decision
- **Each pipe network is one hub** (decided 2026-11-07). The save reader makes a hub buildable per connected set of pipe links: kind `PipeNetwork`, class `PipeNetwork`, id `PipeNetwork:<its pipe that sorts first>`, with no position.
- **Into the hub:** each pipe a machine output or extractor feeds sends into the hub. **Out of it:** the hub sends into each pipe that feeds a machine input. Those pipes stay in the flow, so their capacity still limits each link. A pipe that both is fed and feeds an input sends into the hub, and the input gets its share from the hub.
- **In the flow** the hub is a splitter that also merges (BR-A6): it takes evenly from its inputs and shares evenly over its outputs.
- **The pipes inside** a network stay in the world (the map draws them, with their contents) but have no connections.
- **Fluid buffers:** in a network something feeds, a buffer passes nothing (it's full in steady state). In a network nothing else feeds, it feeds the hub, and supplies what is asked (BR-A9).
- **Counts** of a snapshot leave hubs out: they're not built by the player.
- Snapshot version 9; a version 8 snapshot keeps its guessed directions.

## Alternatives considered
- **Better guessing**: point ties towards the machines instead of dropping them. That fixes the residue case found, and keeps pipe bottlenecks, but collection or distribution manifolds still come out wrong somewhere.
- **Pipes in both directions** in the solver: every splitter would send half back where it came from.

## Consequences
- No pipe link is lost any more. In the big world the hub change, together with running on stock (BR-A17), raised running machines from 1,623 to 1,792.
- **A bottleneck inside a network** (one thin pipe in the middle) isn't found any more. Only the pipes at the network's edge can show as bottlenecks.
- Factory tracing (BR-W*) walks through the hub like any logistics, so a factory's members reach across a pipe network as a whole.
