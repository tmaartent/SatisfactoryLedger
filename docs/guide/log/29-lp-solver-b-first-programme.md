# Step 29b · The LP solver, part B: OR-Tools and the first programme

**Goal:** one linear programme per network, solved with OR-Tools' GLOP. It covers machines, capacities, providers, sinks, sources of unknown rate, stock and loops, but none of the sharing rules of splitters and mergers yet (ADR-0026). Not wired in: the sweep still computes every analysis. Branch `feat/lp-solver`.

## The programme

```
variables   f(e, i) ≥ 0     per connection e and item i that can reach it
            u(m) ∈ [0, 1]   per machine with a recipe

machine     Σ f(in, i) = demand(i) × u        (≤ for a stocked input, BR-A17)
            Σ f(out, p) = production(p) × u   (no line of p's form → u = 0, BR-A3)
logistics   Σ f(in, i) = Σ f(out, i)          per item
            Σ f(in, ·) ≤ capacity             belts, lifts, pipes
provider    Σ f(out) ≤ offer
sink, unknown source: free

maximise    Σ u(m) + Σ provider deliveries − 10⁻⁶ × Σ f
```

- **Which items can reach a connection** (`ItemsOnConnections`) is worked out before the LP. Items start at a provider, a machine's products, or what a source of unknown rate is asked for, and spread along logistics. They go only where the connection carries their form, and into a machine only if it asks for them. A variable exists only for those pairs, so solids never go over pipes, with no constraint needed for that.
- **The tiny penalty on every flow** stops items circling a loop of belts for nothing. An LP has no reason to keep a flow at zero when it costs nothing.
- **Loops:** "maximise Σ u" is what makes the Ficsonium encoder and collider run at 100%. The sweeps can't prefer the top of a balanced loop. A loop that gives back too little (nitric acid, half the water back) can only balance at 0, so that's where it ends up.

## Files

| Where | What |
|---|---|
| `Directory.Packages.props`, `World.Simulation.csproj` | `Google.OrTools` 9.15.6755, only here |
| `World.Simulation/ItemsOnConnections.cs` | The items each connection can carry |
| `World.Simulation/LinearSteadyState.cs` | The programme, built and solved |
| `World.Simulation/LinearFlow.cs` | What it found: flows per connection, utilisation per machine |
| `tests/…World.Simulation.Tests/` | Back: `LinearSteadyStateTests`, `LinearFlowQueries`; `TestFlow.cs` and `Items.cs` linked from the domain tests |
| `World.Domain.Tests/TestFlow.cs` | `Network()`: the built network, for a solver other than the sweeps |
| `ArchitectureTests/ModuleBoundaryTests.cs`, `SolutionAssemblies.cs` | Only World.Simulation uses OR-Tools, and `LinearSteadyState` does |

## Points worth noticing

| .NET / C# | Java | Here |
|---|---|---|
| OR-Tools' .NET API is generated (SWIG) around C++, so `Solver` is `IDisposable`: `using Solver solver = …;` frees it when the method ends | Its Java API (`MPSolver`) is generated the same way; you'd call `delete()` or use try-with-resources | `LinearSteadyState.Solve`. In the primer |
| The API isn't in the package's XML docs, so it was verified by compiling against 9.15.6755 | — | As CLAUDE.md asks: the installed version, not the docs site |
| A test file compiled into a second project (`<Compile Include=… Link=…>`) | `testFixtures` (Gradle) or a test-jar | `TestFlow` serves both solvers. In the primer |
| Native runtimes per platform come as separate packages (`Google.OrTools.runtime.win-x64`, …) | Classifier jars | Restoring them took longer than the default timeout the first time |

## Tests (17, one behaviour each)

Machines at 100% · what flows into a belt is what its inputs bring · half the input, half speed · output with nowhere to go stops · the scarcest of two inputs decides · a provider delivers only what's taken · a belt carries at most its capacity · a slow output belt holds a machine back · a stocked input keeps it running · and still takes what its belt brings · each product over the line of its form · a fluid product without a pipe blocks · a loop that gives back all it needs runs at 100% · one that gives back too little stops · **the encoder and collider at 100%** · items circling in a loop are conserved · a source of unknown rate supplies what's asked, and no fluid over a belt.

Red first: the project didn't compile until `LinearSteadyState` existed. All 17 then passed on the first run. Two architecture rules were added. **Totals:** 773 .NET tests.

## Not yet, by design

- **Sharing** (BR-A4, A5): today the LP may send a splitter's items to any output. Part C adds the sequence: a level per splitter and merger, with the capped outputs and inputs corrected each round.
- **Priorities** (priority mergers, overflow, uploaders, fuel that takes only leftovers), **clogging** (a belt bringing an item a machine doesn't ask for), and a source of unknown rate sending **in proportion** to what's asked.
- **Findings** (starved of, blocked on, bottlenecks, unused supply) and the `FlowResult` they go into: part D.
- **Parts:** this solves the whole network at once; `PartsOf` comes back with the findings.

## Choices worth a second look

1. **The objective's weights:** one per machine's utilisation, one per item a provider delivers. Raw numbers in different units. That doesn't matter without sharing rules, because nothing competes yet. Part C tunes it against the splitter tests.
2. **Sources of unknown rate aren't rewarded** for delivering, only pulled by machines. Otherwise a station feeding a sink would deliver without limit.
