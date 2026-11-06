# ADR-0017: A snapshot's analysis is stored, and computed in the background after import

- **Status:** Accepted
- **Date:** 2026-11-06

## Context
The flow analysis of a snapshot (BR-A1 to A14) takes seconds on a big world: about 9 s for the owner's 42,000 buildables, after an import of about 13 s. The world's findings and every factory's findings come from the same simulation. A snapshot never changes (ADR-0016), so its analysis only changes when the analysis rules do. A web app and a WPF app will show findings, and shouldn't wait seconds on every view.

## Decision
We will store the result of a snapshot's simulation, and compute it in the background after the import:
- **One row per snapshot** in `world.snapshot_analyses`:
  - its **status**: *pending*, *ready* or *failed*, with the error when it failed;
  - the **analysis version** it was computed with;
  - when it was computed and how long it took;
  - the result as one `jsonb` document.
- **The document holds the simulation's result, not a response:** per machine its state; the bottlenecks, unused supply and sources of unknown rate; the parts that didn't settle; and what flows into every belt, lift and pipe. The world's findings and a factory's findings (BR-A14) are both made from it, so a factory defined later needs no new simulation.
- **The import** stores the snapshot and a *pending* analysis in one transaction, then hands the snapshot's id to a background worker over an in-process queue. The import returns as before.
- **A background worker** (a hosted `BackgroundService`) takes ids from the queue one at a time and runs a plain handler (ADR-0010) that computes and stores the analysis.
- **At startup**, the worker queues every analysis that is still pending, failed for a reason that may be gone (a restart), or computed with an older analysis version. Raising the version re-analyses every snapshot.
- **The analysis endpoints** answer *202 Accepted* with the status while it's pending, the findings when it's ready, and a problem (500) when it failed. The snapshot itself says its analysis status.

## Alternatives considered
- **On the first request:** simplest, but the first viewer waits about 9 s, and two viewers at once would compute it twice.
- **During the import:** always there, but the upload takes about 22 s instead of 13 s for a big world.
- **Storing the finished world response:** smaller, but a factory's findings need the flows into its output belt and the states of its machines. The factory would need a new simulation.
- **A message broker or a separate worker process (like Spring Batch or a queue):** more moving parts than a modular monolith with one user needs. The in-process queue loses nothing on a restart, because the database says what's pending.

## Consequences
- Viewing findings is fast; importing is as fast as before.
- A snapshot can be *pending* for a while, so clients have to show that and ask again.
- The analysis version must go up when a change to the analysis rules changes results. Otherwise stored analyses are stale.
- Only one analysis runs at a time. That's fine for one player; more workers can read the same queue later.
- The stored document is a contract with ourselves, versioned like the snapshot document.
