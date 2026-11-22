# Step 65 · Every start explained

**Goal:** asked why the Constructors (Alternate: Iron Wire) were starved, the explanation named only Iron Ingot taken by others. It left out that nothing brings Iron Ore to two Smelters, the other start the trace found. It also listed every machine that takes the ingots, which the panel already lists. You chose that every start is explained and that the model isn't told who else takes an item (asked 2026-11-22). Branch `feat/no-numbers-in-explanations`, on top of step 64, uncommitted.

## What you decided

- **Every start is explained.**
- **Who else takes an item stays out of what the model gets.** The panel names them.

## How it's built

First attempt: one prompt with all starts and the rule "explain every start". Three runs gave the same wrong sentence: *"…unable to provide due to Iron Ore being taken by others"*. The model merged the two starts into a cause neither gives.

So the model gets **one start per call**:
- **`FindingExplainer`** asks once per start, in the trace's order, and joins the answers as paragraphs. The number check runs on the joined text.
- **`ExplanationPrompt.For(limit, facts, start)`** keeps only that start in the facts. With more than one, the question adds that the ledger finds more than one place where it starts and the others are explained beside it. The rules speak of one start again.
- **`Simplify`** drops `others`.
- **The panel** keeps the paragraphs (`white-space: pre-line`).

Written into ADR-0030 (one call per start, and why).

### Technical choices to overrule

- **One call per start, not one call in all:** a trace with three starts takes three model calls, one after another. Each takes a couple of seconds on your GPU.
- **Paragraphs in the trace's order,** as the panel lists the starts.

## Tests

| File | What |
|---|---|
| `ExplanationTests.cs` | Two starts are asked about in two calls, each with only its own start, and the answers joined; the second call says there are other places; who else takes it isn't in any question, but stays in the facts for the panel |
| `FakeChatClient.cs` | Keeps the question of every call |

25 Assistant tests pass.

**Tried on your snapshot** (Learning), the same question:

> The Constructor (Alternate: Iron Wire) machines are starved because there is no Iron Ore being brought to the Smelter (Iron Ingot) machines, which are in turn unable to produce Iron Ingot. The next step is to ensure Iron Ore is supplied to the Smelter.
>
> The Constructor (Alternate: Iron Wire) machines are short of Iron Ingot because the Iron Ingot is taken by others at the Smelter. […] consider increasing the output or efficiency of the Smelter or redirecting the allocation […]

In another run the first paragraph said the Smelters run *"at full speed but cannot obtain enough Iron Ore"*, which contradicts itself. That's wording for the evals to count.

## .NET notes

- **`JsonNode.DeepClone()`**: a node belongs to one parent, so moving the chosen start into a new `JsonArray` needs a copy. Adding it as it is throws *"The node already has a parent"*. Jackson's `ObjectNode` has no such rule: a node may sit in two trees.
