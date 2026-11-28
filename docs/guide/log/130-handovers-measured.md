# Step 130 Â· The handovers measured, and their snapshot-wide parts read once

Card E of the architecture review of 2026-11-28 (`docs/architecture/reviews/2026-11-28.md`), spec #79. Measure first; change the code only if the per-factory handovers take more than about 0.5 s of making an arrangement problem (the owner's threshold, 2026-11-28). They took about 4.8 s, so the change was made. Branch `perf/handovers-per-snapshot`.

## Measured

As step 120 did: curl against the running AppHost, world "Desert Start Cleaned" (72 factories, snapshot `01a21822-â€¦`), `GET /internal/world/arrangements/{run}/problem` for the whole world with default settings. A temporary `Stopwatch` around each factory's `SnapshotHandovers.Of(...).Inputs` in `WorldArrangement.Make` added up the handovers, a second one timed all of `Make`; neither was committed. Each factory's members were read before the handovers' stopwatch started, since `Make` needs them anyway. One run was made for it with `POST â€¦/snapshots/{id}/arrangements` (run `01a22260-bdab-7280-8cb3-22d85e6c2c2b`, approved by the owner). One warm-up call first, then three counted.

| Code | Handovers (s) | All of `Make` (s) | curl, with loading the snapshot (s) |
|---|---|---|---|
| Before, `754f109` | 4.95, 4.81, 4.84 | 6.07, 5.86, 5.87 | 7.06, 6.48, 6.51 |
| After, `0489f90` | 3.64, 3.64, 3.68 | 4.71, 4.81, 4.77 | 5.55, 5.49, 5.42 |

About 1.2 s less in the handovers, and so in making the problem: a fifth of it. Both rows were taken in the main checkout with the AppHost otherwise idle (no solver running).

A first round on `754f109`, taken while the run's own solve was still going and with the three scans also timed on their own, gave handovers of 6.06, 6.19, 6.11 s and the three scans alone 1.77, 1.75, 1.72 s. That's where "over 0.5 s" was first seen; the table above is the fair comparison.

Step 120's 8.4 to 8.9 s for the same call is now about 5.5 s. Card D and #84 (pipe networks) landed in between, so not all of that difference is this step's.

**What's left of the handovers, about 3.6 s, is per factory:** the walk over each factory's inputs in `FactoryHandovers`. The card doesn't cover it, and nobody has looked at it yet.

## Design

- **Module:** `SnapshotFactories` (`World/Factories`), the code home of **Factories in a snapshot**, the one place for "once per snapshot" (BR-W5, BR-W11). It now also holds the three things every factory's handovers read from the whole snapshot: `Sinks`, `EndConsumers` and `BuildingOf` (each buildable's class by its id). Each is a `Lazy<T>`, read the first time it's asked, as `World` already was: `ClaimedMachines` and the arrangement-machines endpoint never ask, so they never pay.
- **Interface changes:**
  - `SnapshotFactories` gains the three properties above.
  - `SnapshotHandovers.Of` loses its `SnapshotDocument` parameter: everything it read from the snapshot now comes from `SnapshotFactories`. Its two callers, `WorldArrangement.Make` and `SnapshotAnalysis` (the factory page), drop one argument.
  - The handovers' domain interface, `FactoryHandovers.Of`, doesn't change (the spec's decision).
- **Deletion test:** delete the three properties and every factory's handovers scan all of the snapshot's buildables three times again, 72 Ã— 3 scans of some 42,000 buildables for the owner's world. They earn their keep. `SnapshotHandovers` stays: it's the one place that puts a factory and its snapshot together for `FactoryHandovers`, for both callers.
- **Glossary:** **Factories in a snapshot** now names what it holds for the handovers. No new term.

## How it was built

No new seam (the spec says so: the change changes no behaviour). The guards were run green first, then after the change, without editing a test: `WorldArrangementTests`, `SnapshotFactoriesTests` and `SnapshotAnalysisTests` (`World.Tests`, 212), `FactoryHandoversTests` (`World.Domain.Tests`, 421), and the test worlds' stored analyses (`World.Simulation.Tests`, 62, no `REGENERATE_ANALYSIS_FIXTURES`; no fixture changed).

**Test run:** `dotnet build SatisfactoryLedger.slnx` 0 warnings, 0 errors; `dotnet test SatisfactoryLedger.slnx` 1373 passed, 0 failed (integration tests included, Docker running); the stored analyses unchanged.

## .NET points

| Here | In Java / TypeScript |
|---|---|
| **A `Lazy<T>` whose function reads a constructor parameter**: `sinks = new(() => IdsOf(snapshot, BuildableKind.Sink));` in the constructor. The lambda captures `snapshot`, so the object keeps the snapshot alive as long as one of the three hasn't been read. A field initialiser couldn't do it: it can't see constructor parameters (a primary constructor's can). | `Suppliers.memoize(() -> idsOf(snapshot, SINK))` in a Java constructor; the capture keeps `snapshot` reachable the same way. |
| **`IReadOnlySet<T>` over a `HashSet<T>`**: the property `public IReadOnlySet<BuildableId> Sinks => sinks.Value;` hands out the set without `Add`. The private `IdsOf` returns `HashSet<T>`, which CA1859 asks of private members. | Java's `Collections.unmodifiableSet(...)` wraps it; .NET's read-only interface is a view of the same object, with no wrapper. |
| **`Stopwatch`**, temporarily: `Start()` and `Stop()` around each factory's call add up in `Elapsed`. Primer: *Backend â†’ `Stopwatch` for a quick timing*. | Guava's `Stopwatch`, or `System.nanoTime()` differences; `performance.now()`. |
| **Aspire's data volume is named after the AppHost's path**: run from a git worktree, the AppHost got a new, empty Postgres, without the owner's world. The measurement was taken in the main checkout. Primer: *Backend â†’ `WithDataVolume()` is named after the AppHost's path*. | Docker Compose names volumes after the project directory the same way. |

## Choices to overrule

- **The three parts are three `Lazy` properties on `SnapshotFactories`**, not one object of their own handed to `SnapshotHandovers`. One record ("snapshot ends") would be a new type with a single reader; three properties keep `SnapshotFactories` the one thing callers pass.
- **`BuildingOf` stays a map of every buildable's class**, as card D left it. The handovers read it to name a building; it's now built once, not removed.
- **The "before" numbers that count are the second round**, on the old code with the AppHost idle, not the first, taken while the solver was busy and with extra scans timed. Both are written above.
- **Run `01a22260-bdab-7280-8cb3-22d85e6c2c2b` is left in the database, still pending.** Its 60-second solve hadn't finished after some 40 minutes, and restarting the AppHost for the second measurement stopped it. It isn't deleted, as the owner asked; deleting it, or asking for a new run, is the owner's choice.
- **The per-factory rest of the handovers (about 3.6 s) isn't touched.** The card is about the snapshot-wide parts only.

**Files:** `src/Modules/World/SatisfactoryLedger.World/Factories/SnapshotFactories.cs`, `Analysis/SnapshotHandovers.cs`, `Analysis/SnapshotAnalysis.cs`, `Arrangement/WorldArrangement.cs`; `GLOSSARY.md`; `docs/guide/primer.md`; `docs/architecture/reviews/2026-11-28.md` (card E's Status line); this log.
