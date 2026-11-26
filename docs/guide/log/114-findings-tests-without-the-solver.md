# Step 114 · Findings tests without the solver

**Goal:** issue #47. The World module's tests stop running the solver: the findings are tested on a stored analysis, read from a fixture or built by hand. `World.Tests` no longer references `World.Simulation`. Branch `test/findings-without-solver`, from `main` after PR #48.

## Design

The first step under the new **Design** section of `CLAUDE.md`.

- **Module:** the findings (`SnapshotAnalysis.For` / `ForFactory`, and `FactoryInputs` and `FactoryOutputs` behind them). Its interface takes a snapshot and the stored `AnalysisDocument` (ADR-0017). That interface is the test surface; the tests now feed it a document instead of a solver run.
- **Seams under test** (agreed in Q1–Q2):
  - the findings, through `SnapshotAnalysis` with a stored analysis. They catch how a finding reads and groups an analysis; they miss whether the solver gets there;
  - the simulation, end to end (save → solver → finding), in `World.Simulation.Tests`. Those catch what the solver decides.
- **Two adapters, so a real seam:**
  - the stored analysis comes from the solver in production;
  - in tests it comes from a fixture or from `TestAnalysis`.
- **Interface:** unchanged. No production code changed, except one `InternalsVisibleTo` for `World.Simulation.Tests`.
- **Deletion test:** delete the fixtures, and nine test files would need the solver again. So they earn their keep.

## What you decided (2026-11-26)

- **Q1:** the findings tests hand-build an `AnalysisDocument`. That also covers the adapter (`AnalysisReadings.Of`), and goes through the findings' own interface.
- **Q2:**
  - a test of what the solver decides moves to `World.Simulation.Tests` as an end-to-end test;
  - a test of what a finding makes of an analysis stays, on a stored analysis;
  - you approved the classification, test by test, before anything moved.
- **Q3:** the test world's analysis becomes a golden fixture, with a guard test.
- **Q4:** `FlowNetworkDocumentTests` and `TestWorldFlowTests` move to `World.Simulation.Tests`.
- **Q5:** an architecture test keeps the simulation the analysis service's alone.

### Choices to overrule

- **A second golden fixture**, for the world saved with generators (`Generators.sav`). It's Q3 applied to the second shared test world. `GeneratorFactoryTests`, the listing half of `GeneratorFindingsTests`, and one output test read it.
- **The fixtures are indented JSON, compared as documents**: read and written again, then compared as text. So formatting can't fail the guard, and a regeneration's diff shows what the solver changed.
- **`TestWorldSnapshot.cs` is linked into `World.Simulation.Tests`**, not moved into a shared test library. One file wasn't worth a project.
- **The reference rule reads the `.csproj` files.** Test projects aren't in the assemblies the ArchUnit rules load, so this one checks project references directly, as the layout rule checks namespaces.

## Where each test went

| Test | Now |
|---|---|
| `TestWorldFlowTests`, `FlowNetworkDocumentTests` | moved to `World.Simulation.Tests` (`git mv`) |
| Generators: nuclear and coal at full power, fuel generator starved by its packager, blocked machine names every product, starved generator names its fuel | `FindingsEndToEndTests` in `World.Simulation.Tests` |
| Stock: holding less is starved, starved names its input | `FindingsEndToEndTests` |
| Stock: holding half an hour **runs at full speed** | `FindingsEndToEndTests`, the solver's part of a split test |
| Stock: running on stock says so, with how long it lasts | `StockFindingsTests`, hand-built: the finding's part of the split test |
| Stock: belts bring all it uses, so not on stock | `StockFindingsTests`, hand-built |
| The 9 files on the test world's analysis | read `Fixtures/TestWorldAnalysis.json` |
| `GeneratorFactoryTests`, generator listings, "a generator it feeds would take its fuel" | read `Fixtures/WithGeneratorsAnalysis.json` |
| `FactoryInputsTests` (5 small worlds), `FactoryOutputsTests` (3), `DronePortFindingsTests`, "fuel unknown" | hand-built with `TestAnalysis` |

