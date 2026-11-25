# Step 99 · Fakes checked, the MCP against the real API, and an unreachable API

**Goal:** close the integration gaps found after step 98: the MCP's fake API answers and the Assistant's fake MCP tools were never checked against what they stand in for, two endpoints had no HTTP test, and nothing tested the analysis service when the API can't be reached. Branch `test/integration-contracts`, from main. Everything runs on every PR, in the backend job.

## What was built

- **The MCP's fake API checked against `api/openapi.json`** (`tests/SatisfactoryLedger.Mcp.Tests/ApiDescription.cs`):
  - `FakeLedgerApi.Answer` refuses an answer the real API couldn't give: a path it doesn't have, a status it doesn't answer there, or a body that doesn't fit its schema.
  - It uses **JsonSchema.Net 9.7.0**, a test dependency only. `openapi.json` is copied next to the tests.
  - The check reports only the parts that really fail: a `oneOf` that holds (a position that isn't null) has a failing branch, which isn't wrong.
  - **It found four answers the API had outgrown:**
    - a snapshot's `counts` (missing `providers`, `consumers` and `connections`; it had a `buildables` the API never sends);
    - the analysis's `pipeFlows` and `generatorSummary.fuelUnknown`;
    - a factory trace's machines without `className` and `recipe`;
    - the trace starts' `moreCapacity`.

    All four fakes are fixed. The check doesn't catch a field the fake has and the API doesn't, as with the web recording (step 96).
- **The MCP server against the real API** (`Api.IntegrationTests/Mcp/LedgerMcpApiTests.cs`):
  - The test world is imported and analysed. The MCP server runs in memory, its ledger client pointed at the API in memory, and a real MCP client calls its tools.
  - Three tests: `list_snapshots`, `get_findings_summary`, and `trace_upstream` of the starved rod constructor.
- **The trace endpoints over HTTP** (`World/WorldTraceApiTests.cs`): a shortage traced from the rod constructor; smelters with nothing to trace; an unknown snapshot is 404; a trace needs a building (400).
- **The analysis service when the API can't be reached** (`World/AnalysisServiceReachabilityTests.cs`):
  - `LedgerApiFactory.UnreachableCalls` makes the service's next calls fail.
  - One failed call: the World client's own retries cover it.
  - **Longer: the snapshot stayed pending.** The message went to RabbitMQ's error queue after the client's retries, though the handler's comment said Wolverine would retry it.
  - **The owner chose Wolverine retries:** after 5 s, 30 s and 2 min, then every 5 minutes for an hour, only for failed calls to World. ADR-0018 is amended.
- **The Assistant's fake MCP tools checked against the real MCP server** (`tests/SatisfactoryLedger.Assistant.Tests`):
  - `RealLedgerTools` derives the schema of the real trace tools' answer (`TraceAnswer`) with .NET's `JsonSchemaExporter`. It makes the schema strict: a field is required unless it can be null (the server leaves nulls out), and no field the type doesn't have. `FakeLedger.Answer` refuses an answer the real tool couldn't give.
  - `FakeLedgerToolsTests`: each fake tool takes the same parameters, by name, type and whether required, as the real MCP server's tool of its name. A renamed parameter made it fail, as it should.
  - It found the fake traces' path steps without `ids`, which the real tool always sends. Fixed.
  - The MCP project lets the Assistant's tests see its answer types (`<InternalsVisibleTo>`), as the Catalog and World modules do for their tests.

## .NET points

- **`DelegatingHandler`** wraps the HTTP handler under the resilience pipeline. It's like an OkHttp interceptor, or a Spring `ClientHttpRequestInterceptor`. The test's `Reachability` handler is a `file`-local class next to the factory it serves.
- **Wolverine's error policies** (`OnException(...).RetryWithCooldown(...).Then.ScheduleRetry(...)`) are like Spring Retry's `@Retryable` with a back-off, set once for every handler. `RetryWithCooldown` waits in the handler, and `ScheduleRetry` puts the message back for later.
- **`JsonSchemaExporter.GetJsonSchemaAsNode(options, type)`** (System.Text.Json, .NET 9+) gives the JSON Schema of a type as the serializer would write it. It's like Jackson's `jsonSchema` module or springdoc's model schemas, but built into the runtime.
- **Two layers of retry:** the HTTP client's (Polly, seconds) and the message's (Wolverine, minutes to an hour). A short blip never reaches the message layer.

## Tests

- All 1,234 .NET tests pass, with no build warnings. `api/openapi.json` is unchanged.
- The longer-outage test failed before the retry policy (timed out after a minute) and passes with it.
