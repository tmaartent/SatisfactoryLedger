# Business rules

Each rule has an ID. **Every rule should end up as one or more tests named after it.** Write the test first, then the code (see [guide step 3](../guide/03-tdd.md)). If a rule changes, change it here first.

All rates are per minute. Solids are items/min, fluids are m³/min.

> **Direction changed 2026-11-04 ([ADR-0012](../adr/0012-diagnose-the-world-from-the-save-file.md)).** The world is now imported from the save file and diagnosed. Planning comes later.
> - **Still valid:** BR-B2, BR-B9, BR-B10 and BR-B11 (per-machine rates), and BR-F5 (max capacity).
> - **On hold:** the other Blueprint Design and Factory rules.
> - **To be rewritten as rules on the real graph:** the Supply line rules. BR-S3 is replaced by the real splitter and merger behaviour.
> - **Not changed yet:** the Layout suggestion rules (BR-P).
>
> The rules below stay as they are until each one is rewritten or retired.

---

## World (from the save file)

Decided 2026-11-04 ([ADR-0012](../adr/0012-diagnose-the-world-from-the-save-file.md)). These rules describe the world as imported from the save.

| ID | Rule |
|---|---|
| **BR-W1** | A **provider** brings items into the network from outside: miners, oil extractors, water extractors and resource well extractors, and the stations of trains, trucks and drones. A provider is **never part of a factory**. One provider can supply **more than one** factory. A station's platform (or drone port) **unloads when belts or pipes leave its outputs**; that decides, not its mode setting (decided 2026-11-05, [spike](../guide/log/14-flow-stations-and-stock.md)). A drone port with belts on both sides loads and unloads at once. |
| **BR-W2** | The player defines a **factory** by **its machines**, selected in the 3D view of an area (decided 2026-11-09). A **generator** can be one of them (decided 2026-11-11): it then takes its fuel and water as a machine takes its inputs, and leaves its waste as a machine leaves its products. A generator not chosen stays a consumer at the end of a line (BR-W8). The game has no notion of a factory, so this is the player's own data. Everything else about a factory (its logistics, outputs and inputs) is derived from the world, per snapshot (BR-W3, BR-W4). Factories defined earlier by an output belt and input items were converted once: traced in their world's latest snapshot, and stored as the machines that trace found. |
| **BR-W3** | A piece of **logistics** (belt, lift, pipe, splitter, merger, container, pipe network) **belongs to a factory** when every machine it is connected to through logistics, upstream or downstream, belongs to that factory (decided 2026-11-09). Logistics connected to each other form one **line**, so this is decided per line: a bus with a splitter is one line, its branches included. The **ends of a line** don't count as another factory: providers and unloading stations, sinks, uploaders, generators and loading stations, and the storage containers in the line itself. So a factory's line to its own storage belongs to it, and so does a supply line from a miner that feeds only this factory. A line that touches a machine of **another factory** belongs to **none**: it **connects** them. Examples: a bus with splitters feeding several factories, a pipe network collecting a byproduct from several factories, a belt from one factory's machine straight into another's. |
| **BR-W4** | A factory's **outputs** are where its machines and logistics hand over to anything that isn't the factory's: a line it shares with other factories, a consumer, or nothing after a storage container at the end of its own line (its output buffer). Its **inputs** are where something that isn't the factory's hands over to it: a provider, a shared line, or nothing before a container nothing feeds. A factory can have **several outputs and inputs** (decided 2026-11-09): a product and a byproduct, say. An output's items are what the factory's machines that reach it make. An input's items are what the factory's machines it reaches ask for and **don't get from inside the factory** (2026-11-09: so an input that brings back ingots the factory also makes, from a bus it shares, still carries ingots). |
| **BR-W5** | A machine belongs to **one factory at most** (decided 2026-11-11, superseding "a machine can be shared"). Defining a factory with a machine another factory of the world already has is refused (409 `World.MachinesTaken`, naming each machine and its factory). "Already has" is checked in the world's latest snapshot: by id, or as the machine that replaced one of its own (BR-W11). Factories stored before this rule that overlap are kept as they are; the 3D view shows the overlap. |
| **BR-W6** | A factory's output can feed **another factory** or a **train, truck or drone station**. To the receiving factory it's a provider. |
| **BR-W7** | *Superseded 2026-11-09:* with several outputs per factory (BR-W4), there's no single output item any more, so no finding for "another item leaves the factory". |
| **BR-W8** | **Consumers** take items out of the network: AWESOME Sinks, dimensional depot uploaders, power generators, and stations that load items: a platform or drone port with belts or pipes into its inputs. A consumer is never part of a factory, except a generator chosen as one of its machines (BR-W2). **Storage containers** on a belt are logistics: they pass items through. |
| **BR-W9** | A provider on a resource node extracts that node's resource. Per minute it delivers its **extraction rate** (Catalog, normal node at 100%) × the node's **purity** (impure ×0.5, normal ×1, pure ×2) × its **clock speed**. Resource and purity come from the game's map ([ADR-0015](../adr/0015-resource-nodes-from-the-game-map.md)), not from the save. |
| **BR-W10** | Every imported save becomes a **world snapshot**, and every snapshot is kept. A save that contains something the Catalog doesn't know is still imported. A machine whose recipe the Catalog doesn't know stays in the snapshot without known inputs or outputs, and the import result lists the unknown recipes. A recipe is taken from the Catalog **at import** and copied into the snapshot ([ADR-0016](../adr/0016-world-snapshots-as-one-document.md)). To pick up a recipe the Catalog learns later, import the save again. |
| **BR-W11** | A **world** is every snapshot with the same session name. A factory definition belongs to a world, not to one snapshot: it's defined once and traced in **every** snapshot of that world, earlier and later ones. Its machines keep their id from save to save; a machine **rebuilt** gets a new id, so it's matched by its **class and position, within a metre** (decided 2026-11-09). A machine with no match in a snapshot is reported as **missing** there. A factory none of whose machines is in a snapshot is **not traceable** there. Within a world, factory names are unique. |
| **BR-W12** | A **candidate output belt** is a belt (or lift) that's fed, through logistics only, by at least one machine, and that ends in a storage container, a consumer, or nothing at all (a belt end, or a station that isn't modelled yet). Its **items** are what those machines produce. The tool suggests candidates so the player can pick an output belt (BR-W2). Picking one is still the player's decision. *Only used by the old define request (output belt and input items). The web app's dialog for it was replaced by the 3D selection in step 37.* |

