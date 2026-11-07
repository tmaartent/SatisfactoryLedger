# Step 25 · Pipe networks, running on stock, and loops

**Goal:** your quantum encoders and hadron colliders showed as starved or blocked, while in the game they were full and running. Find out why, and fix what the analysis gets wrong.

## What was going on

Following the "starved of X" chain back from a Ficsonium encoder, three separate gaps stacked on one chain:

1. **The plants ran on rods the analysis couldn't see.** You hand-fed the plutonium plants. The save reader didn't read a generator's fuel slot, and a steady state ignores what's inside machines. So the plants looked starved, made no waste, and everything above them went to zero.
2. **Pipes were cut.** Pipes have no direction in the save, and the import guessed one per link. **654 of 10,034 pipe links** in your world got none and were dropped, among them the encoders' residue pipes.
3. **Loops started at zero.** The nitric acid ↔ water blenders, and the Ficsonium encoders ↔ colliders, each wait on the other. "All at zero" is a steady state, and the solver, starting from nothing, found it.

## What you decided

- **Stock counts like a station's** (BR-A17): what a machine or generator holds of an input counts as supplied when it lasts 30 minutes at its demand.
- **A pipe network is one hub** (BR-A6, [ADR-0023](../../adr/0023-pipe-networks-as-one-hub.md)), as the game treats it, instead of guessing directions.
- **Loops start as if running** (BR-A10): only the loops, not the whole world.
- Write this up and commit, then do the linear programme as its own step.

## Effect in your world

| | Start | Pipe networks + stock | + loops primed |
|---|---|---|---|
| Machines running | 1,623 | 1,792 | **1,851** |
| Starved | 477 | 333 | **295** |
| Output blocked | 324 | 299 | **278** |
| Plutonium plants at 100% | 0 of 11 | 0 | 3 |
| Plutonium pellet colliders | starved | running | running |
| Ficsonium encoders and colliders | 0% | 0% | 4% to 55% |

**The Ficsonium loop is still not right**, and that's a finding of its own. Each of the four residue networks is one encoder plus one converter feeding one collider, exactly balanced (58.6 + 58.6 = 117.2 m³). In such a loop every speed from 0 to 100% is a steady state, so the sweeps settle wherever they drift to. Only an objective ("as much as possible") picks 100%. That's what the linear programme on the enhancements pile does, and now its main reason.

## How it works

**Pipe networks** (`ConnectionBuilder.Networks`): pipe links are grouped into connected sets (union–find). Each set gets a hub, `PipeNetwork:<first pipe id>`, kind `PipeNetwork`, without a position. A pipe fed by an output sends into the hub; the hub sends into each pipe that feeds an input. In the flow, the hub is a splitter that also merges. A fluid buffer in a network nothing else feeds supplies it; otherwise it passes nothing.

**Stock** (`WorldFlow.Stocked`, `SteadyState.ShareOf`): an input counts as stocked when its stock ≥ demand × 30 minutes. The solver treats a stocked input's share as 1 when working out how fast the machine runs and what it accepts of its other inputs. It still accepts up to its demand from its belts. The finding compares what the belts bring with what the machine uses, and says how long the stock lasts at that shortfall.

**Loops** (`SteadyState.PrimeLoops`, `Loops`): Tarjan's strongly connected components, without recursion (a big world has long lines), find the loops with a machine in them. Before the first sweep, every input of a loop machine is offered what it asks, and the links inside the loop are offered, and given as flow, what the loop's machines make at 100%. Each of the three was needed. The first test found the loop's own accepts collapsing to zero when the machine's outside inputs hadn't been offered yet. The second found a loop with nothing from outside having no flow to start on.

## Files

| Where | What |
|---|---|
| `World.SaveFile/ConnectionBuilder.cs` | `Wiring`, `PipeNetwork`, `Networks` replaces the direction guessing |
| `World.SaveFile/SaveFileReader.cs`, `GameClasses.cs`, `SavedWorld.cs` | Hubs as `SavedLogistics`; a generator's `FuelContents` |
| `World.Domain/Logistics.cs` | `LogisticsKind.PipeNetwork` |
| `World.Domain/Machine.cs`, `Consumer.cs` | `InputStock` |
| `World.Domain/Flow/FlowNetwork.cs`, `WorldFlow.cs` | `FlowMachine.Stocked`; the hub as a fluid splitter |
| `World.Domain/Flow/SteadyState.cs` | `ShareOf`, `PrimeLoops`, `Loops` |
| `World/Snapshots/SnapshotDocument.cs` | Version 9; stock into the domain; hubs left out of counts |
| `World/Analysis/SnapshotAnalysis.cs` | `OnStock`, `StockMinutes` on machine and generator findings |
| `World.Simulation/AnalysisDocument.cs`, `FlowNetworkDocument.cs` | Analysis version 5; `Stocked` on the machine document |
| `web/src/app/world/findings.ts` | "On stock: X, lasts N min" |

## .NET points worth noticing

