# ADR-0028: Routing belts without crossings, as orders that nest

- **Status:** Proposed
- **Date:** 2026-11-18
- **Builds on:** [ADR-0027](0027-optimal-arrangement-with-cp-sat-in-the-analysis-service.md) (the optimal arrangement, computed in the analysis service).

## Context
The owner wants the belts between arranged factories laid out like a circuit board (BR-O2): at right angles, on a floor of their own below the factories, **never crossing seen from above**. Belts may stack, up to the ceiling, but **a stack keeps its order**: two belts that share a stretch may not leave it swapped. Lifts take their square. Routing comes after placing; one belt per link. On the owner's area: 48 factories, 67 belts, a floor of about 140 by 150 squares of one foundation.

"No crossing" isn't about where belts are: two belts may share every square of a stretch, so they cross or not only by how they enter and leave it. A router has to keep track of that order.

## Decision
- **Squares of one foundation**, each holding one stack of at most ⌊ceiling ÷ 2 m⌋ belts. Plots and roads are whole foundations, so factories line up with squares.
- **Each side between two squares keeps the belts across it in order.** A belt through a square joins two of its sides. Going round the square, every belt through it shows twice, and the belts must **nest like brackets** (`a b b a`, never `a b a b`). That is exactly "no crossing, and a stack keeps its order". A lift shows only once, so nothing nests with it, and no belt can pass it.
- **A\* over (square, side it came in by, place in that side's order)**, so the search picks the gap between existing belts where a new one fits, and is only as fine as the belts already there. The costs: a square of belt first, a turn second (one square outweighs every turn). Every free square under the source factory is a start, every free square under the target an end; the ends become its lifts.
- **Spots and the floor's edge.** A belt to a node's extractor, station or drone port ends on a free square of its spot. For a spot outside the area, every free square on the floor's edge is a possible end, charged the distance still to go from there to the spot; the A\* estimate is the distance to the spot itself, so it stays admissible. The belt is marked as leaving the floor.
- **One belt at a time, links shortest first**: a belt that finds no way is reported with the rest of its link's.
- **Rip-up and reroute by reordering** (added 2026-11-18, the owner asked for it): when belts are left over, every belt is taken up and laid again, the links that failed first, the most often failed foremost; at most 8 tries, the one with the fewest left over kept. Nothing about the search changes, only the order.
- **Crossings straight over straight** (added 2026-11-18, the owner's rule): a belt may cross another where both run straight through the square, at right angles, on different levels. The bracket check became pair by pair: a new belt may interleave with another only then. Without it, a belt that had to get past another went round its lift, the only way past a belt without crossing, which drew curls; on the owner's area 6 belts were left over and 51 of 124 turned back on themselves. With it, none left over and 32 of 130.
- **Levels** (added 2026-11-18, the owner's rule): a belt keeps its level from turn to turn, and changes it where it turns, a ramp for 2 m, a lift for 4 m or more. The order on a side stays what it was, seen from above; the level is kept apart from it, per square. Using the order as the height was tried first and failed: a belt joining a stack from the south had to go under the belts there, and with the first belt on level 0 nothing fit. The way is searched first with only a free level asked of each square, then the levels picked along it (the cheapest over its runs); only when none fit is it searched level by level. Searching every level everywhere took 8½ minutes on the owner's area instead of about 10 seconds.
- **Pipes on the same floor** (added 2026-11-18): routed by the same search, at the same 2 m step; a square's stack is of one kind, so a pipe passes only squares that are empty or hold pipes, and a belt only those empty or holding belts.
- **Own code, in `World.Domain`** (`BeltRouter`), with no library. It runs in the analysis service right after CP-SAT places the factories (`ArrangementDocuments.Solve`), and the belts go back with the proposal.

## Alternatives considered
- **A fine grid** where each square becomes as many lanes as it holds belts, with lanes that never touch: the same rule, but 16 × 16 times more cells (4 million for 1 km), and lanes would look like belts side by side rather than stacked.
- **CP-SAT for routing too** (a flow per belt with crossing constraints): optimal for small floors, but the non-crossing rule over shared stretches is hard to state as constraints, and the model would grow with every square.
- **Negotiated congestion (PathFinder, from chip design)**: routes all belts again and again with rising penalties for sharing. It handles *capacity*, not *order*, and a belt's place in an order depends on the others laid before it.
- **Rip-up and reroute** when a belt fails: done by reordering whole tries (see Decision). The finer kind, taking up only the belts in a failed one's way and laying them again around it, wasn't chosen: it needs belts taken out of the sides' orders and a way back when the reroute fails, for a gain not yet shown.

## Consequences
- Routing is fast (well under a second on the owner's area) and deterministic.
- Greedy: which links go first decides who goes round whom; a different order might route a belt this one reports.
- Heights aren't assigned: a stack's order is known per side, not which belt is on which level. Containers on the floor and shared buses aren't modelled (BR-O2).
- A belt that would pass the same square twice is refused rather than checked against itself; none was found.
- With nodes, stations and drone ports, the owner's area has about 230 belts; greedy routing leaves 41 of them unrouted (2026-11-18), mostly into drone ports and from freight platforms with many belts. Rip-up and reroute, or more room for lifts at a spot, are the next levers. With merging (stations and drone ports too), pipes apart and rip-up and reroute: 7 of 130 left over (run `01a1eed8`, 2026-11-18), about 8 s of routing.
