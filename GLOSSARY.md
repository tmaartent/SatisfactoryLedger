# Satisfactory Ledger

The ledger reads a Satisfactory save file, models what flows through the world, and finds what holds its factories back. These terms are used identically in conversation, docs, code, API routes and the UI; a word the code needs that isn't here is added here first.

## Game Catalog

**Item**:
Anything that can be on a belt or in a pipe: ore, ingots, parts, fluids. Its form is *solid* or *fluid*.

**Raw Resource**:
An item extracted from the world: ores, limestone, coal, SAM, water, crude oil, nitrogen and the like. Every other item is *non-raw*.

**Recipe**:
The game's input amounts, output amounts and cycle time for one building type. It may be an *alternate* recipe.

**Building**:
A production machine type that runs a recipe: Smelter, Constructor, Assembler, Refinery and the like.

**Transport Tier**:
A belt or lift (Mk.1–Mk.6), or a pipe (Mk.1–Mk.2 and their *clean* variants), with its maximum throughput. A lift carries as much as the belt of its mark, a clean pipeline as much as the pipeline of its mark.

**Resource Node**:
A spot on the map where a resource can be extracted, with its **purity** and position, named as the game and the save name it (ADR-0015). A resource well's satellite nodes are resource nodes of their own kind.

**Purity**:
How rich a resource node is, multiplying an extractor's rate: impure ×0.5, normal ×1, pure ×2 (BR-W9).

**Extractor**:
A building that takes a raw resource out of the world: a miner Mk.1–Mk.3, an oil, water or resource well extractor. Its **extraction rate** is what it extracts per minute on a normal node at 100% clock speed.

**Icon**:
The game's own picture of an item, a resource or a building, shown beside its name and on the map (ADR-0032). The **icon set** is all of them at once; an import replaces it whole.

**Map Art**:
The game's own map picture under the world map: one rectangle of the world, cut into **tiles**; an import replaces it whole (ADR-0020).

**Catalog Entry**:
An item, building, transport tier, recipe or extractor taken from the game's Docs file, identified by the game's class name.

**Catalog Import**:
One import of the Docs file: the game version, when it happened, and per kind of entry how many were added, updated or marked no longer in the game (ADR-0009).

**Buildable** (class):
What the catalog knows of a kind of buildable: its class name and the name the game shows (a *Freight Platform*, a *Smart Splitter*). A **Building** is the narrower kind a recipe runs in.

**Fluid Colour**:
The colour the game gives a fluid or gas, as its pipes show it. Solids have none.

**Stack Size**:
How many of an item fit in one inventory slot: 1, 50, 100, 200 or 500. Fluids have none.

**Storage**:
A building that holds items, in slots or, for a fluid buffer, a volume in m³: storage containers, fluid buffers, freight platforms, truck stations, drone ports, the depot uploader.

**No Longer In Game**:
A catalog entry that a later import didn't contain. It stays in the catalog, so references to it stay valid.

