# Step 30 · A full inventory blocks its line (BR-A7)

**Goal:** everything with an inventory at the end of a line (storage container, fluid buffer, freight platform, truck station, drone port, dimensional depot uploader) takes everything **until it's full**. One full at save time takes nothing, and the machines feeding it are output blocked. Decided 2026-11-10, including: a station that's full at save time counts as full, even if vehicles load from it.

Checking it against your world, in your game, led to four more decisions and fixes, all 2026-11-10:

1. **The save overrules "full"** when it shows products still leaving the machines that feed an inventory.
2. **A loop takes back its own byproduct first** (BR-A10).
3. **A loading drone port** is read from the inventory it sends from.
4. **A station takes back none of its own supply** (BR-A9).

## How "full" is known

| What | From | Where |
|---|---|---|
| Slots of a storage, or m³ of a fluid buffer | Docs file: `mInventorySizeX × Y`, `mStorageInventorySize`, `mStorageCapacity` | Catalog `Storage` |
| How many fit in a slot | Docs file: `mStackSize` (SS_ONE 1 … SS_HUGE 500; fluids 0) | Catalog `Item.StackSize` |
| What a container or station holds | The save: its inventory; a drone port that loads, its `InputInventory` | `SavedBuildable.Stock`, `GameClasses.LoadingInventoryOf` |
| What the depot holds | The save: `CentralStorageSubsystem.mStoredItems` | `SavedWorld.DepotStock` |
| The depot's upgrades | **Not in the save**: read from what it holds, the most stacks any item fills | `Inventory.DepotStacks` |

A slot inventory takes an item while a slot is empty or a stack of that item isn't full. The fluid freight platform is left out: its stack-size multiplier makes it something else.

## The save overrules "full"

Your casings go to the depot (1000 = 5 stacks × 200, full) and four freight stations (each 9600 = 48 × 200, full). Taken at face value, every casing assembler and the smelters behind it stop. But many assemblers held ingots and had nothing waiting to leave: their casings were leaving when you saved, so trains empty those stations.

- A machine shows a product **leaving** when it holds some of every input (it can run) and its next **batch** still fits in its output slot. An Alclad casing assembler makes 15 a cycle, so with more than 185 of 200 waiting it's blocked (your call, over "room by share" and "nearly empty only").
- A full inventory then takes that product as fast as those machines make it (`Inventory.Arriving`, per minute). A machine whose line reaches several ends shares its output over them evenly; without that, every station was credited with the whole line's output.
- That rate is a lower bound: how fast trains really empty a station isn't in the save (on the enhancement pile).

## A loop takes back its own byproduct first

The aluminium loops wound down to 0%, though they run smoothly in your game:

- The alumina refinery needs 333.3334 water: 200 back from the scrap refinery, 133.32996 from a pump, 0.0034/min short.
- A pipe junction shared what the refinery accepts **evenly**, so the pump was served in full and the whole shortfall landed on the water coming back. That grew ×1.67 per round, down to nothing. Damping wasn't it.

Your rule, as the game does it: **what the loop makes comes back first**; the pump waits. It applies at junctions, mergers and a machine's inputs, per item the loop makes, below a priority merger's own priorities. With it came a fix to priming (BR-A10): a link from inside a loop is primed only with what the loop makes. Before that, the nitric acid loop's water pipe seemed to bring nitrogen too.

## Two reading and modelling bugs your game check found

- **The drone port.** The model had the ingot drone port `…2146650494` empty, so the smart splitter's *any* sent it everything and the Alclad sheet assemblers on *overflow* starved. In your game it holds 1800 ingots, full. A drone port keeps what it sends (`InputInventory`) apart from what its drones bring (`OutputInventory`), and the reader read the second.
- **A station refuelling itself.** The rocket fuel port `…2146652257` feeds its own fuel input through a splitter. With no known burn rate it took fuel from the leftovers without limit, and as a source it supplied whatever was asked: 1200/min round in a circle. An input a station's own outputs feed now takes nothing.

## On your world

