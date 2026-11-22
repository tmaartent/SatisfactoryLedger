# Step 57 · Explain a finding

**Goal:** AI enhancement 3. A button on a held-back machine explains in words why it's held back and what to do, grounded in the trace of step 56 (asked 2026-11-21). Branch `feat/mcp-server`, on top of step 56 (committed), uncommitted.

## What you decided

- **The Assistant is its own service** in the AppHost, not a module in the API.
- **Ollama in a container,** started by the AppHost, on the GPU.
- **A local model, plus Claude** when an API key is configured.
- Step 56 committed first.

Written down as **ADR-0030**.

## How it works

```
Why? button ─▶ Assistant ─MCP─▶ trace_upstream / trace_downstream ─▶ API
                   │
                   ├─ prompt: rules + the tool's own description + its answer
                   ├─ IChatClient: qwen2.5:7b on Ollama, or Claude
                   └─ number check: which numbers aren't in the facts
                   ▼
           text, model, facts, unsupported numbers
```

1. The web app sends the finding: snapshot, building and recipe (class names), and starved or output blocked.
2. The Assistant calls the MCP server as an **MCP client**: `trace_upstream` for a starved machine, `trace_downstream` for an output-blocked one. Code picks the tool, not the model.
3. It reads the tool's **own description** from the server (every cause and what it means) and gives it to the model with the answer. One source for what `takenByOthers` means, for Claude Code and for the Assistant.
4. One model call, at temperature 0.2. The rules: only these facts, names as written, copy numbers and never work one out, say "in the model", the main cause in plain words and one next step, no list of the starts and no positions, two or three sentences.
5. **The number check:** every number in the answer must come from the facts, as written or rounded. The ones that don't come back as `unsupportedNumbers`, and the dialog flags them: *"Not in the facts: 45, 15. The model may have worked these out itself; check them."*
6. When none of the machines is held back, the trace's note is the answer, and no model is asked.

## Files

- **`src/SatisfactoryLedger.Assistant`** (new):
  - `LedgerAssistant.cs` (setup, as `LedgerMcp` does), `Program.cs`, `AssistantEndpoints.cs`;
  - `LedgerTools/`: `LedgerToolsClient` (the MCP client, a typed `HttpClient`), `ToolCall`, `LedgerToolException`;
  - `Explaining/`: `FindingExplainer`, `ExplanationPrompt`, `NumberCheck`, `HeldBack`, and the request and response records;
  - `Models/`: `ChatModelSetup` (Ollama always, Claude with a key), `ChatModels` (the keys, the first the default), `ChatModelResponse`.
