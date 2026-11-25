# Step 108 · Sources no factory owns, and factories outside the area, as spots

**Goal:** picking *Wire, Cables, Copper Sheets* in the Learning run on footprints (step 107) lit Quickwire and a short Rubber line, but no Copper Ingots, though the factory takes 596 a minute (asked 2026-11-25). The copper comes from smelters no factory owns. The arrangement dropped such sources, in placing and routing alike, and didn't route links to factories outside the area either. Branch `feat/arrangement-touch`, after step 107.

## What was wrong

`WorldArrangement` kept an input only when its source was another factory (routed only when inside the area) or a node's extractor, station or drone port. A source of machines no factory owns (`InputSourceKind.NoFactory`) fell through both, so it had no link, no belt, and no pull on where the factory goes. Half the factory's Caterium came from the *Caterium Ingots* factory outside the area: a full-weight link, but not routed (BR-O2 said so).

Quickwire and the Rubber line were right. 13 of the factory's constructors run **Quickwire Cable** (Quickwire + Rubber), and a drone port brings 33.3 Rubber a minute.

## What you decided (2026-11-25)

- **Like freight platforms and drone ports: a dot where the line starts.** The dot is the one of the source's machines standing **nearest the factory it feeds**, as built.
- **One outside the area is reached like a miner**: the belt leaves the floor where the machine's existing belt comes into the area.
- **A machine no factory owns inside the area takes its ground**: it blocks the plots (or foundations) it touches, like a station.
- **A factory outside the area is routed like a miner too.**

### Choices to overrule

- **"Like a miner" includes the weight:** a factory outside the area now pulls in placing at the *Nodes & stations* weight (0.5), not at full weight as before. Say if it should stay at 1.
- **Every machine or generator no factory owns that reaches into the area blocks**, whether or not it feeds an arranged factory, as stations do. On your Learning area that's **356 machines**.
- **One dot per item per factory**: all the no-factory machines bringing the factory one item are one source, at the nearest of them, as the analysis groups them.
- **What an arranged factory sends to a factory outside the area** still isn't routed. Only what comes in was asked for.

## How it works

- **`InputSource.Machines`** (new): for a factory source or a no-factory source, the machines what arrives starts at. `FactoryInputs` already traced each source back to its machine, but kept only the kind. It's for the arrangement only, so it's left out of the API with `[property: JsonIgnore]`, and `api/openapi.json` doesn't change for it.
- **`WorldArrangement`**: such a source feeding an arranged factory becomes a `Supply` at its machine nearest the factory's middle (`Nearest`), alongside the extractors, stations and drone ports. So it merges onto belts with them, gets a spot, joins supply lines as a source, and is reached through `ExistingWays` when it's off the floor. `ExistingWays` follows logistics from any building, so a smelter needed no change.
- **Blocking:** every machine or generator outside the traced factories whose outline reaches into the area is a blocking fixed place.
- **The page** draws a spot that isn't a miner or extractor as a dark dot, the same as a station's, so it needed no change.

## On your Learning world, in the model

The same run as step 107 (the area, footprints, supply lines, Fast), made again:

| | Step 107 | Step 108 |
|---|---|---|
| Belts and pipes | — | 41, none unrouted, no crossings |
| Their length | 5,296 m | **9,704 m** |
| Copper Ingots to *Wire, Cables, Copper Sheets* | none | **596 a minute, from a smelter no factory owns** |
| Caterium from *Caterium Ingots* (outside the area) | none | 326 a minute to *Quickwire*, leaving the floor where its belt comes in |
| Rubber from an oil refinery outside the area | none | 8 a minute, topping up the drone port's line |
| Machines no factory owns blocking | 0 | 356 |
| Spread of the proposal | 584 × 560 m | 648 × 760 m |

The belts nearly doubled because more than copper came in. Iron Ingots from smelters no factory owns now reach *Wire, Iron Plate, Iron Rods* (1,200 + 455 + 165 a minute), Steel Ingots from foundries reach *Steel Beams* (900), and Concrete from a constructor reaches *Encased Industrial Beams* (89). The belt work went from 1,513.5 now to 1,195.5 proposed (−21%, best found in time). It doesn't compare with step 107's: there are more links.

## Files

| Where | What |
|---|---|
| `World/Analysis/InputSource.cs` | `Machines`, not in the API |
| `World/Analysis/FactoryInputs.cs` | Keeps the machines per source |
| `World/Arrangement/WorldArrangement.cs` | Such sources as spots at their nearest machine; machines no factory owns block |
| BR-O1, BR-O2, ADR-0028 (amendment), glossary (*Spot*, *Fixed Place*), primer | The rules, the decision, the terms, `[property: …]` |

## .NET and C# points

| Here | Java / Spring | TypeScript |
|---|---|---|
| **`[property: JsonIgnore]` on a positional record's parameter**: without `property:` the attribute lands on the constructor parameter, and System.Text.Json would still write the field (primer, *.NET basics*, *`[property: …]` on a positional record*). | A record component's annotation goes to every element its `@Target` allows; `@JsonIgnore` reaches the accessor. | No attributes on types. You'd map to a separate response type. |
| **`MinBy`** picks the nearest machine (`machines.MinBy(machine => distance²)`), returning the element, not the distance. | `stream().min(Comparator.comparingDouble(…))` returns an `Optional`. | `reduce` by hand, or lodash `minBy`. |
| **A `SortedSet<string>` keyed by a tuple that holds a tuple** (`(string Item, (InputSourceKind, StoredFactory?, string?, string?) Source)`): value tuples compare by value, nested ones too, so they work as dictionary keys without a key class. | A `record Key(String item, Source source)`. | A string key built by hand, `${item}|${…}`. |

## Tests

The new tests failed first, on compile: there was no `Machines`.

- **World** (`FactoryInputsTests`): a source of machines names the machines it starts at (smelter A for *Smelting*, smelter B for no factory).
- **World** (`WorldArrangementTests`, test save):
  - Ingots from a smelter no factory owns come from a spot where it stands, at the spot weight.
  - Of the machines no factory owns that bring an item, the spot is at the one nearest the factory, bringing all 22.5 a minute.
  - A machine no factory owns takes its ground, like a station.
  - Ingots from a factory outside the area come from a spot at its machine nearest: one belt with smelter B, which stands with it, as two miners'.
- **Changed, the rule having changed:**
  - With supply lines, smelter B is now a source of the ingot line.
  - *Where things stand* compares blocking places by id, not order.
  - The API's supply-line run expects smelter B's top-up of 10 a minute.
  - Three API runs about keeping and deleting runs now run on footprints: on 48 m plots the test world's machines no factory owns leave no room. The 48 m default is still checked.
- **Results:** 1,252 .NET tests pass (exit 0). No web code changed. Tried on the Learning world through the running stack (above).