| | Before this step | Now | Your game |
|---|---|---|---|
| Aluminium loop A | 0% | 100% | runs |
| Casing assemblers | all running | some blocked by the full stations | some blocked |
| Ingot drone port | read as empty | full (1800) | full |
| Alclad sheet assemblers | short of ingots | 12 of 20 run, 8 short | all run |
| Aluminium loop B | ran | 0% | runs, a bit blocked |

## What changed in the code

| Where | What |
|---|---|
| Catalog: `Storage`, `StorageId`, `Item.StackSize`, `CatalogStorage`, `ICatalogQueries.GetStoragesAsync` | Sizes and stack sizes from the Docs file; migration `AddStoragesAndStackSizes` |
| Catalog: `StorageDto` and five group DTOs, `SatisfactoryTranslator.TranslateToStorages` | Reading the storage classes |
| `World.SaveFile/SaveFileReader.cs`, `GameClasses.LoadingInventoryOf` | What loading stations and the depot hold |
| `World.Domain/Inventory.cs`, `InventoryKind.cs` | `Takes`, `TakesPerMinute`: slots, fluid, depot; `Arriving` per minute |
| `World.Domain/Machine.cs`, `Recipe.cs` | `OutputStock`, `Batches`, `ShowsLeaving(item, stackSize)` |
| `World.Domain/Flow/WorldFlow.cs` | `Evidenced`: what arrives at an inventory, shared over each line's ends |
| `World.Domain/Flow/Solver.cs` | A full inventory takes at most what arrives; a loop's own byproduct first (`FromOwnLoop`, `Prioritised`); priming per item the loop makes; `SelfFed` |
| `World.Contracts/Analysis/InventoryDocument.cs`, `AnalysisDocument.cs` | The inventory on the wire; analysis version 8 |
| `World/Snapshots/*` | Snapshot version 12: storages, stack sizes, depot stock; a machine's output stock and batches |

## Tests

All written first:

- `InventoryTests` (9).
- BR-A7 in `SteadyStateTests`:
  - a full container takes what arrives, and its machine runs at that.
  - a full depot.
- `WorldFlowTests`:
  - a full container takes what a running machine has room to send.
  - not with a full output slot; not when the next batch won't fit.
  - not from a machine holding none of an input; not when the save says nothing.
  - through a splitter.
  - the arriving rate is what the running machines make, shared over the ends.
- The aluminium loop: byproduct first at a junction and at a machine's inputs; the pump waits.
- A station takes back none of its own fuel.
- Save reader and translator: a loading drone port's inventory. Snapshot, batches and round-trip tests too.

**Totals:** 819 .NET tests.

## Points worth noticing

| | Java | Here |
|---|---|---|
| Extra properties on a positional record | Records can't have extra fields | `CopiedCatalog(…) { Depot { get; init; } }`, `Recipe(…) { Batches }` (primer: *A positional record with a body*) |
| Test and bind in one pattern | Not possible | `is not (> 0 and var stackSize)` in `Inventory.Takes` |
| An object initialiser needs `new` | Builders | `Inventory.OfSlots(…) with { Arriving = … }`: a factory's result is changed with `with` |
| Two `Inventory` types in one test file | Fully qualified names | `using BuildableInventory = …;` (primer: *A `using` alias settles a name clash*) |

## Technical choices you can overrule

- The depot's upgrades are inferred from its stock, rounded up.
- An inventory holding an item whose stack size isn't known is never called full (a Catalog imported before this step).
- "Can run" means holding some of every input.
- A fluid product has no stack size, so it never shows as arriving: a fluid buffer full at save time stays full.
- A machine's output is shared **evenly** over the ends its line reaches.
- An input a station's own outputs feed takes nothing at all, even when another line merges into it.

## Still open

- **The Alclad sheet manifold takes only 416 of the 600 ingots a minute it needs**, while the smelters feeding it are held back. A full belt would feed every assembler, so the sweep computes too little acceptance at the head of that manifold or at the two smart splitters before it. Copper is fine. Next step: instrument the solver's acceptance there. Loop B's standstill is probably the same thing.
- How fast vehicles empty a station (enhancement pile).
- The Ficsonium loops (from step 29e).
