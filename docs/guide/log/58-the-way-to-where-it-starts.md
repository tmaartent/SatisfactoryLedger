# Step 58 · The way to where it starts

**Goal:** make the explain dialog's *where it starts* rows readable on their own (asked 2026-11-21). On your world, the Singularity Cell explanation listed "Other machines get it first · Computer · 50/min" with nothing tying computers to the Nuclear Pasta shortage, and the model's text said "a takenByOthers issue". Branch `feat/mcp-server`, on top of step 57 (uncommitted), uncommitted.

## What you decided

All four of the proposed changes:
1. Each row shows the **way there**: the items between the machines asked about and the start.
2. Each row says **who is held back first**: the machines short of the item (or unable to get it away), linked to the map, and what the buildings it starts at do with the item next to the rate ("Sent by · 50/min").
3. Starts are grouped by **cause, item and way**, not only cause and item.
4. The model gets each **cause in words**, so it stops writing the tool's names for them.

## How it reads now

```
Other machines get it first · Computer
  Short of it        Manufacturer (Radio Control Unit) ×1          ← map link
  Via                Nuclear Pasta ← Pressure Conversion Cube ← Radio Control Unit
  Sent by · 50/min   Assembler (Alternate: Crystal Computer) ×10
  Also taken by      Manufacturer (Adaptive Control Unit) ×4 · 8/min, AWESOME Sink ×1 · 25/min, …
```

"←" reads "made from" (upstream); a blockage's way uses "→" ("goes on to") and says *Can't get it away*.

## How it's built

- **MCP** (`Answers/TraceStepAnswer.cs`, `Tools/TraceTools.cs`): each step of a path now carries its machines' `ids` (the first ten, as the API gives them). The dialog needs them to link the held-back machines to the map, and to count a machine once when several starts name it. The Assistant already strips every `ids` before the model reads the facts, so the prompt doesn't grow.
- **Assistant** (`Explaining/CauseWords.cs`, new; `Explaining/ExplanationPrompt.cs`): while it strips ids and positions, `Simplify` (was `RemoveWhereabouts`) replaces each `cause` with its words. The rule now says to use the words the facts give and never the tool's names. The facts the dialog gets keep the cause as the tool gave it.
- **Web:**
  - `assistant.ts`: `TraceStart.path` and the `TraceStep` type.
  - `trace-starts.ts`: the causes `Record` now holds the words, the label for the buildings it starts at, and whether the rate means anything. `wayTo` works out the held-back machines (the path's last step) and the items before the start's own. The row key is cause, item and way.
  - `explain-dialog.html/.ts/.scss`: the lines per row, and one `<ng-template>` for the three lists of places instead of the same markup twice.

## Tests

1115 .NET tests green (1113 + 2), 215 web tests green (206 + 9), build clean. Each new test was seen failing first (compile errors for the web ones, then wrong output for the dialog).
- **MCP (1):** a path step names its machines' ids.
- **Assistant (1):** the model reads `"cause":"too few producers"` and never `tooFewProducers`, while the facts shown keep `tooFewProducers`. The old assert that the model saw `"tooFewProducers"` is gone.
- **Web, `whereItStarts` (7):** your Singularity Cell case as a fixture: who's short, linked; the way; *Sent by* with the rate; two ways stay two rows; a machine counted once when two starts on one way name it; a blockage (*Can't get it away*, "→"); no rate where the cause's rate is always nothing.
- **Web, dialog (2):** the held-back machines with their map link and the *Made at full speed by · 30/min* line; the *Via* line for a start further up. The test that took the first link on the page now looks for the smelter's link, since the held-back machine comes first.
- Not tried on your world yet: the app was running, so I built into a separate folder (see the primer) and didn't restart it.
- **Found in review (2026-11-22):** `add` counted a group by its new ids, but a group lists only its first ten, so 20 Assemblers showed as ×10. It now counts by the group's `count`, less the listed ids the row has already; a group whose listed ids are all there is skipped as one seen again. A new `whereItStarts` test (seen failing with ×10).
- **Totals after that review and the trace fix of step 57's log** (*A made-up cause*): 1119 .NET tests green, run in place with the AppHost stopped; 216 web tests green; build clean.

