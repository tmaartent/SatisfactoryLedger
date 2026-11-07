# Step 22 · What's on the belts and in the machines

**Goal:** see what the save holds at the moment it was made: items on belts and lifts, in splitter and merger buffers, and in machines.

**Result:**
- The map's tooltips say what's on or in each belt, lift, splitter, merger and machine ("In: Iron Ore × 100, Out: Iron Ingot × 98").
- A toggle colours belts by what they carry most of, with a legend of the items.
- **Tests:** 612 .NET and 33 web tests, all green.

**Decisions you made:**
- **Contents are their own thing,** not stock, so they never count as supply in the analysis (BR-A9 stays as it was).
- Machines have contents too, after you asked.
- **On the map:** tooltips and colour by item.

**Decisions I made, for you to overrule:** see "Choices worth a second look".

## What was built

```
Save reader   items on belts and lifts, from the conveyor chains · splitter and merger buffers (mBufferInventory)
              · machine input and output inventories (mInputInventory, mOutputInventory)
Snapshot      version 6: contents per buildable, a machine's fluids in m³
World         the map endpoint passes contents on: conveyors, other logistics, machines
Web           describeContents · mainItem · itemColour · tooltips · "Colour belts by what they carry"
```

## How it works

- **Conveyor chains.** Since 1.0 the game keeps a belt's items on a *chain*: one run of belts and lifts with a single ring of items, listed from its head.
  - Each belt or lift says which stretch of the ring it has: from its first to its last index, wrapping round, or −1 when it has none.
  - The test world's miner chain is belt, lift, belt, with 12 iron ore: 7, 3 and 2.
- **Buffers and inventories.** Splitters and mergers point at a small buffer inventory; machines at an input and an output inventory. They're read the way container stock already was.
- **Fluids.** A machine counts fluids in litres (the packager's 50,000 water), so the snapshot divides by 1,000. It knows which items are fluids from the Catalog at import.
- **Not stock.** A belt with items that nothing feeds would otherwise look like a source to the analysis. Contents only reach the map.

## What it confirmed

Smelter A, which the analysis calls *output blocked*, holds 98 ingots waiting to leave. The save backs up the finding.

## Tests (one behaviour each)

| Where | Tests |
|---|---|
| `SaveFileReaderTests` | A belt carries its stretch of the chain · a lift carries its part · items are counted on a stretch that wraps round (mutation-checked) · an empty belt carries nothing · a splitter holds its buffer · a machine holds its ingredients and its waiting products · contents are not stock |
| `SnapshotDocumentTests` | A belt keeps what it carries · a machine keeps its contents with fluids in m³ · a version 5 snapshot reads without contents |
| `WorldMapTests` | A belt shows what it carries · a splitter shows its buffer · a machine shows its ingredients and products. *Written with the change, not seen failing first.* |
| `WorldMapApiTests` | The belt with its curve now also has its contents |
| Web: `map-model.spec.ts` | Describes contents most first · "empty" · nothing when unknown · fluids rounded · the main item · the same colour every time, ore and ingots different |

## Choices worth a second look

1. **The item `Position` field** on chain items isn't used. The library's types don't say what it means.
2. **Colours come from a hash** of the item's class name into 16 colours. Two items can share a colour; the legend shows which.
3. **Pipes have no contents yet.** Fluid buffers keep their fluid in an `mFluidBox`, and pipes may do the same. I haven't checked: a possible next step.
4. **Containers and stations** keep their *stock*; they don't also get contents.
5. **Older snapshots** (version 5 and before) show no contents until the save is imported again.

## Next

Your look at the map with contents. The open items from before: the solver's accuracy, `npm audit`, and building names from the API.
