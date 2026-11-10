# Step 29a · The LP solver, part A: what the services share, and where the solver goes

**Goal:** prepare the linear-programme solver (ADR-0026). The wire documents move to `World.Contracts`; `World.Simulation` becomes the project that solves. No change in results: the sweep solver still runs. Branch `feat/lp-solver`.

## What you decided

- **A sequence of LPs**, not progressive filling. Global max-min breaks the game's even split: source 10 → S1 → (sink a, S2 → sinks c, d) gives a = 3.33 instead of 5. Each LP fixes which splitter outputs and merger inputs are capped, then the guess is corrected until it stops changing.
- **Google OR-Tools (GLOP).**
- **The library stays out of the domain,** as SatisfactorySaveNet stays in `World.SaveFile`. `World.Simulation` keeps its name and becomes the solver.
- **The documents go to `World.Contracts`** (folded into this step).
- **The sweep is deleted** once the LP matches it, so the work stays on a branch until then.

## What moved

| Before | Now |
|---|---|
| `World.Simulation`: 21 documents, the message, the JSON settings, and `Compute` | `World.Contracts/Analysis/`: the documents, `SnapshotImported`, `AnalysisMessaging`, `AnalysisResult`, `SimulationJson`. Plain records |
| `FlowNodeDocument.ToNode()` and `FlowNetworkDocument.ToNetwork()` | `World.Simulation/DocumentToNetwork.cs` (`ToNetwork()`, an extension member) |
| `FlowNodeDocument.From` and `FlowNetworkDocument.From` | `World/Analysis/NetworkToDocument.cs` (`FlowNetworkDocument.From(…)`, a *static* extension member) |
| `AnalysisDocument.Compute` | `World.Simulation/NetworkAnalysis.cs` (also a static extension: callers unchanged) |
| The domain's `Carries`, `MachineLimit` and `SortRuleKind` in the documents | Copies in `World.Contracts/Analysis/`, mapped by name at both edges. JSON is unchanged: enums are written as names |
| `World` referenced `World.Simulation` | It no longer does: only the analysis service (and `World.Tests`, for the test world's analysis) |
| `World.Simulation.Tests` | The round-trip tests need both sides now, so they're in `World.Tests/FlowNetworkDocumentTests.cs`; the project is gone until the LP tests bring it back |

The findings (`MachineFinding`, `GeneratorFinding`) now use the wire `MachineLimit`: they're read from the stored document.

## Points worth noticing

| C# / .NET | Java | Here |
|---|---|---|
| **Static extension members** (C# 14): `extension(AnalysisDocument) { public static … Compute(…) }` | No equivalent | Callers keep `AnalysisDocument.Compute(…)` after the record moved. In the primer |
| A `using` alias wins over namespace imports | Write the full name | Tests that see both `MachineLimit`s. In the primer |
| Wolverine names a message by its type's full name | Spring Cloud Stream: a type header | `SnapshotImported` changed namespace: a message left in the outbox from before isn't delivered. The startup requeue covers it (ADR-0026) |

## Tests

No new behaviour, so no new tests. **754 of 754 pass.**

Your commit `d790d56` had moved `ServiceDefaults/Extensions.cs` to the namespace `SatisfactoryLedger.ServiceDefaults`. `SourceLayoutTests.Every_namespace_follows_its_project_and_folder` expects Aspire's template namespace `Microsoft.Extensions.Hosting` there. Its namespace is back to the template's, and the two `using SatisfactoryLedger.ServiceDefaults;` lines went with it. Your rewrite into `extension(IHostApplicationBuilder)` blocks stays. Your `Flow/PartResult.cs`, which `Solver.cs` needs, is in this commit too.

## Next (part B)

Add OR-Tools to `World.Simulation`, with an architecture test that only it references the library. Then the first LP: recipes, capacities, conservation, providers and sinks, with no sharing rules yet. Its tests come from the first `SteadyStateTests` cases, and a balanced loop running at 100%.
