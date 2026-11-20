# ADR-0029: An MCP server over the public API, read-only

- **Status:** Accepted
- **Date:** 2026-11-20

## Context
AI enhancement 1 ([ai-enhancements.md](../domain/ai-enhancements.md)): let a model investigate a world in a conversation. *"Why are my cell makers at 76%?"* The investigations of step 25 were done by hand with throwaway scripts. The Model Context Protocol (MCP) is the standard way to give a model tools; Claude Code and other clients speak it.

Three questions to settle: where the server runs, how it reaches the data, and what it may change.

## Decision
- **Its own project in the AppHost:** `SatisfactoryLedger.Mcp`, an ASP.NET Core app with the official C# SDK (`ModelContextProtocol.AspNetCore` 2.4.0). It speaks MCP over HTTP (*Streamable HTTP*, stateless) at `/mcp`, on port 5280. The AppHost starts it after the API. *(Decided 2026-11-20.)*
- **It reads the public API,** `/api/v1/…`, through service discovery, as the web app and any other client would. It references no module and no `*.Contracts`. The API is the contract (ADR-0006); the server's own records hold only the fields its tools use.
- **Read-only.** Every tool is marked `ReadOnly`, and none calls a POST, PUT or DELETE. Importing a save or changing a factory stays with the player. *(Decided 2026-11-20.)*
- **The tools speak the player's language:**
  - in-game names (`Iron Plate`), not class names; a name that isn't clear gets an error listing the likely ones;
  - rates per minute, shares and utilisation in percent, positions in metres;
  - findings grouped and summed up first (`get_findings_summary`), details on request, so an answer fits a context window.
- **The model explains, the solver computes.** The tools give the solver's numbers and the Catalog's recipes, so the model can check against them rather than recall them.
- **Errors a model can act on.** An unknown snapshot names `list_snapshots`; an analysis still running says to ask again; an unknown factory lists the factories there are.

The first tools (step 55): `list_snapshots`, `get_findings_summary`, `list_findings`, `list_factories`, `get_factory_findings`, `find_machines`, `find_items`, `get_recipes`, `get_transport_tiers`. Tracing up and downstream (step 56) needs new World endpoints first.

## Alternatives considered
- **A stdio console tool,** started by the MCP client: no AppHost change, but it needs the API's address passed in, and it lives outside the dashboard and its traces.
- **Inside the API,** calling the modules' Contracts in-process: the fewest moving parts, but it puts the MCP SDK into the API and couples the tools to the modules' internals instead of the published contract.
- **Write tools** (define a factory, delete a snapshot): useful, but a model acting on the player's data is a decision of its own, with confirmations to design. Left for later.

## Consequences
- **The server needs the AppHost running.** Claude Code connects to `http://localhost:5280/mcp` (the repository's `.mcp.json`).
- **A second client of the API.** A change to the API's responses can break the server's records. The tests answer with the API's response shapes, so a renamed field shows up there, not in a conversation.
- **The Catalog's names are cached for five minutes,** since every tool call needs them and they change only with an import.
- **No authentication,** like the internal endpoints (ADR-0018): it runs locally under Aspire. A server reachable by others would need it.
- **`Microsoft.Bcl.Memory` is pinned at 10.0.14,** the version the SDK needs; it was 9.0.14 for CUE4Parse.