- **AppHost:** `AddOllama("ollama").WithDataVolume().WithGPUSupport().AddModel("chat", "qwen2.5:7b")`, and the Assistant with references to `mcp` and `chat`, waiting for both. The web app references the Assistant but doesn't wait for it. The Claude key, when set, is a secret parameter.
- **ServiceDefaults:** traces `Experimental.Microsoft.Extensions.AI`, so the dashboard shows each model call (and, in development, its prompt and answer).
- **Web:**
  - `world/explain/assistant.ts` (the calls and the trace's shape, by hand), `trace-starts.ts` (the *where it starts* rows), and `explain-dialog.ts/html/scss`;
  - a lightbulb button in the machines table's *Why* column, for starved and output-blocked machines;
  - `proxy.conf.js` sends `/api/v1/assistant` to the Assistant.
- **Packages:**
  - `ModelContextProtocol.Core` 2.4.0, `Microsoft.Extensions.AI` 10.11.0;
  - `CommunityToolkit.Aspire.Hosting.Ollama` and `CommunityToolkit.Aspire.OllamaSharp` 13.6.1 (13.6 to go with Aspire 13.6);
  - `Anthropic` 13.12.0.

  Each API was checked against the installed version with a reflection probe.

## Tests

1113 .NET tests green (1099 + 14), 206 web tests green (195 + 11), build clean.
- **Number check (5):** copied, rounded, worked out (flagged), numbers in names, ids and positions, and full speed.
- **Explanations (9),** with a real MCP server in memory carrying fake trace tools, and a fake `IChatClient` that keeps its messages:
  - starved traces upstream;
  - output blocked traces downstream;
  - the prompt holds the tool's description and its answer;
  - the model doesn't get the machines' ids, the dialog does;
  - unsupported numbers are flagged;
  - the trace's note, without a model;
  - a tool's error as a 422;
  - an unknown model as a 400 naming the ones there are;
  - the model list.
- **Web (11):** the dialog (first model, where it starts with its map links, flagged numbers, the ledger's error, picking another model, no choice with one model), the button on held-back machines only, and `whereItStarts` (causes in words, starts merged with each machine once, a rate left out where they differ, who else takes it).
- The tests all passed on their first run, so I broke four things on purpose (the number check, the description in the prompt, the dialog's warning, the button's condition). Each broke the tests it should, and only those.
- **Found on the first real run:** *"Endpoint must use HTTP or HTTPS scheme."* The MCP client got its endpoint from the base address `https+http://mcp`, a scheme only service discovery understands. The tests had replaced that address with `http://mcp.test`, so they never saw it. The tests now swap only the handler and keep the real address (they failed with the same error), and the address is `http://mcp`: the MCP server only listens on HTTP anyway.
- **Found on the second real run:** a long, rambling answer about "a game like Factorio", in lists, cut off mid-word. Ollama's log said `truncating input prompt limit=2050 prompt=4855 keep=4`: its context is 4k tokens below 24 GB of VRAM, and it cuts a longer prompt from the start, so the system message with the rules was gone. Two fixes:
  - the model no longer gets the machines' ids (a new test; the dialog's facts keep them);
  - the AppHost gives Ollama a 16k context (`OLLAMA_CONTEXT_LENGTH`).
- **Your review after that:** too long, no display names, coordinates that mean little without a link, and the raw JSON. You chose a readable list of *where it starts* instead of the JSON:
  - `whereItStarts` (`world/explain/trace-starts.ts`) turns the trace's starts into rows: the cause in words, the item, the rate, and the places (*Converter (Dark Matter Residue) ×2*), each a link to the map. Starts with the same cause and item share a row, and each machine counts once by its id, so four Freight Platforms on four paths become one row with ×4. For *taken by others* / *supplied by others*, the row also lists who else gets the item, with how much.
  - The model no longer gets positions either, and is asked for two or three sentences: the main cause in plain words ("too few producers", not `tooFewProducers`), and one next step, without listing the starts. Its output cap is 250 tokens.
- **A made-up cause (2026-11-22):** for the Blender (Alternate: Nitro Rocket Fuel) the model wrote *too few consumers … check the machines producing the inputs*: the wrong direction, and no item. The trace had `traced: 2` and **no starts**: the two Blenders are blocked on Rocket Fuel, and no line of theirs carries it, so the trace walked nowhere. The model, given no cause, took the first one in the tool's description. Two fixes:
  - **The Assistant asks no model when the trace has no start** (a new test), and says the ledger found none.
  - **The trace (BR-A20, BR-A21; you chose this):** a blocked machine with no line that carries the item has *nowhere to go* itself; and, in both traces, held-back machines that nothing explains get an *unclear* start, so a trace never comes back empty. Three new domain tests, each seen failing with an empty trace. The upstream net has no test of its own: no world I could build ends upstream without a start.

## Choices made (overrule if you like)

- **Facts through the MCP server,** not a shared library or the API directly (see ADR-0030's alternatives).
- **Qwen 2.5 7B** (`qwen2.5:7b`), which fits the 8 GB GPU. **Claude Sonnet 5.5** as the Claude model (`Anthropic:Model` changes it).
- **Temperature 0.2,** at most 400 output tokens.
- **The number check** counts a fact when it's written as is, rounded to the decimals written, or rounded up or down to a whole number. Numbers in names and ids count, and 100 is always allowed (full speed). A sign is ignored.
- **One explanation per building and recipe,** like the trace. The button sits on each machine, but explains its kind.
- **Nothing stored:** each click asks again. The evals (idea 2) will want to keep them.
- **No retries on the model's client** (see below).

## .NET points

| Point | Java / Spring |
|---|---|
| **`IChatClient`** is .NET's one interface for chat models. Ollama's and Anthropic's clients both adapt to it, so `FindingExplainer` doesn't know which it has. | Spring AI's `ChatClient`. |
| **Keyed services:** `AddKeyedSingleton<IChatClient>("local", …)`, then `GetRequiredKeyedService<IChatClient>(key)` with the key the request names. | `@Qualifier`, or `getBean(name, type)` for a name known at run time. |
| **The MCP client:** `McpClient.CreateAsync(new HttpClientTransport(…, http))` over a typed `HttpClient`, so service discovery finds `mcp`. `ListToolsAsync` gives each tool's description, `CallToolAsync` its answer. | Spring AI's MCP client starter. |
| **HttpClient defaults reach every client,** with one shared options name (`-standard`), so options per client name are never read. Found with a spy on the options; the retries were counted with a handler that answers 503: four attempts with the defaults, one without. `RemoveAllResilienceHandlers()` is experimental (EXTEXP0001). | A `WebClient.Builder` customizer applies to all clients too. |
| **A real MCP server in a test:** `WebApplication` + `UseTestServer()` + `WithTools<FakeLedgerTools>()`, and the Assistant's client gets `GetTestServer().CreateHandler()` as its handler. Nothing listens on a port. | `MockMvc` / `WebTestClient` bound to a context. |
| **Aspire hosting integrations** from the Community Toolkit: `AddOllama(…).AddModel(…)` pulls the model and gives its connection string (`Endpoint=…;Model=qwen2.5:7b`) to whoever references it. | Testcontainers' Ollama module, but wired into the app's startup. |
