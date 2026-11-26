# Step 111 · One home for what a buildable demands and produces

**Goal:** answer "what does this take and make per minute at 100%" in one place, for machines, generators and drone ports alike. Candidate 1 of the architecture review of 2026-11-26. Branch `refactor/buildable-demand`, from `main` after PR #43.

## Why

`Machine` had `Demand` and `Production`, but a generator kept the same facts in another shape (`Consumer.Generation`). So every caller wrote its own `Machine … / Consumer { Generation: … }` switch: about 13 of them in 10 files. They had drifted apart:

| Copy | Generator's water | Generators at all | Drone ports |
|---|---|---|---|
| `WorldFlow` (the simulation) | when needed > 0 | yes | burn rate passed on |
| `BlockageTrace.Takes` | **no** | yes | **no** |
| `ShortageTrace.Missing`, `BlockageTrace.CannotLeave` | — | **no, machines only** | — |
| `OutputDestinations.Asks` (BR-A18) | yes | yes | **no** |
| `FactoryOutputs.Takes` | even at 0 a minute | yes | no |

## What you decided (2026-11-26)

- **Only the rates at 100%.** What a machine runs at (utilisation), and what to assume without a state, stays with candidate 2 (one reader of the stored analysis).
- **`Demand` and `Production` move up to `Buildable`**, with `Consumes` and `Produces`, keeping the glossary's own words.
- **Drone ports and truck stations are consumers of their fuel too.** A drone port burns fuel only when it has fuel in its slot and drones refuel there: a flight needs at least one port to provide fuel, not necessarily both. Its demand is that fuel at its drones' burn rate, whether it loads or unloads. A truck station, or a port whose drones haven't all flown yet, *consumes* its fuel at a rate that isn't known: `Consumes` lists it, `Demand` stays empty.
- **Zero rates are left out**, as the simulation does. A zero water rate means "uses no water with this fuel", never "starved now" (that's utilisation).
- **The drifts are fixed in this step, each with a failing test first.**
- **In BR-A18, what reaches a drone port is what flows into the belts that end at it**, and "others bring" subtracts what this factory brings any receiver that asks for the item.
- **A fuelled port where no drone refuels** burns nothing in the game but takes leftovers in the model, like a port whose rate isn't known. That changes the simulation and needs a new analysis version (ADR-0017): issue #44, its own step.

### Choices to overrule

- **`abstract`, not `virtual` with an empty default.** Every kind of buildable must say what it demands, or the build fails. That's how drone ports were forgotten before.
- **`Produces` is worked out once from `Production`** on `Buildable`, so the two can't disagree; `Consumes` is its own member, because a truck station consumes without a known rate.
- **A drone port's demand needs exactly one fuel in `Burns`.** The save's fuel slot holds one item, and `DroneRefuelling` computes the rate for that one.
- **BR-A18's "get" has its own rule (`FactoryOutputs.Intake`)** instead of changing `FlowInto`, because `OutputDestinations.Of` also uses `FlowInto` to share flow at splits, and those shares stay exactly as they were.

## How it works

- **`Buildable`** (`Buildable.cs`): `abstract Demand`, `Production`, `Consumes`, and `Produces` from `Production`. `Burning(burns, burnRate)` is the drone-port rule, shared by `Provider` and `Consumer`.
- **`Machine`**: its recipe, as before. **`Consumer`**: a generator from its `Generation` (fuel, water when needed > 0, waste when it leaves any), otherwise a drone port's burning. **`Provider`**: a drone port's burning; `Production` empty, since what it brings in is its `Offer`. **`Logistics`**: nothing.
- **The callers** ask `buildable.Demand` and `buildable.Production` and no longer switch:
  - **Domain:** `BlockageTrace`, `ShortageTrace`, `FlowReadings`, `OutputDestinations`, `FactoryDefinition` and `WorldFlow`.
  - **API side:** `FactoryInputs`, `FactoryOutputs` and `SnapshotAnalysis.OnStock`. `FactoryOutputs.Makes` and `Takes` are gone.
- **The stock rule (BR-A17)** now gets a generator's full demand, fuel and water. It checks each input against what's held, and a generator holds only fuel, so nothing changes.

## C# points

| Here | In Java / TypeScript |
|---|---|
| **`public abstract IReadOnlyDictionary<ItemId, Rate> Demand { get; }`** on the base, **`public override … Demand { get; }`** set in the constructor. Leaving it out of `Provider` doesn't compile. | An abstract getter in Java. A `virtual` one with a default is Java's `default` method: it compiles when forgotten. |
| **`private protected static`** helpers (`Burning`, `NoRates`): visible to subclasses in this assembly only. | A `protected static` in a sealed hierarchy; Java has no "subclass *and* same package" level. |
| **`Consumer { Generation: not null } generator`**: a property pattern that only tests, binding the whole object. | `instanceof Consumer c && c.generation() != null`. |
| **A name lookup surprise:** `Catalog` inside `namespace SatisfactoryLedger.World.Tests` is the namespace `SatisfactoryLedger.Catalog`, not the `using static` field. And inside `FlowReadings`, `Machine` is its method, not the type. | Neither happens in Java: no namespaces to walk up, and methods and types live apart. |
| **`x?.Consumes ?? []`** doesn't compile: the collection expression has no target type. | TypeScript's `?? []` is fine. |

All three surprises are in `primer.md`, under .NET basics.

## Tests

Written first. The behaviour tests failed with 0 where the rate belongs, and the rate tests failed on compile (`Buildable` had no `Demand`):

- **`BuildableRatesTests`** (8):
  - A generator asks its fuel and its water at full power.
  - One that needs no water asks only its fuel.
  - A generator makes its waste.
  - One whose fuel isn't known asks and makes nothing in particular.
  - A drone port that loads, and one that unloads, ask the fuel its drones burn.
  - A truck station consumes its fuel at a rate that isn't known.
  - A piece of logistics asks and makes nothing.
- **`BlockageTraceTests`** (3):
  - Too few consumers counts the water a generator takes.
  - Too few consumers counts the fuel a drone port burns.
  - A blocked generator counts the waste it cannot get away.
- **`ShortageTraceTests`** (1): a starved generator counts the fuel it lacks.
- **`OutputDestinationsTests`** (1): a drone port asks for the fuel its drones burn.
- **`FactoryOutputsTests`** (1): a drone port its belt brings all the fuel its drones burn is short of nothing. A small world, a packager making packaged turbofuel for a port. It failed with short 1.6667: the port's need counted, its intake didn't.

The existing tests stayed unchanged. `dotnet test SatisfactoryLedger.slnx`: 1,266 passed, exit code 0. The web app is untouched: no contract changed.

## Files

- **Domain:**
  - `Buildable.cs`, `Machine.cs`, `Consumer.cs`, `Provider.cs`, `Logistics.cs`
  - `FactoryDefinition.cs`, `OutputDestinations.cs`
  - `Flow/WorldFlow.cs`
  - `Tracing/BlockageTrace.cs`, `ShortageTrace.cs`, `FlowReadings.cs`
- **API side:** `Analysis/FactoryInputs.cs`, `FactoryOutputs.cs`, `SnapshotAnalysis.cs`
- **Tests:**
  - `BuildableRatesTests.cs`, `OutputDestinationsTests.cs`
  - `Tracing/BlockageTraceTests.cs`, `ShortageTraceTests.cs`
  - `Items.cs` (Battery)
  - `World.Tests/FactoryOutputsTests.cs`
- **Docs:**
  - `glossary.md` (Demand / Production)
  - `business-rules.md` (BR-A16, BR-A18, BR-A20, BR-A21)
  - `primer.md`
