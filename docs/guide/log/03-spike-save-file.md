# Step 03 · Spike: what the save file gives us

**Goal:** before choosing how to read the save file ([ADR-0012](../../adr/0012-diagnose-the-world-from-the-save-file.md)), check against real saves that the diagnosis has the data it needs.
**Result:** the data is there and the connection graph is complete. The released library can't read game 1.2 saves; its unreleased `fix/v1.2` branch can. This led to [ADR-0013](../../adr/0013-read-the-save-with-satisfactorysavenet.md) (Proposed).
**Code:** a throwaway console app in a scratch folder, not in the repo.

## Inputs

| Save | Game | Size | Notes |
|---|---|---|---|
| `Clean Stop.sav` | 1.2 (save version 60, build 513885) | 18.9 MB | Latest save, 25 Oct 2026 |
| `Desert Start Cleaned_autosave_2.sav` | 1.2 | 18.9 MB | Same session, 12 minutes earlier |
| `Clean Stop_CALCULATOR (7).sav` | ? | 15.8 MB | 6 Oct, edited with Satisfactory Calculator |

The saves were copied first and only read.

## Can it be read?

| Reader | Result |
|---|---|
| `SatisfactorySaveNet` 4.0.0-alpha.3 (NuGet) | **Fails** on the latest save: `CorruptedSatisFactorySaveFileException` … `Unable to read beyond the end of the stream`. This is the same error as the library's open issue #14. |
| `SatisfactorySaveNet` branch `fix/v1.2`, commit `dd994a6` (1 Nov 2026, unreleased, "AI-assisted") | **Reads both 1.2 saves** in about 5 s. The full object model takes about 740 MB of memory. |
| Same branch, on the 6 October save | **Fails** with `EndOfStreamException`. Not investigated further: this file was written by a third-party editor. |

## What's in it (latest save)

278,065 objects in 3,900 levels. 84,790 of them are buildings (`Build_…`).

| Need from ADR-0012 | In the save? | How |
|---|---|---|
| Machine recipe | Yes | `mCurrentRecipe` → `Recipe_IronPlate_C`. These are class names, the same as in the Catalog. |
| Clock speed | Yes | `mCurrentPotential`, a **float** multiplier (1.6667 = 166.67%). Missing on 89 machines, which means 100%. |
| Somersloops | Yes | `mCurrentProductionBoost`, the output multiplier (2 = doubled). Only present on the 4 slooped machines. |
| Connections between buildings | **Yes, complete** | Each building has `FGFactoryConnectionComponent`s (`Input0`, `Output0`, `ConveyorAny0`). `mConnectedComponent` points to the other side. All **71,794** links are stored on both ends, and none point to a missing object. 15,000 connectors are unconnected (e.g. unused splitter outputs). |
| Pipes | Yes | `FGPipeConnectionComponent`. 18,402 of 22,123 are connected. |
| Belt and pipe tier | Yes | The class name: `Build_ConveyorBeltMk6_C`, `Build_ConveyorLiftMk1_C`, `Build_PipelineMK2_C`. |
| Splitters and mergers | Yes | 2,877 splitters, 1,820 mergers. |
| Smart (sorting) splitters | Yes | 212, with `mSortRules`: a list of (`ItemClass`, `OutputIndex`). |
| Priority mergers | Yes | 104, with `mInputPriorities` per input. |
| Identity over time | Yes, so far | `Persistent_Level:PersistentLevel.Build_ConstructorMk1_C_2147020985`. Between the two 1.2 saves, all 84,790 buildings kept their identity and all 2,424 recipes matched. Not yet tested across a game restart or after rebuilding something. |

Also there, but not asked for:

| Data | What it is | Use |
|---|---|---|
| `mLastProductivityMeasurementProduceDuration` / `…Duration` | How long the machine actually produced in the last ~300 s | **Observed** productivity. It's a finding in itself ("this machine only ran 40%"), and a check on our simulation. |
| `mSmoothedLoadRate` on train stations | Observed load rate | Unit not yet known. A possible input for train stations (216 of them). |
| `mBlueprintProxy` | 2,288 of 2,547 machines were placed from an in-game blueprint (1,023 placements) | The proxy itself has no readable properties in this library, so the blueprint's name isn't available yet. |
| Dimensional depot uploaders (`Build_CentralStorage_C`) | 42 of them | Taps that leave the network (BR-S14). |
| Storage containers | 1,507 | Buffers on belt lines. They have to be modelled: at steady state a container passes items through. |

## Rules the anti-corruption layer will need

1. **A missing property means the game's default.** Unreal leaves out values equal to the default. No `mCurrentPotential` means 1.0, and no `mCurrentProductionBoost` means 1.
2. **Clock speeds are floats.** `1.6667` is a binary approximation of a value the game shows with 4 decimals (BR-B9). Round to 4 decimals first, then convert to an exact rational (ADR-0004). Never compute with the float.
3. **The somersloop boost is already a multiplier.** BR-B11 counts sloops and slots: boost = 1 + sloops ÷ slots.
4. **Connections come in pairs.** Build the graph from one side and check that the other side agrees. That's a cheap consistency check on every import.

## .NET points worth noticing

| .NET | Java/Spring (TypeScript) | Note |
|---|---|---|
| `switch` expression with **property patterns**: `ArrayProperty { Property: ArrayObjectProperty a } => …` | Java 21 record patterns, roughly | Matches a type *and* a nested property in one arm. The compiler rejects arms that can never match (CS8510) or types that can't apply (CS8121). |
| A `ProjectReference` to a cloned repo | Maven `<module>` or `mvn install` of a fork | The cloned project used its own `Directory.Build.props` and package versions. MSBuild stops at the first `Directory.Build.props` it finds walking up the folders. |
| Top-level statements in `Program.cs` | `jshell`, a `main` method without a class | For a spike, the whole program is a script, with no class or `Main`. |
