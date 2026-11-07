# ADR-0021: Generators burn their fuel at full power, at rates from the Docs file

- **Status:** Accepted
- **Date:** 2026-11-06

## Context
Generators were consumers that take everything they're offered (BR-A8). A nuclear plant also *makes* something, its waste, and the analysis never let that enter the network.

In the big world, colliders on Ficsonium and on Plutonium were starved at 0%, while their buffers held hundreds of plutonium waste. The plutonium waste comes from nuclear plants burning plutonium rods; the non-fissile uranium is blended from uranium waste.

How much a generator burns depends on its power output. In the game, generators throttle down to what the power grid demands, and that demand isn't in the save.

## Decision
- **Burn rate from the Docs file** (decided 2026-11-06). A generator burns its fuel at its power production × its clock speed ÷ the fuel's energy, and needs its supplemental fluid (water) at power × its supplemental ratio. It makes its byproduct per fuel item burned.
  - **Nuclear on uranium rods:** 0.2 rods, 240 m³ water and 10 uranium waste a minute.
  - **Fuel generator on fuel:** 20 m³ a minute.
- **At full power.** The grid's demand isn't in the save, so a generator is assumed to run at full power. Nuclear plants usually do.
- **The fuel is the one in it at save time** (`mCurrentFuelClass`). A generator with an empty fuel slot, or a fuel the Catalog has no entry for, takes everything, as before.
- **In the flow** a generator burning a known fuel is a machine node: it runs at the lowest of fuel supplied, water supplied and waste taken. A nuclear plant whose waste can't leave stops, as in the game.
- **Generators stay consumers** (BR-W8): never part of a factory, not counted as machines. They get their own findings (BR-A15).
- **The Catalog** keeps a *Generator* entry per generating building: power, the litres of water per MJ, and per fuel its energy, supplemental fluid and byproduct.
  - A fluid's energy is per litre in the Docs file, so the Catalog keeps it per m³.
  - Geothermal burns nothing and isn't imported.

## Alternatives considered
- **An unknown-rate source for the waste** (like an unloading station, BR-A9): small, but no real rate. A plant burning half its fuel would still "supply" fully.
- **Investigate first:** the cause was clear from the 0% starvation and the full buffers.
- **Generators as machines:** simpler, but generators would enter the machine counts, and BR-W8 (consumers aren't part of a factory) would need revisiting.

## Consequences
- A generator takes only what it burns. Surplus fuel now backs up upstream, as it does in the game, so upstream findings change. The analysis version is 4, and stored analyses are computed again.
- Snapshot version 7 keeps each generator's fuel and clock speed, and the Catalog's generators. Older snapshots treat generators as before.
- **Partly-loaded power grids** look better supplied than they are: a generator at 40% load burns 40% of its fuel in the game, and the analysis assumes 100%. Reading the grid's demand (the save's power circuits) is on the enhancements pile.
