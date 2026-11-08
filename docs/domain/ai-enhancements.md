# AI enhancements

Ideas for practising AI engineering on this project, kept like the [possible enhancements](possible-enhancements.md): what each is, why it's worth doing here, and what it would take. None is planned yet. When one is picked up, it gets an ADR where needed and a step log.

The project suits this well. It has real domain data (the game's Docs file, the owner's saves), a deterministic solver that gives ground truth, an API with a contract (`api/openapi.json`), and investigations whose right answers are known.

**One rule runs through all of it:** the model explains, the solver computes. A model never does the rate arithmetic (ADR-0004: rates are exact rationals).

## Suggested order

1. MCP server, then 2. evals, then 3. explain a finding. Everything after builds on the evals: they're what tells you whether RAG, a local model or fine-tuning actually helped. MLflow (8) can start as soon as the evals do.

## 1. An MCP server over the ledger
*Raised 2026-11-08.*

**What:** expose the API as Model Context Protocol tools, such as `list_snapshots`, `get_findings`, `find_machine` and `trace_upstream`, so Claude or any MCP client can investigate a world in a conversation.

**Why:** the investigations of step 25 were done by hand with throwaway scripts: following "starved of X" back through the chain, solving with and without the uploader. A good tool set makes that one question: "why are my cell makers at 76%?"

**Skills:** tool design (names, parameters and errors a model can act on), agent loops, keeping answers small enough for a context window.

**What it would take:**
- `trace_upstream` and `trace_downstream` endpoints: the walks the scripts did, as API calls.
- The C# MCP SDK (`ModelContextProtocol` on NuGet), hosted as its own project in the AppHost. Verify the version installed before use.

## 2. An eval set from real cases
*Raised 2026-11-08.*

**What:** a graded set of questions with known answers, run against every assistant feature:
- Why are the encased uranium cell makers below 100%? *Output blocked: the fuel rod makers lack electromagnetic control rods.*
- Why are the Ficsonium encoders blocked? *A balanced loop with nothing outside to push it to the top.*
- Why did the plutonium plants show as starved? *Hand-fed rods the save reader didn't read.*

**Why:** evals are the core AI-engineering skill. Here the ground truth is cheap: the solver produces more of it whenever needed.

**Skills:** rubrics, checks in code first and an LLM judge only where code can't decide, regression tracking, cost and latency per run.

**What it would take:** the questions with expected facts as a file in the repo, a runner, and snapshots of the owner's saves at known points (or small fixture worlds built in code, as `StockFindingsTests` does).

## 3. Explain a finding
*Raised 2026-11-08.*

**What:** a button on a finding that writes a short explanation and next step, grounded in the finding and its trace, for example "the rod makers get 76% of the control rods they need; look at the control rod line".

**Why:** the findings are correct but terse; a player wants to know what to do.

**Skills:** grounding, structured context, prompt design, showing where each claim comes from.

**What it would take:** an *Assistant* module that only uses the other modules' `*.Contracts` (ADR-0003), and `Microsoft.Extensions.AI`'s `IChatClient`: .NET's common interface for chat models, roughly Spring AI's `ChatClient`. The same code then runs against Claude or a local model.

## 4. RAG over the docs and the game data
*Raised 2026-11-08.*

**What:** questions over the ADRs, primer, step logs and the Catalog: "why are pipes one hub?", "which recipes use nitric acid?".

**Why:** two kinds of data side by side. Prose suits vector search; recipes are structured and often better served by a SQL tool than by retrieval. Learning when to use which is the point.

**Skills:** chunking (an ADR, a recipe and a code file each split differently), embeddings, hybrid search (keywords and vectors), reranking, citations, retrieval evals (was the right chunk found?).

**What it would take:** **pgvector** in the PostgreSQL the app already runs, an embedding model, and retrieval questions added to the eval set.

## 5. Plain language to a plan
*Raised 2026-11-08.*

**What:** "I want 10 uranium fuel rods a minute." The model turns it into a request matching the OpenAPI schema; the linear programme computes the factory; the model explains the result.

**Why:** pairs with the LP solver, the next planned step. A clean example of a model at both ends and a deterministic core in the middle.

**Skills:** structured output, schema validation, recovering from output that doesn't validate.

**What it would take:** the LP solver, and a planning endpoint for it.

## 6. A local model, then a fine-tuned one, with Hugging Face Transformers and PEFT
*Raised 2026-11-08.*

**What:** run a small open model for "explain a finding", compare it with Claude on the eval set (quality, speed, cost), then fine-tune it with **PEFT** (LoRA, or QLoRA on a 4-bit model to fit a home GPU) on pairs of finding and explanation.

**Why:** the full loop of an AI engineer's work on models: choose a model, make training data, train, evaluate, decide. The honest outcome may be that a good prompt beats the fine-tune; the evals will say.

**Skills:**
- **Transformers:** loading models and tokenizers, chat templates, generation settings, batching.
- **PEFT:** LoRA ranks and target modules, adapters you can swap without copying the base model, QLoRA's memory savings.
- **Training data:** synthetic pairs from the solver's findings, written by a strong model and checked by code against the solver's numbers. A model trained on wrong facts learns them.
- **Training:** TRL's `SFTTrainer` on top of Transformers; overfitting on a small set; held-out evaluation.

**What it would take:**
- A Python project in the repo, for example `ml/`, outside the .NET solution. Aspire can run Python apps, so it can still start with the AppHost.
- Serving the adapter so `IChatClient` can call it: Ollama after converting to GGUF, or a small OpenAI-compatible server.
- A GPU, or a rented one for the training runs.
- Pinned versions of `transformers`, `peft`, `trl` and `bitsandbytes`, checked against their installed versions before use (as for every library here).

## 7. Observability and safety, alongside everything
*Raised 2026-11-08.*

**What:** trace every model call, and treat text from saves as untrusted.

**Why:**
- Aspire already gives the app OpenTelemetry. Model calls belong in the same dashboard: tokens, latency, cost, cache hits.
- Saves hold text the player typed: session names, drone port tags. Once that text reaches a prompt, it's a real prompt-injection surface.

**Skills:** LLM observability (OpenTelemetry's GenAI conventions), cost control, prompt caching, handling untrusted input.

## 8. MLflow for experiments, evals and models
*Raised 2026-11-08.*

**What:** one place where every experiment leaves a record:
- **Tracking:** each eval run and each fine-tuning run logs its parameters (model, prompt version, LoRA rank, learning rate), metrics (eval score, cost, latency, loss) and artifacts (the eval answers, the adapter).
- **Evaluation:** MLflow's GenAI evaluation runs the eval set of idea 2 with scorers in code and LLM judges, and compares runs side by side.
- **Tracing:** MLflow Tracing records each model call and tool call of an agent; it speaks OpenTelemetry, so it can sit next to the Aspire dashboard.
- **Model registry:** the LoRA adapters of idea 6 as versioned models, with the eval run that justified each.

**Why:** without it, "the fine-tune was better" is a memory. With it, it's a run you can open, compare and reproduce. This is how teams that ship models work.

**Skills:** experiment design, reproducibility, comparing runs fairly (same eval set, same judge), model versioning.

**What it would take:** an MLflow tracking server (a container in the AppHost, with PostgreSQL as its store), the Python project of idea 6 logging to it, and the .NET eval runner logging through MLflow's REST API or OpenTelemetry. Check the installed MLflow version's API before use; its GenAI features change quickly.
