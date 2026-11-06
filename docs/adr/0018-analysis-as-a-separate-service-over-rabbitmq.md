# ADR-0018: The analysis runs in a separate service, triggered over RabbitMQ

- **Status:** Accepted
- **Date:** 2026-11-06
- **Amends:** [ADR-0017](0017-snapshot-analysis-stored-and-computed-in-the-background.md): the storage stays, the in-process worker goes.

## Context
ADR-0017 stores each snapshot's analysis, computed by a worker in the API process. The analysis is a clear, separate job:
- one event starts it (a snapshot was imported);
- its input and output are documents;
- it takes seconds of CPU on a big world.

The project is also a learning project. Messaging and a second service, next to the modular monolith (ADR-0002), are worth seeing in a real case.

## Decision
We will run the analysis in its own service, `SatisfactoryLedger.AnalysisService`, triggered by a message over RabbitMQ:
- **World keeps the data.** Snapshots, factories, stored analyses (ADR-0017) and the findings API stay in World. The service owns no database: it's a stateless compute service.
- **The flow.**
  1. The import publishes `SnapshotImported { SnapshotId }`.
  2. The service fetches the snapshot's **flow network** from World's API.
  3. It simulates the network and sends the **analysis document** back to World's API, or the reason it failed.
  4. World stores it as before.
- **Messages stay small.** The documents (megabytes on a big world) go over HTTP, not through the broker (the *claim check* pattern).
- **Wolverine** (MIT) with its RabbitMQ transport. The import publishes through Wolverine's **transactional outbox** on PostgreSQL: the snapshot, its pending analysis and the message are stored in one transaction. A crash can't store a snapshot and lose its message.
- **The service-facing endpoints** live under `/internal/world/…`, outside the public OpenAPI contract. They have no authentication yet, because everything runs locally under Aspire. A real deployment would secure them, for example with a client certificate or a service token.
- **The shared code** is a library both use, `SatisfactoryLedger.World.Simulation`: the message, the flow network document, the analysis document, and the computation. It depends only on `World.Domain`.
- **At startup**, World still finds every snapshot without a current analysis (ADR-0017), and publishes `SnapshotImported` for each.
- **Aspire** starts RabbitMQ, the API and the service, and connects them. The service finds the API by service discovery.

## Alternatives considered
- **Keep the in-process worker (ADR-0017):** simpler, and enough for one player. It loses the showcase this decision is for.
- **MassTransit:** the most used .NET bus, but v9 is commercial and v8 only gets fixes until the end of 2026.
- **Plain RabbitMQ.Client:** shows the broker's basics without a framework, but the outbox and retries would be ours to write and test.
- **The service owns the analyses:** more independent, but a factory's findings also need World's factory definitions. That's more API calls or a second event stream.
- **The result as a message:** `AnalysisCompleted` with the 2.4 MB document would work, but large messages slow the broker and make retries heavy.
- **Sending the snapshot document instead of the flow network:** the service would then have to know the save format, the Catalog copy and the adapter's rules. The flow network is the narrower contract.

## Consequences
- Running the app needs RabbitMQ; Aspire starts it. The integration tests need it too: a RabbitMQ container, and the service running next to the API.
- Two deployables share one library. A change to the flow network or analysis document format is a change to both, and is versioned like the documents already are.
- The analysis version (ADR-0017) still decides when stored analyses are stale. It now lives in the shared library.
- One analysis at a time per service instance. More instances can listen on the same queue later.
