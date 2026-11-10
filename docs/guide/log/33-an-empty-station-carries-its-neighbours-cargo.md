# Step 33 · An empty station carries its neighbours' cargo (BR-A9)

**Goal:** the power shard encoder `Build_QuantumEncoder_C_2146425563` came out at 0%, "starved of time crystals". In your game it gets its time crystals and runs at about 56%, idle when its power shards can't leave. Branch `fix/empty-station-cargo`.

## How it was found

1. The encoder's time crystals come from four freight platforms on a load balancer. In the save only one holds cargo (`…679006106`, 9437 time crystals); the other three were empty.
2. Under BR-A9, an empty station "supplies whatever is asked". So the model had those three offer everything the machines further on use: supercomputers, ficsite mesh, dark matter, quartz.
3. All of that arrived as an offer on the encoder's time-crystal belt. Under BR-A1, a belt bringing an item the machine doesn't ask for clogs it, so the encoder took nothing from that belt, and the platform with time crystals was asked for 50 a minute it couldn't deliver.

## The rule (decided 2026-11-10)

An **empty station carries what the stations its lines meet hold**: all of it when they hold different items, still narrowed to what the machines further on ask. "Its lines meet" means their outputs pass through the same belts, splitters or mergers before a machine. Only a station whose lines meet no station holding anything still supplies whatever is asked.

- `WorldFlow` works out the cargo: the line of each station, and for an empty one, the stock of the stations whose lines overlap.
- `FlowUnknownSource.Cargo` carries it: in the sweep (`Solver.AskedItems`), in the linear programme's item analysis (`ItemsOnConnections`), and over the wire.
- `Provider.IsStation` tells a station from an extractor on an unknown node. A station has a stock the save reads; an extractor has none. An extractor never takes on a neighbour's cargo.
- BR-A9 updated; glossary *Cargo*; analysis version 11.

## On your world

The three empty platforms now carry time crystals. Run on the saved flow network, with the cargo rebuilt the same way:

| | Before | Now | Your game |
|---|---|---|---|
| Encoder `…2146425563` | 0%, starved of time crystals | **47%, output blocked on power shards** | ~56%, output blocked on power shards |
| Quantum encoders (11 here), sum | 3.1 | 5.4 | |

The rest of the gap (47% vs 56%) is how many shards the two encoders further on take.

## Tests

Written first:
- `WorldFlowTests`: an empty station takes the cargo of the station its lines meet; the union when two hold different items; none when its lines meet no station.
- `SteadyStateTests`: an empty station supplies only its cargo, so it clogs no machine with what others further on ask.
- `FlowNetworkDocumentTests`: the cargo survives the document.
- Snapshot: an unloading drone port is a station; a miner isn't.

**Totals:** 828 .NET tests.

## Points worth noticing

| | Java | Here |
|---|---|---|
| A positional record gains a parameter with a default | A new constructor | `FlowUnknownSource(…, Inventory? Inventory = null, IReadOnlySet<ItemId>? Cargo = null)`: every existing `new` still compiles; callers name it, `Cargo: …` |
| `with` on a record from a pattern | Builders | `(FlowUnknownSource)node with { Cargo = carried }` in the probe |

## Technical choices you can overrule

- "Lines meet" follows outputs downstream through logistics only, up to the first machine, consumer or station.
- A station counts as empty when it holds nothing at all; one holding a single item is taken at its word.

## Still open

- **An idle machine takes inputs it can't use.** It decides how many shards the encoders further on take, and so the last 9 points of this encoder.
- How fast vehicles empty a station (enhancement pile).