**Rate**:
An amount of an item per minute, kept as an exact fraction. Fluids are in m³/min.
_Avoid_: ratio (the spreadsheet's workaround; rates follow from recipes)

## World

**World**:
Every snapshot with the same session name. Factory definitions belong to a world (BR-W11).

**World Snapshot**:
One imported save: when it was saved and imported, its world graph, and what the import couldn't place. Every snapshot is kept (BR-W10).

**World Graph**:
The world as read from one save file: its buildables and how they're connected. Never changed after import.

**Buildable**:
Anything built in the world that items move through: a machine, provider, consumer or piece of logistics, identified by its id in the save, which stays the same from save to save. The game's own term.

**Connection**:
Items moving from one buildable's output to another buildable's input.

**Upstream / Downstream**:
Upstream of a buildable is what feeds it; downstream is what it feeds.

**Port**:
Which output of a splitter a belt leaves from, or which input of a merger it enters by, counted from 0.

**Machine**:
A building in the world that runs a recipe, with its clock speed and somersloops.

**Provider**:
A building that brings items into the network from outside: an extractor, or a train, truck or drone station. Never part of a factory; it can supply several (BR-W1).

**Consumer**:
A building that takes items out of the network: an AWESOME Sink, the dimensional depot uploader, a generator, a station that loads. Never part of a factory (BR-W8), except a generator chosen as one of its machines (BR-W2).

**Generator**:
A consumer that makes power by burning a fuel: coal, fuel, biomass or nuclear (ADR-0021). With a known fuel it takes its **burn rate**, water if it needs any, and leaves its **byproduct** (BR-A8); as one of a factory's machines, its **power** counts as the factory's (BR-A15).

**Burn rate**:
How much fuel a generator burns a minute at full power: its power ÷ the fuel's energy, × 60. For a drone port, what the drones refuelling there burn a minute, from their latest trips (BR-A16).

**Station**:
A train platform, fluid platform, drone port or truck station. Belts leaving its outputs make it unload, as a provider of unknown rate; belts into its inputs make it load, as a consumer (BR-W1, BR-W8).

**Logistics**:
Everything that moves items between buildings without changing them: belts, lifts, pipes, splitters, mergers, storage containers, fluid buffers. A belt, lift or pipe has a **capacity** from its tier.

**Solids / Fluids**:
What a line can carry: belts, lifts, splitters, mergers and containers carry solids; pipes, junctions and fluid buffers carry fluids (BR-A3).

**Pipe network**:
Every pipe, junction, pump, valve and fluid buffer connected by pipe, treated as one hub that takes from what feeds it and shares over what it feeds (BR-A6, ADR-0023).

**Curve**:
How a belt or pipe runs as built: the points the save keeps for it, with the direction it leaves and arrives at each.

**Footprint**:
The ground a building covers seen from above: a rectangle from the game's clearance boxes, turned the way the building faces.

**Loose End**:
An end of a belt, lift or pipe connected to nothing in the save, so items can't go on, or come in, there. An end linked to a building the ledger doesn't read counts as connected.

**Sort Rule**:
What one output of a smart or programmable splitter sends: an item, *any*, *any undefined*, *overflow* or *none* (BR-A4).

**Stock**:
What a station or storage container holds right now, per item, as the save stores it (BR-A9).

**Contents**:
What's on a belt or lift, in a splitter's or merger's buffer, or in a machine at save time. Unlike stock, never counted as supply.

**Inventory**:
What something at the end of a line holds, and how much fits: slots, a fluid volume or the dimensional depot. It takes an item until full, then as fast as the save shows that item **arriving** (BR-A7).

**Cargo**:
What an empty station is taken to carry: what the stations its lines meet hold, or else what's on its lines' belts (BR-A9).

**Item Offer**:
What a provider can deliver: an item and a rate per minute. For an extractor, its extraction rate × its node's purity × its clock speed (BR-W9); unknown for a station.

**Demand / Production**:
What a buildable asks and makes per minute at 100%, when that's known (BR-A1, BR-A8, BR-A16). What it **consumes** are the items it takes, rate known or not.

**Drone route**:
A drone's round trip between its home port and the port it's paired with: the flat distance between them, and how long its latest trip took (BR-A16).

**Trip cost**:
The energy a drone's round trip costs: 24,000 MJ plus 6 MJ per metre there and back.

**Refuels at**:
The port a drone takes its fuel from: its home port when fuel is in that port's slot, otherwise the port it's paired with (BR-A16).

**No drone refuels here**:
A drone port with fuel in its slot where no drone refuels: it burns none of that fuel, and a line that brings it backs up. A state of its own, not a burn rate of 0 (decided 2026-11-26).

## Factories

**Factory**:
A group of machines the player selects, generators included (BR-W2); its logistics, outputs and inputs are derived per snapshot (BR-W3, BR-W4). A machine is in one factory at most; of two factories stored before that rule with the same machine, the oldest owns it, though each counts it as its own (BR-W5).
_Avoid_: factory for a blueprint design (see **Blueprint**); output belt (factories are defined by their machines since 2026-11-12)

**Factory Preview**:
What the machines chosen so far would make a factory, traced in a snapshot without saving it: the logistics it would own and its outputs and inputs (BR-W3, BR-W4).

**Factories in a snapshot**:
A world's stored factories matched in one of its snapshots all at once: per factory its machines found or missing, whether it's traceable there and its members, and who owns each machine (BR-W5, BR-W11).

**Missing Machine**:
A factory's machine that isn't in a snapshot, not even rebuilt: no machine of its class stands within a metre that no other factory has by its id or, being older, as its own rebuilt machine (BR-W11).

**Factory Members**:
What belongs to a factory in one snapshot: its machines there, the logistics it owns, its outputs and inputs, its suppliers and producers.

**Factory Output / Input**:
Where a factory hands over to the world, or the world to it: its machine or logistics on the inside, what lies across, and the items (BR-W4).

**Line**:
Logistics connected to each other, with the machines, providers and consumers at its ends. It belongs to a factory when every machine on it is the factory's; one that touches machines of several factories connects them (BR-W3).

**Connected** (to a factory):
Its machines, and everything reached from them through logistics up to the buildings at the lines' ends, the walk stopping at another machine. Wider than what the factory owns (BR-W13).

**Supplies** (of a factory):
What feeds a factory, walking upstream only from its machines: where its lines start, the machines of the factories that feed them, and the logistics in between. Not its own machines, nor its belts between them (BR-W14).

**Supplier**:
A provider, station or container that nothing feeds, upstream of a factory's inputs. Never part of the factory; it can supply several (BR-W1, BR-A14).

**Producer**:
A machine of a factory that reaches one of its outputs through the factory's own logistics: it makes what the factory makes (BR-A14).

**Handovers** (of a factory):
What crosses a factory's inputs and outputs, per item: on the inputs, what arrives, what it needs from outside, how short it is and its sources; on the outputs, what leaves, its destinations and what its receivers need (BR-A17 to A19).

**Source** (of an input):
Where some of what a factory takes in comes from: another factory, machines of no factory, a building on its own, or nowhere (BR-A19). *Comes from* on the factory page.

**Destination** (of an output):
Where some of a factory's output ends up: another factory, machines of no factory, an AWESOME Sink, a consumer by building, storage and stations, or nowhere (BR-A18).

**Receivers' Need**:
What the machines and generators a factory's outputs reach would take of an item at 100%, from whatever source (BR-A18).

**Others Bring / Short**:
Of those receivers: what they take now from sources other than this factory, and what more they'd take at 100% than they get now from all sources (BR-A18).

**Sunk / Can't Leave**:
Of a factory's output: what reaches an AWESOME Sink, and what its output-blocked producers would make more at 100% (BR-A18).

## Analysis

**Steady State**:
What flows where once everything has run long enough to settle, to within 0.0001/min (BR-A1 to A10).

**Flow Part**:
A connected part of the world, simulated on its own. One that hasn't settled within 2,000 sweeps is **approximate**: its numbers came close but some still moved (BR-A10).

**Analysis Status**:
Whether a snapshot's analysis is ready: *pending* until it's computed, *ready*, or *failed* with its reason (ADR-0017).

**Finding**:
Something the analysis reports about a snapshot, with where it stands in the game: every machine and why one runs below 100%, a bottleneck, unused supply, or a source of unknown rate and how long its stock lasts (BR-A9, BR-A11 to A13).

**Utilisation**:
The share of 100% a machine runs at: the lowest of what its inputs supply and its outputs take (BR-A1).

**Carries / Reaches / Takes**:
How every finding and trace reads the analysis (decided 2026-11-26): what a piece of logistics **carries**, what **reaches** a buildable over the logistics ending at it, and what a machine or generator **takes** at the rate it runs (BR-A23).

**Starved / Output Blocked**:
Why a machine runs below 100% (below 99.9%): too little of an input is offered, or an output can't leave (BR-A11).

**Bottleneck**:
The first belt, lift or pipe of a line at its capacity while more is offered upstream (BR-A12).

**More capacity** (of a bottleneck):
What carries all that's offered to a bottleneck: the lowest tier of its kind that carries it alone, or else that many lines of the highest tier side by side (decided 2026-11-22).

**Unused Supply**:
What a provider could deliver on top of what it does (BR-A13).

**Spare**:
What a connection takes only once the line's other consumers have what they want: the fuel of a truck station, or of a drone port whose rate isn't known (BR-A8).

**Batch**:
What one cycle of a recipe makes of a product, somersloops included. A machine whose next batch doesn't fit in its output slot waits (BR-A7).

**Leaving / Backing up**:
A machine's product with room for more than one batch in its output slot is **leaving**, and never holds the machine back; with room for one batch at most it's **backing up** (BR-A7).

**Assumed / Confirmed Supplied**:
A source of unknown rate is assumed to supply what's asked; confirmed when its stock lasts 30 minutes at that rate (BR-A9).

**Running on stock**:
A machine or generator running on what it holds, because its belts bring less than it uses; it counts while the stock lasts 30 minutes (BR-A17).

**Loop**:
Machines that feed each other, directly or through others. It starts as if running; one that can't keep itself going winds down (BR-A10).

**Head lift**:
How high above itself a pump, or a fluid output, lifts fluid. A pipe whose top is above that **reach** is **short of head lift**, estimated (BR-A22); a gas needs none.

**Pipe flow**:
What flows through a pipe in the model: its own flow for a pipe between a network and a machine, else what its whole network takes in (BR-A6). Not in the save.

**Trace** (upstream, downstream):
Following a shortage upstream, or an output blockage downstream, from the held-back machines of one class and recipe to where it **starts**: the first place that holds the item back for a reason of its own (BR-A20, BR-A21).

**Cause** (of a start):
Why a start holds the item back (decided 2026-11-22). Upstream: *too few producers*, *bottleneck*, *source at its limit*, *taken by others*, *producer output blocked*, *unknown supply*, *nothing brings it*, *without a recipe*, *unclear*. Downstream: *too few consumers*, *bottleneck*, *supplied by others*, *consumer starved*, *storage full*, *nowhere to go*, *no drone refuels here*, *without a recipe*, *unclear*.

**Explanation**:
A finding put in words by a chat model, with a next step, where it starts, and any **unsupported numbers** it wrote (ADR-0030). It's the model's wording of the ledger's model, not a fact about the game.

## Arrangement

**Optimal Arrangement**:
A proposal for where the world's factories would best stand, so the belt work to their inputs from other factories is least (BR-O1).

**Arrangement Run**:
One computation of the optimal arrangement with its area and settings (plot size, roads, solver time), kept until the player deletes it (BR-O1).

**Plot**:
One of the equal rectangles an arrangement's area is divided into, in rows and columns with roads between, sized in foundations of 8 by 8 metres. A factory takes one (BR-O1).

**Footprints** (arrangement):
The other way to place factories: each takes the whole foundations its machines cover, with a road's width kept free beside it, so the roads stagger (BR-O1).

**Fixed Place**:
What doesn't move in an arrangement and keeps its plots: a factory outside the area, a station, a drone port, a machine no factory owns, or an exclusion (BR-O1).

**Exclusion**:
A rectangle drawn inside an arrangement's area where no factory may stand, kept with the run (BR-O1).

**Undefined factory**:
In an arrangement, machines no factory owns where a line starts or ends at them, marked and named so. Defining them as a factory gives them a name and a colour.

**Belt Floor**:
The floor below an arrangement's factories where the belts between them run, in squares of one foundation, under a ceiling of configurable height (BR-O2).

**Spot**:
The squares on the belt floor where a belt ends at a building that isn't arranged: a node's extractor, a station, a drone port, or the nearest machine of a factory outside the area or of machines no factory owns (BR-O2).

**Entry** (on the belt floor):
Where the existing belt from a building outside the arranged area comes into it, in the save; a routed belt to that building joins it there (BR-O2).

**Stack**:
The belts on one square of the belt floor, one above the other and keeping their order, at most one per 2 metres of ceiling; all belts or all pipes, never both (BR-O2).

**Level** (on the belt floor):
How high a belt runs in the stacks: 2 metres a level from the floor up. A belt keeps it from turn to turn (BR-O2).

**Ramp**:
How a belt changes level by 2 metres where it turns (BR-O2).

**Lift** (on the belt floor):
Where a routed belt changes level by 4 metres or more at a turn, goes up into its factory, or comes down from it (BR-O2).
_Avoid_: for the game's conveyor lift in a save, which is logistics

**Load** (of a belt):
What one routed belt or pipe carries: an item, per minute. A link's rate of an item fills its belts one after the other (BR-O2).

**Crossing** (on the belt floor):
Where a belt or pipe passes over another: only where both run straight through the square, at right angles, on different levels (BR-O2).

**Last-resort crossing**:
A crossing where one of the belts turns, or parts from a stack swapped: allowed only for belts that can't get through otherwise, and counted and marked (BR-O2).

**Rip-up and Reroute**:
When belts can't get through, taking them all up and laying them again with the failed links first; the try with the fewest left over is kept (BR-O2).

## Blueprint design

On hold since ADR-0012. Clock Speed, Power Shard, Somersloop and Slot Count still apply to machines in the world.

**Blueprint**:
A design made in the 6×6 (Mk.3) Blueprint Designer that turns its inputs into one main product and optionally a byproduct.
_Avoid_: factory (a factory is placed, a blueprint is designed)

**Production Step**:
One recipe running on a number of identical machines, with the same clock speed and somersloops: "4× Constructor making Iron Plate at 100%".

**Clock Speed**:
A machine's speed, 1%–250%, scaling its inputs and outputs linearly. Under 100% it's underclocked, over 100% overclocked.

**Power Shard**:
What overclocking takes: one per started 50% above 100%.

**Somersloop**:
A production amplifier placed in a machine's slots. It raises output, not input; full slots double the output.

**Slot Count**:
How many somersloops a building can hold.

**Main Product**:
The single item a blueprint is built to produce.

**Byproduct**:
A secondary output of a blueprint, declared by the designer.

**Undeclared Surplus**:
An item with a balance above zero that's neither the main product nor a declared byproduct. A design error.

**Precision Surplus**:
A tiny undeclared surplus that the game's clock precision (4 decimals) can't remove. Allowed (BR-B15).

**Balanced / Unbalanced**:
A blueprint is unbalanced while it has a deficit or an undeclared surplus. Only a balanced blueprint can be placed.

**Blueprint Input / Output**:
An item rate crossing a blueprint's edge, per copy, derived from its production steps.

**Item Balance**:
The net rate of one item inside a blueprint: produced + input − consumed − output.

**Surplus / Exact / Deficit**:
An item balance above, at or below zero.

## Factory network

On hold since ADR-0012; the supply line terms will be redefined on the real graph. The belt floor of an arrangement already routes items as supply lines (BR-O2).

**Placed blueprint**:
A blueprint placed in the world, stacked vertically a number of times. What this context called a factory.

**Stack Height**:
How many copies of a blueprint are chained vertically. Inputs and outputs scale linearly.

**Flagged Factory**:
A placed blueprint that became unbalanced, or whose stack no longer fits, after a blueprint edit. It keeps working with its last balanced inputs and outputs (BR-F6, BR-F7).

**Max Stack Height**:
The highest stack whose total input and output rates still fit the highest belt or pipe capacity (Mk.6 belt, Mk.2 pipe).

**Resource Supply**:
A raw resource source with a rate: miners, a train, a drone.

**Supply Line**:
One belt or pipe carrying one item: a source at the start, then an ordered sequence of taps and injections. An arrangement can route every item as supply lines past the factories, leg by leg (BR-O2).

**Source** (of a supply line):
What feeds a supply line: a resource supply, a factory output, or the overflow of another line.

**Tap**:
A consumer taking its demand of a line's item at a point along the line: a factory or a dimensional depot uploader.

**Dimensional Depot Uploader**:
Uploads items to the dimensional depot, for use as building material. A tap with its upload rate as its demand.

**AWESOME Sink**:
Destroys items for coupons. Required at the end of every belt line carrying a non-raw item (BR-S16).

**Injection**:
A second source merged into a belt line with a priority merger. Fluid lines have none (BR-O2).

**Priority Merger**:
Takes the line's leftover as *primary* first, then tops up from the injected *secondary* source until the belt is full.

**Smart Splitter** (before a priority merger):
Sends the merger what it takes and the rest on as **overflow**, to top up another line, start one, or feed the factory where it stands (BR-O2, BR-S5).

**Section**:
A run of consecutive taps between two injections, or the line's start or end. Its taps have no priority: a shortage is shared proportionally.

**Segment**:
The stretch of belt between two consecutive points, with one flow.

**Remaining**:
What's still on the belt after a point. What remains after the last point is there for future factories.

**Unallocated Leftover**:
Raw resource flow left at the end of a belt line, waiting to be routed as the primary input of the next factory (BR-S15).

**Overflow**:
The part of an injected source that doesn't fit through the merger. It can be split off as the source of a new supply line.

**Unused Supply** (of a fluid line):
Flow left on a fluid line after its last tap: wasted, not kept for later.

**Underclock Advice**:
The clock speed the source of a fluid line should run at so supply matches demand, rounded up.

**Stranded Capacity**:
Overflow that isn't routed anywhere: supply that's there but can't be used.

**Line Analysis**:
The segment-by-segment result for a supply line: flow, remaining, deficits, overflow and stranded capacity.

**Consumer Link**:
A record that one placed blueprint consumes part of another's main product or byproduct.