| .NET / C# | Java / Spring (TypeScript) | Here |
|---|---|---|
| `connection with { From = hub, FromPort = null }` on a positional record | A copy method or a builder; Java records have no `with` (derived record creation is still a proposal) | `ConnectionBuilder.Networks` |
| A local function closing over a dictionary (`Find` over `root`) | A lambda, but it couldn't loop as naturally | `ConnectionBuilder.Networks` |
| Value tuples as stack frames, `Stack<(int Node, int Next)>`, with `TryPeek` | `Deque<int[]>` or a small record | `SteadyState.Loops` |
| Optional trailing record parameters (`Stocked = null`, `OnStock = null`) keep every old call site compiling | Overloads | `FlowMachine`, `MachineFinding` |

Nothing new for the primer: the tuple-with-a-list comparison that bit a test here is already in it (Testing, *`Assert.Equal` on a tuple compares its collections by reference*).

## Tests (one behaviour each)

| Where | Tests |
|---|---|
| `ConnectionBuilderTests` | A pipe network takes from every output feeding it and feeds every input it reaches · knows its pipes · a pipe from an output straight to an input is no network · a fluid buffer in a fed network passes nothing · a buffer feeds a network nothing else feeds · a pipe that is fed and feeds an input · pipes that nothing feeds give no connections |
| `SaveFileReaderTests` | Water flows from the extractor through the pipe network into the packager · a pipe network is a hub that stands nowhere · a generator holds its fuel, and its water in litres |
| `SteadyStateTests` | A machine whose stock lasts runs without that input · still takes what its belt brings · what it makes flows on · without stock it's still starved · a balanced loop runs as once started in the game · an encoder and a collider that feed each other run · a loop that gives back too little still winds down |
| `WorldFlowTests` | 30 minutes of stock counts as stocked · less doesn't · a generator's fuel stock counts |
| `GeneratorSnapshotTests` | A nuclear plant holding 50 rods counts its fuel as stocked, not its few seconds of water |
| `StockFindingsTests` | Half an hour of stock: running on stock, lasts 30 min · less: starved as before · belts bring all it uses: not on stock |
| `SnapshotDocumentTests`, `WorldSnapshotApiTests` | Version 9 · a version 8 snapshot still reads · counts without hubs (64 connections in the test world, not 71) |
| Web: `findings.spec.ts` | Says what a machine on stock runs on and how long it lasts |

Red first: the three new pipe tests failed on the old guessing, the stock and loop tests on the old solver. Totals: 731 .NET tests and 41 web tests, all green.

## Choices worth a second look

1. **A bottleneck inside a pipe network isn't found any more**; only the pipes at its edge can be.
2. **Stock is checked at full demand** (30 minutes at 100%), like a station's, even when the machine runs slower.
3. **The water in a generator's tank** is counted as stock too; a nuclear plant's 50 m³ lasts seconds, so it never matters.
4. **Analysis version 5**: stored analyses are computed again, since stock changes results for every snapshot that has contents (version 6 and later).
5. **What I tried and dropped:** pointing pipe links towards the nearest machine. It fixed collection manifolds and broke distribution manifolds, which led to the hub.

## Then: the uploader that made cell makers look starved

You saw the encased uranium cell makers reported as starved of ore, while full of it. They were really **output blocked**: the fuel rod makers after them are starved of electromagnetic control rods and take only 76% of the cells. The splitter in front of them sends *any* to a dimensional depot uploader and *overflow* to the factory. The uploader rightly took only the leftovers, but what it took was also subtracted from what the splitter **offered** the machines. That pulled the chain down to 68% and flipped the label to "starved of uranium ore". Plain splitters had the same flaw.

- **Fix** (`SteadyState.Send`, `SendSorted`): what an output takes as spare no longer lowers what the other outputs are offered.
- **Tests** (`SteadyStateTests`): what an uploader takes from the leftovers is still offered to the factory, behind a smart splitter and behind a plain one. Both failed before the fix.
- **Found by** solving your snapshot locally, with and without the uploader: without it, the cells came out output blocked at 76%.

## Then: every input short, every output blocked

You asked which inputs are starved and which outputs blocked: a finding named only one item, and the map tooltip none.

- **Solver** (`SteadyState.Run`, `BlockedProducts`): `MachineState.StarvedOf` holds every input offered less than its demand at 100%, with that share; `BlockedOn` every product whose outputs can't take it all, with the share they can. A machine starved of something that's offered in full but doesn't arrive (its sender is held back) names that item with the share that arrives.
- **Findings** carry both, worst first; the analysis version is 6 so stored analyses get them.
- **Web** (`limit-text.ts`): one wording for the findings table and the map tooltip: "Not enough Uranium Ore (50%), Sulfur (80%)", "Can't all leave: Uranium Cell (76%)". *Choice to overrule:* shares are floored, so 99.5% shows as 99%, never 100%.
- **Tests:** `SteadyStateTests`: short of two inputs names both with their shares · short of one names only that · an output that can't leave names the product with its share · a running machine names nothing. `StockFindingsTests`: a starved machine names every input it's short of. `GeneratorFindingsTests`: the blocked packager names its canisters; the starved fuel generator its turbofuel. Web `limit-text.spec.ts`: five cases.
- **A record compares its dictionaries by reference**: `MachineState` with its new dictionaries broke a round-trip test. In the primer (Testing).

## Next

The linear programme, as its own step: it finds the largest steady state, so balanced loops such as your Ficsonium pairs run at the top.
