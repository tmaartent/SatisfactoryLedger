# ADR-0034: Head lift is estimated, to show only

- **Status:** Proposed
- **Date:** 2026-11-24

## Context
The owner asked to see what's in pipes, how much flows, and their head lift, and whether the save has them.
- **In the save:** each pipe, pump and junction keeps its `mFluidBox`, the m³ in it at save time, and its network keeps which fluid (`mFluidDescriptor`). Each pump keeps its two ports, `Connection0` and `Connection1`.
- **Not in the save:** the flow rate, pressure and head lift. The game works them out as it runs.
- **In the Docs file:** a pump's head lift, twice: `mDesignPressure` (Mk.1 20 m, Mk.2 50 m, as its description says) and `mMaxPressure` (22 m, 55 m). Extractors, the refinery, packager and blender say "Head Lift: 10 m" in their description only.
- **Which way a pump pushes:** in the owner's save, 199 pumps had a fluid extractor on the side of `Connection0` and machines on that of `Connection1`; 16 the other way round (built backwards, or reached through a loop).

## Decision
- **Contents** come from the save: a pipe's fluid and m³, as a belt's items.
- **Flow** comes from the model (BR-A6): a pipe at a network's edge has its own; a pipe inside shows what its whole network takes in, said as such.
- **Head lift is estimated** (BR-A22), by the owner's choices of 2026-11-24:
  - every fluid output lifts 10 m, also those whose Docs description doesn't say so; a fluid port counts only while the building puts out fluid: a machine whose recipe makes one, an extractor of one, a station (the owner, after the second run);
  - a pump lifts to its **`mDesignPressure`**, as its description says. The owner tried `mMaxPressure` after the third run (water reached 2.7 to 3.9 m above 50 m past Mk.2 pumps in their save), then chose the design pressure.
  - a pump's head lift doesn't stack: past it, fluid reaches its head lift above it, or as high as before when higher;
  - a pipe above its reach is a **warning only**: the flow still sends fluid through;
  - **gas needs no head lift** (the owner, after the first run): the Catalog reads which fluids are gases from the Docs file (`mForm` `RF_GAS`: exactly Rocket Fuel, Nitrogen Gas, Excited Photonic Matter, Dark Matter Residue and Ionized Fuel), and a network carrying one gets no estimate.
- The Catalog imports the pumps (`Pump`, `pumps` table); a snapshot copies their head lift, and which pipe piece each pump pushes into. Snapshot version 15; an older snapshot has no estimate.

## Alternatives considered
- **Show the pumps' head lift only.** Simple and exact, but says nothing about a pipe.
- **Stop the flow** past a pipe short of head lift. Machines past it would run short in the model, and the estimate's simplifications (below) would move every figure.
- **Head lift that adds up** past a pump. More forgiving, but not what the game says ("Head Lift does not stack").

## Consequences
- In the owner's world, without the gas rule, 1,143 of 6,076 pipes came out short, 744 of them by over 100 m: rocket fuel from two blenders at −31 and −64 m up to fuel generators 255 to 342 m high, without a pump. That runs in the game, which is how the gas rule came about.
- With it, 13 of 4,155 pipes came out short; 9 of them empty, 18.5 m short: an all-purpose blueprint of stacked refineries on Pure Iron Ingot, whose fluid output makes nothing. Counting only ports that put out fluid, **4 of 3,278** remain: an empty pipe 11.5 m above what a Mk.2 pump lifts crude oil to, on its way to a particle accelerator (Oil-Based Diamonds), and 3 water pipes 2.7 to 3.9 m short past Mk.2 pumps, which hold water in the save. Those 4 of 3,278 stand with `mDesignPressure`; with `mMaxPressure` only the oil pipe would, 6.5 m short.
- Simplifications: a network's fluid is what its pieces hold at save time; heights are a pipe's curve points (between them the curve may rise a little more); a source's height is its pipe's end; valves pass fluid both ways; a fluid buffer gives no head lift; a pump past a pipe short of head lift still lifts.
- A pipe's saved fluid stays out of the model: an empty station's cargo is still guessed from belts only (BR-A9).