Each hand-built analysis says in a comment what it stands for, such as "the merger takes 15 a minute from each miner". The tests' expected values didn't change: they come from the scenarios, as before.

## How it works

- **`TestAnalysis`** (`World.Tests`): a builder of the stored document.
  - It sets only what a test needs: `Running`, `Starved`, `Blocked`, `Into` and `Unused`.
  - `Build()` writes the document to JSON and reads it back, as the database does.
- **`TestWorldSnapshot`:**
  - `TestWorldAnalysis` and the new `WithGeneratorsAnalysis` read their fixture, lazily (`Lazy<T>`).
  - A missing fixture throws with the command to write it, instead of breaking the whole class.
- **`AnalysisFixturesTests`** (`World.Simulation.Tests`):
  - runs the solver on both test worlds and compares the result with the fixtures;
  - with `REGENERATE_ANALYSIS_FIXTURES=1` it writes them into the source tree instead, found with `[CallerFilePath]`.
  - I checked that it fails: changing one utilisation in a fixture makes it fail, with its message.
- **`SourceLayoutTests.Only_the_analysis_service_and_the_simulations_own_tests_reference_the_simulation`:**
  - lists the projects whose `.csproj` references `World.Simulation`: the analysis service, these architecture tests, and the simulation's own tests;
  - I checked it fails when `World.Tests` references it again.

## C# points

| Here | In Java / TypeScript |
|---|---|
| **`[CallerFilePath] string file = ""`**: the compiler passes the caller's source path. | Nothing built in; a system property or a path passed in. |
| **`<Compile Include="..\SatisfactoryLedger.World.Tests\TestWorldSnapshot.cs" Link="TestWorldSnapshot.cs" />`**: one source file in two projects. | An extra source root in Gradle or Maven. |
| **`Lazy<AnalysisDocument>`** instead of a static initialiser: a failing initialiser would make the whole class unusable (`TypeInitializationException`). | `ExceptionInInitializerError`; the holder-class idiom. |
| **Content items with `LinkBase`**: the fixtures are copied next to the test binaries of both projects, from one folder. | Test resources on the classpath. |
| **`TheoryData<string>` with `[MemberData]`**: a theory's rows from a method, so they can be constants of another class. | JUnit 5's `@MethodSource`. |

New in `primer.md` (Testing):
- **A golden file, guarded**
- **`[CallerFilePath]`**
- **A linked source file**
- **`Lazy<T>` against a static initialiser**

## Tests

This step moves tests rather than adding behaviour, so there was no red first for the moves. The new guard and architecture tests were each made to fail on purpose, then passed. Every moved or rewritten test kept its expected values.

Results:
- `dotnet test SatisfactoryLedger.slnx`: 1,298 passed, exit code 0. That's 1,294 before, plus the two guards, the reference rule, and the stock test split in two.
- One earlier run ended "Failed!" with only 1,175 tests and none marked failed: a test project didn't finish. Three runs since have passed in full.
- `World.Tests` no longer references `World.Simulation`.

## Files

- **Module:** `SatisfactoryLedger.World.csproj` (`InternalsVisibleTo` for `World.Simulation.Tests`)
- **`World.Tests`:**
  - `TestAnalysis.cs` (new), `Fixtures/TestWorldAnalysis.json`, `Fixtures/WithGeneratorsAnalysis.json` (new)
  - `TestWorldSnapshot.cs`, the `.csproj`
  - `FactoryInputsTests.cs`, `FactoryOutputsTests.cs`, `DronePortFindingsTests.cs`
  - `GeneratorFactoryTests.cs`, `GeneratorFindingsTests.cs`, `StockFindingsTests.cs`
- **`World.Simulation.Tests`:**
  - `AnalysisFixturesTests.cs`, `FindingsEndToEndTests.cs` (new)
  - `TestWorldFlowTests.cs`, `FlowNetworkDocumentTests.cs` (moved)
  - the `.csproj`
- **`ArchitectureTests`:** `SourceLayoutTests.cs`
- **Docs:**
  - `CLAUDE.md`: the regeneration command
  - ADR-0017: tests read stored analyses
  - `primer.md`