---

## Flow analysis (decided 2026-11-05)

How the World's analysis computes what flows where. A **steady state**: everything has run long enough to settle, and nothing fills or empties except as these rules say. All rates per minute.

| ID | Rule |
|---|---|
| **BR-A1** | A **machine** asks for each input at its recipe rate × clock speed, and makes each output at its recipe rate × clock speed × production boost (somersloops raise output only, BR-B11). It runs at a **utilisation** between 0 and 100%: the lowest of *supplied ÷ asked* over its inputs and *taken ÷ made* over its outputs. It takes and makes everything at that utilisation. When **several belts bring the same item**, it takes from them evenly, like a merger, by what each offers; a belt that brings less than its share, or nothing at all, leaves the rest to the others (fixed 2026-11-10: a fourth belt connected to your crystal oscillator manufacturers that brings nothing took half of every input, and they ran at 50%). |
| **BR-A2** | A **provider** offers: an extractor on a node its extraction rate × the node's purity × its clock speed (BR-W9), a water extractor its rate × clock speed. If less is taken, it delivers less (back-pressure). |
| **BR-A3** | A **belt, lift or pipe** carries at most its tier's capacity (Catalog). Belts, lifts, splitters, mergers and storage containers carry **solids**; pipes, pipe junctions and fluid buffers carry **fluids** (the Catalog says which items are fluids). A machine with a solid and a fluid product sends each over the line of its form; a product with no line of its form blocks the machine. |
| **BR-A4** | A **splitter** divides what comes in evenly over its outputs that take items. What one output can't take goes to the others. A **smart or programmable splitter** sends each item to the outputs its rules name: an item, *any*, *any undefined* (an item no other output names), *none*, or *overflow*. Evenly over those outputs; an overflow output only gets what the other outputs for that item can't take, and an output set to *none* gets nothing. A smart splitter without saved rules works like a plain one. |
| **BR-A5** | A **merger** takes evenly from its inputs, up to what its output can carry. An input that offers less leaves its share to the others, and so does one that **sends** less than its share, whatever it offers (fixed 2026-11-10: a smelter behind a slow belt offered its full rate, kept a share it couldn't fill, and the line behind it stood still). A **priority merger** takes from its inputs in order of priority: the input with the **highest number** first, then what's left from the next, and evenly between inputs of the same priority. 0 is the default (decided 2026-11-06: in the test world, the center input set to highest priority stores 2). |
| **BR-A6** | A **pipe network** (every pipe, junction, pump, valve and fluid buffer connected by pipe) is **one hub**, as the game treats it (decided 2026-11-07, [ADR-0023](../adr/0023-pipe-networks-as-one-hub.md)). It takes from every machine output and extractor that feeds it, and shares evenly over every input it feeds, like a merger and a splitter in one. The pipe between the network and each machine still limits that link to its capacity; the pipes inside the network don't. Pipes have no direction of their own, so this replaces guessing one. A **fluid buffer** in a network something feeds is full in steady state and passes nothing; in a network nothing else feeds, it supplies the network (BR-A9). Head lift and pump direction are ignored for now. |
| **BR-A7** | A **storage container** inside a line passes items through. **Everything with an inventory at the end of a line** (a storage container or fluid buffer, a freight platform, drone port or truck station that loads, a dimensional depot uploader) **takes everything until it's full**, like a sink. One that is **full at save time takes nothing**: its line backs up, and the machines feeding it are output blocked, as in the game (decided 2026-11-10; before, an end container always took everything). "Full" is read from the save (what it holds) and the Docs file (its slots and each item's stack size). **A station counts as full when it's full at save time**, even if vehicles load from it: the save doesn't say whether a vehicle comes, or how fast it empties the station (decided 2026-11-10). **The save overrules "full"** (decided 2026-11-10): when a machine feeding it through logistics holds some of every input (so it can run) and its output slot (a stack) has room for more than its next batch, that product is still **arriving**, so something empties the inventory. **Such a machine is never output blocked by that product**, wherever it goes: the save shows it leaving, as with your cooling system blenders whose surplus is still filling the belts (decided 2026-11-10). A machine whose slot holds between the stack less a batch and the stack is **backing up**: a backed-up machine fills its slot, waits until one batch fits again and fills it (your power shard encoder at 99 of 100, a batch of 1). It then takes that product as fast as those machines make it, each machine's output shared evenly over the ends its line reaches. A slot that can't take the next batch shows the machine blocked; an empty one of a machine that can't run shows nothing. A drone port that loads keeps what it sends in its own inventory, apart from what its drones bring. A slot holds one item; an item fits while a slot is empty or a stack of it isn't full. A fluid buffer takes fluid until it holds its volume. The **dimensional depot** holds each item up to its stack size × its upgrades; the save doesn't store the upgrades, so they're read from what it holds (the most stacks any item fills). |
| **BR-A8** | **Consumers** (sinks) take everything they're offered. A **dimensional depot uploader** takes only what the line's other consumers leave: in steady state a depot is full (decided 2026-11-07). Behind a smart splitter that sends *any* to the uploader and the *overflow* on, the overflow side is served first. What an uploader takes from the leftovers doesn't lower what the splitter offers its other outputs (fixed 2026-11-07: it had turned output-blocked cell makers into "starved of uranium ore"). A **generator** burning a known fuel (the one in its fuel slot at save time) takes at most its **burn rate**: its power production × its clock speed ÷ the fuel's energy (both from the Docs file), and, when it needs one, its **supplemental** fluid (water) at power × the Docs file's supplemental ratio. It makes its **byproduct** (uranium or plutonium waste) per fuel item burned, and runs at the lowest of fuel supplied ÷ burn rate, water supplied ÷ water needed, and byproduct taken ÷ made, like a machine (BR-A1). It's assumed to run at full power: the power grid's demand isn't in the save. A generator with an empty fuel slot takes everything it's offered, as before. A **truck station**, and a **drone port** whose rate isn't known (BR-A16), takes the fuel it burns (what's in its fuel slot) only from what the line's other consumers leave (decided 2026-11-05). A drone port whose rate is known takes its fuel like a generator: as an ordinary consumer, up to what its drones burn (BR-A16). Generators decided 2026-11-06. |
| **BR-A9** | A source whose rate isn't known, such as a station, a container nothing feeds (Q19), or an extractor on a node the Catalog doesn't know, supplies whatever is asked of it that its line can carry, up to the capacity of its belt. It **offers** that capacity even while nothing takes it yet, so two inputs of one machine from two stations don't wait for each other. Of the items asked, it sends each in proportion to what the machines it reaches ask, narrowed to the items it holds. An **empty station** carries what the stations its lines meet hold (all of it when they hold different items): freight platforms on a load balancer, of which only one has cargo yet. When those hold nothing either, it carries **what's on the belts of its lines** in the save: your four Ficsite Trigon platforms, all empty at save time, with only Ficsite Trigons on their belts (both decided 2026-11-10). Only a station with nothing to go by supplies whatever is asked. Everything it feeds is marked **assumed supplied**, unless its stock lasts: a source whose current stock (the save stores what containers and stations hold: a platform's inventory, what a drone port received) lasts at least **30 minutes** at the rate asked of it is **confirmed supplied**. Otherwise the finding says how long the stock lasts. Stock is a moment in time: a station that trains keep refilling can be fine with a low stock. A station takes back none of what it supplies itself (a drone port refuelling from its own cargo): that gains nothing, and would send a full belt round in a circle (fixed 2026-11-10). |
| **BR-A10** | Results are approximations: the simulation stops when no flow changes by more than 0.0001/min, and shows 4 decimals ([ADR-0004](../adr/0004-exact-rational-rates.md), amended). Each **connected part** of the world settles on its own. A part that hasn't settled after 2,000 sweeps is reported as **approximate**, with its numbers as they are; the other parts are not held up (decided 2026-11-05). A part that hasn't settled after 50 sweeps may be flip-flopping between two states: from then on, what each connection accepts moves only halfway towards its new value per sweep (damping, step 15). **Loops** (machines that feed each other, such as your nitric acid and non-fissile uranium blenders, or the Ficsonium encoders and colliders) start **as if running** (decided 2026-11-07): before the first sweep, a machine in a loop is offered all it asks, and the links inside the loop what its machines make. A loop that can't keep itself going still winds down. **Since 2026-11-10 every part starts running** ([ADR-0026](../adr/0026-steady-state-as-a-sequence-of-linear-programmes.md)): a linear programme finds the most that can flow, and the sweeps start from that, so a region whose machines wait on each other (such as an aluminium chain looping its water) isn't found standing still. What can't keep going still winds down. **In a loop, what the loop makes comes back first** (decided 2026-11-10, as in the game): where a byproduct coming back (the scrap refinery's water) meets the same item from outside the loop (a pump), at a junction, merger or machine input, the byproduct is taken first and the outside source waits. So only the outside source can stall the loop. Shared evenly, a small shortfall landed on the byproduct, grew every round, and wound a loop that runs in the game down to nothing. A priority merger's own priorities (BR-A5) still come first. *Known limit:* a balanced loop with nothing outside it to limit it can settle at any speed, since every speed is a steady state; the [linear programme](possible-enhancements.md) would find the largest. |

**Findings** (the first three; more later):

| ID | Finding |
|---|---|
| **BR-A11** | **Machine utilisation and why.** Every machine is listed with its utilisation, worst first. One below 100% gets the limiting cause: *starved* of an input item, *output blocked* (an output can't leave), or *no recipe known* (BR-W10). It names **every** input it's offered less of than it needs at 100%, with the share it's offered, and **every** product that can't all leave, with the share that can, the worst first (decided 2026-11-07); the limiting cause is the worst of them. A machine offered every input in full but still short (its sender is held back, or its belt is clogged) names the input that arrives least (fixed 2026-11-10: it named the first in its recipe). A machine at **99.9% or more** counts as running: a smaller shortfall is noise, about what the simulation leaves when it settles slowly (decided 2026-11-06; first 99.99%, lowered the same day). |
| **BR-A12** | **Bottleneck.** A belt, lift or pipe running at its capacity while more is offered upstream of it. The finding says how much more is offered. |
| **BR-A13** | **Unused supply.** A provider delivering less than it offers: what it could deliver more. Spare capacity for a new factory, or room to underclock. |
| **BR-A14** | **A factory's findings** (decided 2026-11-06). The machine findings of its own machines and the bottlenecks on its own logistics. Its **suppliers**: the providers, stations and containers nothing feeds upstream of its inputs, through logistics, with their unused supply and how long their stock lasts. A supplier is never part of the factory (BR-W1), and one that feeds several factories is listed with each. Its **output**: per item, what leaves over all its outputs now (BR-W4), and what its **producers** (its machines that reach an output through its own logistics) make at 100% at their clock speeds. *Since 2026-11-09 several outputs; before, the flow onto its one output belt.* |
| **BR-A15** | **Generator findings** (decided 2026-11-06). Every generator with a known fuel gets its utilisation and, below full power, why: *starved* of fuel or water, or *output blocked* because its waste can't leave. Generators stay consumers (BR-W8) and aren't counted as machines in the summary. A factory lists the generators it has (BR-W2) with its findings, and the **power** they make: at 100%, each generator's power at its clock speed; now, that times its utilisation (decided 2026-11-11). |
| **BR-A16** | **What drones burn** (decided 2026-11-07, [ADR-0022](../adr/0022-drone-fuel-from-the-latest-trip.md)). A drone's round trip costs the Docs file's 24,000 MJ **plus 6 MJ per metre** flown there and back, measured flat between its home port and the port it's paired with. A port burns that energy in its fuel **once every latest trip**: fuel per minute = trip energy ÷ the fuel's energy × 60 ÷ the latest trip's duration, as the game's port screen shows it. A drone that sat docked waiting has a long latest trip, so a low rate. A drone **refuels at its home port** when fuel is in that port's slot, **otherwise at the port it's paired with** when fuel is in that one's. A port where several drones refuel burns for all of them. What a port burns isn't known (BR-A8) when one of its drones has made no trip yet, or the Catalog doesn't know its fuel's energy. **Drone port findings**: every port with a known rate, with what its drones burn and what its belts bring, the least supplied first. Like generators, drone ports are never part of a factory (BR-W1). Trucks aren't covered: a truck station's fuel stays as in BR-A8. |
| **BR-A17** | **Running on stock** (decided 2026-11-07). What a machine or generator **holds** of an input counts as **supplied** when it lasts at least **30 minutes** at the machine's demand, as a station's stock does (BR-A9): the machine runs on it, whatever its belts bring, and still takes what they bring. A generator's stock is the fuel in its fuel slot, so hand-fed fuel counts. A machine running on stock, because its belts bring less than it uses, says so in its finding, with how long the stock lasts at that shortfall. Stock is a moment in time: a machine fine for hours now will still run dry if its line can't keep up. |

---

## Blueprint Design

| ID | Rule |
|---|---|
| **BR-B1** | A production step is one recipe on a whole number of identical machines (≥ 1). All machines in a step share **one setting**: the same **clock speed** (under-, normal or overclocked) and the same number of **somersloops**. |
| **BR-B2** | A step's base rate for an item = recipe amount × 60 ÷ cycle time in seconds × clock speed × machine count. |
| **BR-B3** | A blueprint has **declared inputs**: the item rates its input belts or pipes deliver per copy (e.g. 600 iron ore, 400 copper ore). |
| **BR-B4** | **Item balance** for each item = declared input + produced by steps − consumed by steps. |
| **BR-B5** | Balance < 0 → **deficit** (red): the blueprint can't run as designed. Balance ≥ 0 → OK (green). |
| **BR-B6** | A blueprint has exactly **one main product**, and its balance must be > 0. That positive balance is the blueprint's main output per copy. |
| **BR-B7** | Byproducts are **declared** explicitly by the designer (e.g. polymer resin from a refinery). A declared byproduct with balance > 0 is an output. **Any other item with balance > 0 is an undeclared surplus, which is a design error** (e.g. 2.5 surplus iron plates), except a precision surplus (BR-B15). |
| **BR-B8** | Blueprint outputs (main product + byproducts) and declared inputs are what other contexts see, through `BlueprintDesign.Contracts`. Steps stay internal. |
| **BR-B9** | **Clock speed** is between 1% and 250%, in the game's precision (up to 4 decimals, e.g. 33.3333%). It scales inputs **and** outputs linearly. |
| **BR-B10** | Above 100%, each machine needs **power shards**: 1 shard per started 50% above 100% (≤ 150% → 1, ≤ 200% → 2, ≤ 250% → 3). |
| **BR-B11** | **Somersloops** amplify **outputs only**: output × (1 + sloops ÷ slots). Inputs stay the same. A machine holds 0 up to its building's slot count (from the catalog). Buildings without slots can't be slooped. |
| **BR-B12** | A blueprint reports the shards and somersloops it needs per copy. A factory needs that × its stack height. |
| **BR-B13** | A blueprint has **at most one input per item**, and each input and each output (main product and byproducts) arrives or leaves on **one** belt or pipe. Per copy, each of these rates must be **≤ the max capacity** for its form (BR-F5). |
| **BR-B14** | A blueprint with at least one deficit (BR-B5) or undeclared surplus (BR-B7) is **unbalanced**. Otherwise it's **balanced**. Unbalanced blueprints can still be saved and edited, because a blueprint is unbalanced while you build it step by step, but they can't be placed as a factory (BR-F1). |
| **BR-B15** | An undeclared surplus that is **unavoidable at the game's clock precision** is **not** an error. It's unavoidable when it's smaller than the output change that lowering any producing step by the smallest clock step (0.0001%) would cause. That lower setting would create a deficit. The UI shows it as green with a note ("precision surplus"). |
| **BR-B16** | Each step in a blueprint has a **unique recipe**, so the recipe identifies the step. A step's output can be the input of one or more later steps. The balance is calculated per item (BR-B4), so step order is presentation only and doesn't affect the result. |

**Example (clock speed and sloops):** an Assembler on Stitched Iron Plate (base 18.75 iron plate + 37.5 wire → 5.625 RIP per minute), at 150% with 1 of 2 slots slooped:
- inputs = 18.75 × 1.5 = 28.125 iron plate and 56.25 wire
- output = 5.625 × 1.5 × (1 + ½) = 12.65625 RIP
- needs 1 power shard and 1 somersloop

**Example:** "Reinforced Iron Plates" with declared inputs of 600 iron ore and 400 copper ore. Steps: smelters → iron ingots, constructors → iron plates, smelters → copper ingots, constructors → wire, assemblers → RIP (Stitched Iron Plate alternate). Each intermediate item should balance to exactly 0 or close to it, and RIP balances > 0, which is the output.

---

## Factories

| ID | Rule |
|---|---|
| **BR-F1** | A factory is one **balanced** blueprint (BR-B14) placed with a **stack height** ≥ 1 (a whole number). |
| **BR-F2** | A factory's inputs and outputs = the blueprint's per-copy inputs and outputs × stack height. |
| **BR-F3** | For every input and output, the total rate must fit the **max capacity** for its form (solids → belt, fluids → pipe, BR-F5). Otherwise the stack height is rejected. |
| **BR-F4** | **Max stack height** = ⌊ min over all inputs and outputs of (tier capacity ÷ per-copy rate) ⌋. |
| **BR-F5** | Everything is assumed unlocked. **Max capacity** = the highest tier in the catalog: belt Mk.6 = 1200/min, pipe Mk.2 = 600 m³/min. There is no world setting for this. |
| **BR-F6** | Editing a blueprint is always allowed, even when factories are built from it. If the edit makes the blueprint **unbalanced** (BR-B14), or makes a factory's stack exceed max capacity (BR-F3), those factories are **flagged**, not blocked. The flag clears automatically when the problem is fixed. |
| **BR-F7** | While flagged for an unbalanced blueprint, a factory keeps using the **last balanced inputs and outputs** of its blueprint, so supply line analysis stays meaningful during editing. Once the blueprint is balanced again, the factory takes over the new inputs and outputs and its lines are re-analysed. |

**Example:** 600 iron ore per copy with Mk.6 belts (1200) → max stack height 2. A third copy is rejected by BR-F3.

Because of BR-B13, a single copy always fits, so the max stack height is always ≥ 1, and BR-F3 only comes into play from the second copy onward.

---

## Supply lines

A supply line is a single belt or pipe for **one item**. It starts at a **source** and passes an ordered list of **points**. Each point is a **tap** (a factory takes its demand) or an **injection** (a second source merged in by a priority merger).

| ID | Rule |
|---|---|
| **BR-S1** | A line carries one item and has a capacity: the max capacity for its form (BR-F5). |
| **BR-S2** | Flow at the start = min(source rate, capacity). Source rate above capacity is **stranded**. |
| **BR-S3** | **Taps (factory or depot, BR-S14) behave like a manifold: no tap has priority over another**, on belts and pipes alike. Consecutive taps between two injections (or the line start/end) form a **section**. If the section's flow covers the section's total demand, every tap gets its full demand and remaining = flow − total demand. If not, the shortage is **shared proportionally**: each tap receives flow × (its demand ÷ total demand), its **deficit** is the difference, and the remaining flow is 0. |
| **BR-S4** | **Injection (priority merger):** primary = remaining flow on the line (always consumed fully). The secondary is drawn up to the free capacity: drawn = min(secondary rate, capacity − primary). Flow after = primary + drawn. |
| **BR-S5** | **Overflow** of an injection = secondary rate − drawn. It can be split off as the **source of a new supply line**. Overflow that isn't routed anywhere is **stranded capacity**. |
| **BR-S6** | The flow remaining after the last point is **available for future factories**. What happens to it today depends on the line type: BR-S12 (fluids), BR-S15 (raw resources on belts), BR-S16 (non-raw items on belts). |
| **BR-S7** | A line source is one of: a resource supply, a factory's output (main product or byproduct), or the overflow of an injection. |
| **BR-S8** | Each output item of a factory is the source of exactly one supply line. "Factory B consumes A's output" is modelled as B tapping A's output line. |
| **BR-S9** | A factory's total supply of an input item = the sum of its taps on all lines for that item. Total supply < demand → the factory shows a deficit (red). |
| **BR-S10** | Lines must not form a cycle through overflow sources. |
| **BR-S11** | **Fluid lines (pipes) have no injections**, only taps. So there's no priority merger and no overflow line for fluids. Taps consume until the demand is met, the source is exhausted, or the pipe capacity is reached, whichever comes first. |
| **BR-S12** | What's left on a fluid line after the last tap is **unused supply**, not "available for future". |
| **BR-S13** | When a fluid line has unused supply, the analysis gives **underclock advice** for the source: advised clock = current clock × total demand ÷ supplied rate. In v1 it's expressed as a percentage of the source's current rate, because a resource supply has no machine details yet. It's rounded **up** to the game's precision (4 decimals), so the advice never creates a deficit. |
| **BR-S14** | A tap's consumer is a **factory** or a **dimensional depot uploader**. A depot tap has a demand equal to its upload rate (entered by the player, because it depends on depot upgrades). Use it for intermediates that are also building materials. |
| **BR-S15** | Leftover flow of a **raw resource** on a belt is meant to feed the next factory that needs it: it continues as the **primary input of a priority merger** (BR-S4), topped up by a new source. Until a factory taps it, the analysis reports it as **unallocated leftover** (available for future), not as waste. |
| **BR-S16** | A belt line carrying a **non-raw item** must end in an **AWESOME Sink**. Leftover flow is then sunk, and remains available for a future tap placed before the sink. A non-raw line with leftover flow and no sink is an **error** (it will back up into the factory). |

### Shortage example (BR-S3)

Iron ore, source 600, one section with two taps.

| Tap | Demand | Receives (600 ÷ 800 = 75%) | Deficit |
|---|---|---|---|
| Factory P | 300 | 225 | 75 |
| Factory Q | 500 | 375 | 125 |

Both factories show red. The order of P and Q makes no difference. A fluid line has no injections, so it's always one single section.

### Fluid example

Water, pipe Mk.2 (capacity 600). The source supplies 600 at 100%.

| Point | Flow in | Action | Flow out |
|---|---|---|---|
| Source | — | — | 600 |
| Tap Factory X | 600 | −250 | 350 |
| Tap Factory Y | 350 | −250 | 100 |
| *end* | | | **100 unused** (BR-S12) |

Advice: underclock the source to 500 ÷ 600 = 83.3333…% → rounded up to **83.3334%** (BR-S13). That supplies 500.0004/min, so it never comes up short.

### Worked example (the case that started it all)

Iron ore, capacity 1200.

| Point | Flow in | Action | Flow out | Notes |
|---|---|---|---|---|
| Source: Line A (resource supply 1200) | — | — | 1200 | |
| Tap Factory 1 | 1200 | −300 | 900 | |
| Tap Factory 2 | 900 | −400 | 500 | |
| Injection: Line B (resource supply 1200) | 500 | primary 500 + drawn 700 | 1200 | overflow 500 → **Line C** (BR-S5) |
| Tap Factory 3 | 1200 | −900 | 300 | |
| *end* | | | **300** | available for future (BR-S6) |

**Line C** (source: overflow of Line B, 500) → 500 available for future factories.

If Line C isn't routed, the analysis reports **500 stranded capacity**.

### Steady-state note

The analysis assumes every tap draws its full demand and that "future" capacity is being drawn as well. If downstream draws less, the belt backs up, the merger draws less from the secondary, and the **real overflow is larger**. So the analysis reports a **guaranteed minimum** overflow. Name it that way in the API and the UI.

---

## Layout suggestions

| ID | Rule |
|---|---|
| **BR-P1** | Input: one item, a set of factory demands, available sources, and a capacity. Output: an ordered list of taps and injections in which every demand is met. |
| **BR-P2** | Prefer fewer sources (injections) first, then less stranded capacity. |
| **BR-P3** | A suggestion is advice and is never persisted automatically. The player applies it explicitly. |
| **BR-P4** | For raw resources, a suggestion uses **existing leftovers first** (unallocated leftover at line ends and overflow lines) as the primary input, and only then adds new sources as secondary inputs (BR-S15). |

Example: demands 300, 400, 900 with sources of 1200 each. Order 900, 300 on Line A (exactly full), then inject Line B for the 400 → 800 available, nothing stranded.

---

## Open questions (decide, then update this doc)

- ~~**Q12** Where does the trace start?~~ **Resolved 2026-11-04:** at the output belt, where the merged output leaves. The trace goes upstream (BR-W2, BR-W3). Inputs start at a splitter (BR-W4).
- ~~**Q13** Shared machines?~~ **Resolved 2026-11-04:** allowed, and shown as shared (BR-W5). **Reversed 2026-11-11:** a machine belongs to one factory at most.
- ~~**Q14** Factory feeding factory?~~ **Resolved 2026-11-04:** yes, and a factory can also feed a train, truck or drone station (BR-W6).
- ~~**Q15** Side branches?~~ **Resolved 2026-11-04:** a factory only sinks its output item. Anything else leaving is an undeclared output (BR-W7).
- ~~**Q16** Other consumers?~~ **Resolved 2026-11-04:** sinks, depot uploaders and power generators are consumers, outside any factory. Containers are logistics (BR-W8). **Changed 2026-11-11:** a generator can be chosen as one of a factory's machines (BR-W2).
- ~~**Q17** Who finds the input splitter?~~ **Resolved 2026-11-04:** the tool finds it (BR-W4).
- ~~**Q18** What does a miner deliver?~~ **Resolved 2026-11-04:** the node's resource and purity come from the game's map, through a node table the Catalog imports ([ADR-0015](../adr/0015-resource-nodes-from-the-game-map.md)). Purity: BR-W9.
- ~~**Q19** A storage container that nothing feeds?~~ **Resolved 2026-11-05:** a source with an unknown rate. It supplies what's asked, and its stock decides between assumed and confirmed supplied (BR-A9).
- ~~**Q20** When is a source with an unknown rate confirmed supplied?~~ **Resolved 2026-11-05:** when its current stock lasts at least 30 minutes at the rate asked of it (BR-A9).
- ~~**Q1** Do fluid supply lines support injections?~~ **Resolved 2026-10-26:** no, only taps. Unused supply leads to underclock advice (BR-S11…S13).
- ~~**Q6** Underclock advice for belt lines too?~~ **Resolved 2026-10-26:** no underclock advice for belts. Leftover raw resources feed the next factory as primary input of a priority merger (BR-S15, BR-P4). Non-raw items feed the dimensional depot if needed (BR-S14), and the rest must go to an AWESOME Sink (BR-S16).
- ~~**Q7** Underclock advice without machine details?~~ **Resolved 2026-10-26:** yes. Advice is "reduce the source to X% of its current rate" until the Resource Nodes context exists.
- ~~**Q2** Can a blueprint need more than one belt of the same input item?~~ **Resolved 2026-10-26:** no. One input per item, each ≤ max capacity (BR-B13).
- ~~**Q8** Who owns the unlocked transport tiers?~~ **Resolved 2026-10-26:** everything is assumed unlocked. Max capacity is catalog data (the highest belt and pipe tier), so there's no world setting (BR-F5).
- ~~**Q3** How is a shortage divided over taps?~~ **Resolved 2026-10-26:** manifold behaviour, no priority between taps, for belts and pipes alike (pipes can't prioritise). A shortage is shared proportionally within a section (BR-S3).
- ~~**Q4** Is a surplus intermediate a warning or an error?~~ **Resolved 2026-10-26:** error. Byproducts must be declared, any other surplus makes the blueprint unbalanced (BR-B7, BR-B14).
- ~~**Q9** Is a surplus that's unavoidable at clock precision an error?~~ **Resolved 2026-10-26:** no (BR-B15). It's defined exactly, with no fuzzy tolerance.
- ~~**Q10** Existing factories when their blueprint becomes unbalanced?~~ **Resolved 2026-10-26:** flag the factories (BR-F6, BR-F7).
- ~~**Q5** Mixed settings within one step?~~ **Resolved 2026-10-26:** no. Every machine in a step has the same setting (BR-B1). A step has no machine groups.
- ~~**Q11** Same recipe in two steps?~~ **Resolved 2026-10-26:** no. Each step has a unique recipe, and a step's output can feed several later steps (BR-B16).