## Choices made (overrule if you like)

- **The way leaves out the start's own item and the machines asked about.** The item is in the row's heading, and the machines are in the dialog's title. A start right at the machines asked about has no *Via* line.
- **Labels per cause:** *Made at full speed by* (too few producers), *At capacity* (belt full), *Given by* (source at limit), *Sent by* (taken by others, unknown supply, producer output blocked), *Taken at full speed by* (too few consumers), *Taken by* (supplied by others, consumer starved), *Full* (storage full), *Without a recipe*, *At* (the rest).
- **No rate** for *nothing brings it*, *no recipe*, *storage full*, *nowhere to go* and *unclear*: it's always 0 there, or (for unclear) spare supply that says little. *Comes from a station or container · 0/min* keeps its 0: the station really sends nothing.
- **No rate on the held-back machines:** the path gives one rate for a whole step, repeated on each kind of machine in it, so per kind it would be wrong.
- ~~**The words live twice**, in `trace-starts.ts` and in `CauseWords.cs`.~~ Superseded below: *One language for the causes*.
- **Found, not changed:** *Its producer has no recipe* is also what a blockage's `noRecipe` says, where it's a consumer without a recipe. One word to settle if you like.

## .NET / Angular points

| Point | Java / Spring, TypeScript |
|---|---|
| **`JsonValue.TryGetValue<string>(out …)`** reads a `JsonNode`'s value only when it is that type; `properties["cause"] = "…"` replaces it, with an implicit conversion from `string` to `JsonNode`. Changed before the `foreach` over the same object, not inside it. | Jackson's `ObjectNode.put("cause", …)` on a tree from `readTree`. |
| **Building while the app runs:** MSB3027, *the file is locked by SatisfactoryLedger.Mcp*. `--artifacts-path` builds into another folder (primer, *Testing*). | A running JVM doesn't lock `target/`. |
| **`<ng-template>` + `NgTemplateOutlet`** for a list used three times in one template; `$implicit` is what `let-places` receives (primer, *Frontend*). | A Thymeleaf fragment with parameters; a small function component in React. |

## One language for the causes (2026-11-22)

You asked for unity: the business rules said *taken by others*, the tool's description `takenByOthers`, and the model and dialog *other machines get it first*. You chose **the business rules' terms** everywhere.

- **One place:** `Mcp/Answers/TraceCauses.cs`, a `const string` per term and the API code → term table. The MCP server is where the API's answers become the player's language (ADR-0030), so the terms live there, for Claude Code too.
- **The trace answer** gives each start its `causeTerm` next to the `cause` code. **The tool descriptions** are built from the same constants (`$"{TraceCauses.TakenByOthers} (enough is made …)"`) and no longer name the codes: the model reads one word for a cause in the description and in the facts.
- **The Assistant** lost `CauseWords.cs`: `Simplify` puts the `causeTerm` in place of the code and drops the field, so the model sees `"cause":"too few producers"` alone.
- **The web** reads `causeTerm` and capitalises it; the `Record<TraceCause, …>` keeps only what's the dialog's own (the *Sent by* labels and whether a rate means anything).
- **The terms:** too few producers, bottleneck (the glossary's word for `beltFull`), source at its limit, taken by others, producer output blocked, unknown supply, nothing brings it, without a recipe, too few consumers, supplied by others, consumer starved, storage full, nowhere to go, unclear. BR-A20/21 bold them, and the glossary has a **Cause** row listing them.
- **Tests:** MCP 2 (the answer's `causeTerm`; each description names its causes by term and not by code, for both traces), Assistant 1 changed (no `causeTerm` left in the prompt), web 1 (the row's cause is the trace's term). The MCP ones were seen failing by breaking the code on purpose; the others failed first.
- **Totals:** 1121 .NET tests green, in place, all 13 test projects; 217 web tests green; build clean.
- **Choice made:** the MCP answer keeps the code too, for programs (the web keys its labels on it); a code the MCP server doesn't know yet keeps the code as its term.

