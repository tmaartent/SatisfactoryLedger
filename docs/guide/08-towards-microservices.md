# 08 · Towards microservices

Splitting off a service is the **last** milestone for a reason. Good boundaries come first, and distribution is the optional reward. In an interview, "I can show you the boundary, the outbox, and the ADR explaining why and how I extracted Catalog" beats "I have six services".

## Step 1: asynchronous communication inside the monolith

Introduce this only once there's a real need. The first real need is already in the rules: **BR-F6/F7. A blueprint edit must flag or unflag the factories built from it, and hand them new inputs and outputs once it's balanced again.** That crosses from BlueprintDesign to FactoryNetwork, so it should not be one transaction.

- The Blueprint module publishes an **integration event** (`BlueprintOutputsChanged`, defined in `BlueprintDesign.Contracts`). That's different from the internal domain event: it's a stable public contract with only the data consumers need.
- **Transactional outbox:** the event is stored in the module's schema in the *same transaction* as the aggregate change. A background dispatcher delivers it. This prevents "saved but never published" and "published but rolled back".
- Consumers are **idempotent**: handling the same event twice has no extra effect (store the processed message IDs).
- In-process first. Libraries such as **Wolverine** or **MassTransit** (check its licensing) provide outbox, retries and an in-memory transport, and later a real broker without changing your handlers.

## Step 2: extract Catalog

Why Catalog: it's read-mostly, has a narrow contract (`ICatalogQueries`), no other module writes to it, and it already has its own schema.

1. Write the ADR: the reason (a showcase, independent release with game updates), the risks, and the rollback plan.
2. Create `SatisfactoryLedger.Catalog.Service` (an ASP.NET Core host) that hosts the Catalog module unchanged. Thanks to ADR-0003, that's mostly composition code.
3. In the monolith, replace the in-process `ICatalogQueries` with an **HTTP client implementation** of the same interface (typed `HttpClient`, Aspire service discovery, resilience from ServiceDefaults). Consumers don't change at all, which shows the boundary was right.
4. **Cache** catalog data on the consumer side. It changes only on import, so a `CatalogImported` integration event over a broker (RabbitMQ via Aspire) can invalidate the cache.
5. Give the catalog its own database (or at least its own connection string). No shared tables.
6. Aspire AppHost: two projects, a broker, two databases, all visible in the dashboard with **distributed traces across the service call**. That screenshot belongs in your README.

## What to be able to explain afterwards
- Why Catalog and not FactoryNetwork (data ownership, write patterns, coupling)
- What happens when the Catalog service is down (cache, degraded mode, resilience policies)
- Consistency model: blueprints referencing a recipe that a new import removed
- What you'd do differently in a team or organisation (Conway's law, independent deploys)

## Pitfalls
- Shared database between services (a distributed monolith).
- Synchronous call chains across several services for one request.
- Splitting before the module boundary has been stable for a while.
