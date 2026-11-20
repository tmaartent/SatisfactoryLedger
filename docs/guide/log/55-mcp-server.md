# Step 55 · The MCP server

**Goal:** AI enhancement 1. Let a model investigate a world through tools, so *"why are my plate constructors at half speed?"* is one question, not an afternoon of scripts (asked 2026-11-20). Branch `feat/mcp-server`, uncommitted.

## What you decided

- **Its own project in the AppHost,** speaking MCP over HTTP, reading the API through service discovery. Not a stdio tool, not inside the API.
- **All four tool groups:** snapshots and findings, find a machine, tracing up and downstream, Catalog lookups. Tracing needs new World endpoints, so it's the next step (56). This step builds the three that run over endpoints there already.
- **Read-only.** No tool imports, defines, edits or deletes anything.

Written down in [ADR-0029](../../adr/0029-mcp-server-over-the-public-api.md).

## The tools

| Tool | What it answers |
|---|---|
| `list_snapshots` | The imported saves, newest first: id, world, when saved, whether the findings are ready. |
| `get_findings_summary` | *Start here.* The machine counts, and the groups of machines losing the most output: building, recipe, status, how many, their average utilisation, **machines' worth lost**, and the items they're short of or blocked on (shares in %). Also how many bottlenecks, unused and unknown supplies there are. |
| `list_findings` | One kind in detail: `bottlenecks`, `unusedSupply` or `unknownSupply`, the biggest first. |
| `list_factories` | Your factories: id, name, machine count, traceable or not. |
| `get_factory_findings` | One factory, by name or id: what it takes in and makes (now, at full speed, short), its power, and its groups losing output. |
| `find_machines` | Machines that make an item, are a building, or have a status; the slowest first, with where they stand in metres. |
| `find_items` | Items whose name holds the words asked. |
| `get_recipes` | The recipes for an item: building, inputs and outputs per minute; alternates optional. |
| `get_transport_tiers` | Belt, lift and pipe tiers with their capacity. |

Leave out the snapshot and a tool uses the newest.

## How it's built

- **`SatisfactoryLedger.Mcp`**, an ASP.NET Core app, with the official C# SDK `ModelContextProtocol.AspNetCore` 2.4.0 (checked in the 2.4.0 package, not on GitHub's main branch). `LedgerMcp.AddLedgerMcp` sets it up, as `AddAnalysisService` does for the analysis service: Program.cs and the tests share it.
- **`LedgerApi/`:** `LedgerClient`, a typed `HttpClient` on `https+http://api`, and records for only the fields the tools use. An answer 202 ("still being analysed") or 404 becomes an `McpException`, whose message the model sees.
- **`Catalog/GameData`:** the Catalog's names, both ways. `Desc_IronPlate_C` ↔ *Iron Plate*. Asked for a name that isn't clear (*iron*), it lists the likely ones. `GameDataSource` caches it for five minutes.
- **`Answers/`:** what the model reads, named with their units (`carriesPerMinute`, `utilisationPercent`, `positionMetres`), with empty fields left out. `MachineFacts` groups the machines held back and words a share as the site does: whole percent, never 100% for less than all.
- **`Tools/`:** four classes, one per group. Each tool has an explicit name, `ReadOnly = true`, and a description written for the model: when to call it, what it gives, the units.
- **Server instructions:** one paragraph the client passes to the model when it connects: start with the summary, the units, use in-game names, check against `get_recipes` rather than recalling numbers.
- **The AppHost** starts it as `mcp` after the API, on port 5280. **`.mcp.json`** at the repository root points Claude Code at `http://localhost:5280/mcp`.

## Tests

`tests/SatisfactoryLedger.Mcp.Tests`, 19 tests, written first and seen failing (no server, then the JSON options). They run the **real protocol**:
- the server starts in memory (`UseTestServer`);
- a real `McpClient` connects to it over HTTP and calls tools as Claude Code would;
- the API is a `FakeLedgerApi` answering paths with JSON in the API's response shapes, for a small made-up world (`IronWorld`: ore, ingots, plates; constructors starved, blocked and without a recipe).

They check:
- the answers, with names and units;
- the grouping (two at half speed lose a machine, so they come first);
- the newest snapshot by default;
- each error a model can act on: unknown snapshot, analysis still running, unknown factory, an item name that could be several;
- that every tool is read-only, and the full tool list.

1074 .NET tests green (1055 + 19), build clean. Not tried yet against your world with Claude Code.

## Choices made (overrule if you like)

- **Port 5280,** fixed in `launchSettings.json`, so `.mcp.json` can name it.
- **15 groups** at most in a summary, 20 findings and 25 machines per call unless asked for more: small enough for a context window.
- **Generators, drone ports and the arrangement runs** have no tools yet.
- **`AddLedgerMcp(configureLedger)`** has a parameter only tests use, to point the client at the fake: the same pattern as `AddAnalysisService`, kept for that reason.
- **`Microsoft.Bcl.Memory` 9.0.14 → 10.0.14:** the SDK needs it; the pin is still there for CUE4Parse's advisory.

## To try it

1. Start the AppHost. I stopped it for the build; it now starts `mcp` too.
2. Open Claude Code in this repository. It finds `.mcp.json` and asks once to allow the `satisfactory-ledger` server.
3. Ask, for example: *"Why are my encased uranium cell makers below 100%?"*

## .NET points

| Point | Java / Spring |
|---|---|
| **Tools from attributes:** `[McpServerTool]` on a method and `[Description]` on it and its parameters; the SDK makes the JSON schema from the parameter types and defaults (primer, *Backend*). | Spring AI's `@Tool` and `@ToolParam`. |
| **A typed `HttpClient`,** registered with `AddHttpClient<LedgerClient>`, gets service discovery and the standard resilience handler from the service defaults; the test swaps only the handler underneath (primer, *Backend*). | A `RestClient` bean with an interceptor chain; in a test, `MockRestServiceServer`. |
| **`JsonSerializerOptions` frozen without a resolver throw** in .NET 10 when a library asks for one; copying the SDK's `McpJsonUtilities.DefaultOptions` brings its resolver along (primer, *Backend*). | No equivalent: an `ObjectMapper` has no freeze step. |
| **`UseTestServer` + `GetTestClient`** host a whole app in memory, so a real MCP client talks to the real server without a port. | `@SpringBootTest(webEnvironment = MOCK)` with `MockMvc`, or `WebTestClient` bound to the context. |
| **Collection expressions with a spread,** `[.. items.Select(…)]`, build the answer lists in one expression. | `stream().map(…).toList()`. |
