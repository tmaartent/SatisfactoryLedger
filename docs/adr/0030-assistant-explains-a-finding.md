# ADR-0030: The Assistant explains a finding, from the MCP server's tools, with a local model or Claude

- **Status:** Accepted
- **Date:** 2026-11-21

## Context
AI enhancement 3 ([ai-enhancements.md](../domain/ai-enhancements.md)): the findings are correct but terse. A button on a finding should explain it in a few sentences, with a next step, grounded in the finding's trace (BR-A20, BR-A21).

The enhancements doc planned an *Assistant module* in the API, using the other modules' `*.Contracts`. Since then, the MCP server (ADR-0029) already turns the API's answers into the player's language, and has the traces as tools. Four questions: where the Assistant runs, where its facts come from, which models it uses, and how a model stays with the facts.

## Decision
- **Its own service in the AppHost:** `SatisfactoryLedger.Assistant`, an ASP.NET Core app with `POST /api/v1/assistant/explanations` and `GET /api/v1/assistant/models`. The model and its latency stay out of the API's process. *(Decided 2026-11-21.)*
- **Its facts come from the MCP server, as an MCP client.** It calls `trace_upstream` for a starved finding and `trace_downstream` for an output-blocked one, and gives the model:
  - the tool's answer, without the machines' ids and positions, each cause by the ledger's term alone (the MCP server's `causeTerm`, from `TraceCauses`, the one place the terms are written; decided 2026-11-22);
  - the tool's own description, which says what every cause means.

  The names, positions and causes are then the same ones Claude Code sees. The Assistant references neither the API's records nor the MCP server's code.
- **Code chooses the tool; the model makes one call.** No agent loop: the finding says which trace fits. Tool-calling agents are for idea 6.
- **Models behind `IChatClient`** (`Microsoft.Extensions.AI`), each registered under a key:
  - `local`: **Qwen 2.5 7B** (`qwen2.5:7b`, 4-bit, 4.7 GB) on **Ollama in a container** with the GPU, started by the AppHost (CommunityToolkit.Aspire). Always there. *(Container and local model decided 2026-11-21; the model is a default, to be overruled.)*
  - `claude`: Claude (`claude-sonnet-5-5`) through Anthropic's SDK, only when the AppHost has the secret `Parameters:anthropic-api-key`. *(Decided 2026-11-21.)*

  A request names the model; the first is the default. Step 2's evals can compare the two on the same facts.
- **Grounding rules in the prompt:**
  - use only the facts;
  - write names as the facts give them;
  - copy numbers and never compute one;
  - say "in the model";
  - the main cause in plain words, and one next step;
  - no list of the starts and no positions: the dialog lists those;
  - two or three sentences.

  Temperature 0.2.
- **Code checks the numbers.** Every number in the answer must come from the facts: as written, rounded to the decimals written, or rounded up or down to a whole number. Numbers in names and ids count, and 100 (full speed) is always allowed. The ones that don't come from the facts are returned as `unsupportedNumbers`, and the dialog flags them. This is "the model explains, the solver computes", checked rather than hoped for.
- **When nothing is held back,** the trace's own note is the answer, without a model.
- **The answer shows its sources:** the model and the tool. Beside the text, the dialog lists *where it starts*: built by code from the tool's answer, not by the model, with one row per cause and item and each place linked to the map (`?focus=` its first id). The model says why; the list says where. *(Decided 2026-11-21, instead of the raw JSON.)*

## Alternatives considered
- **An Assistant module in the API** (the enhancements doc's plan): no extra process, but the chat clients, their timeouts and a model's latency would live in the API. The name translation would also need writing a second time.
- **A shared library with the MCP server's API client and Catalog names:** the Assistant would call the API directly. That's one hop fewer, but it couples two apps through code instead of through a protocol. It would also skip what this step shows, the MCP server used by a client other than Claude Code.
- **Letting the model choose the tools** (`FunctionInvokingChatClient` with the MCP tools as `AIFunction`s): needed when the question is open, not when the finding already says which trace fits. Small local models are also least reliable at tool calling.
- **A native Ollama install:** a simpler GPU setup, but outside the AppHost and its dashboard.

## Consequences
- **The first run downloads the model** (about 4.7 GB) into a data volume; the Assistant waits for it, while the web app doesn't. The model then stays loaded in the GPU's 8 GB.
- **Ollama's context is set to 16k tokens** (`OLLAMA_CONTEXT_LENGTH`). Its default below 24 GB of VRAM is 4k, and Ollama cuts a longer prompt from the start, so the model loses the rules and the cause definitions without an error. A trace of a large factory was 4855 tokens; its ids took most of them, hence leaving them out.
- **The Assistant needs the MCP server, which needs the API.** One more hop per explanation, all local.
- **No retries for the model's HTTP client:** ServiceDefaults gives every client retries and a 10-second attempt timeout. A model's first answer takes longer while the model loads, and a retry would ask it all again. Removing them for one client needs `RemoveAllResilienceHandlers`, experimental in 10.10 (EXTEXP0001).
- **The dashboard shows the prompts** in development: `Experimental.Microsoft.Extensions.AI` is traced, with sensitive data on.
- **The web app calls the Assistant by hand,** not through the generated client: `api/openapi.json` is the API's contract. The dev server's proxy sends `/api/v1/assistant` to the Assistant.
- **Explanations aren't stored or cached:** each click asks the model again. Storing them is for the evals.
- **An explanation covers one building and recipe at a time,** like the trace it stands on.
