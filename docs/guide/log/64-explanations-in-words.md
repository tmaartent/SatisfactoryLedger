# Step 64 · Explanations in words

**Goal:** an explanation said *"21 machines needing it and only 9 units of Wire available per minute"*. In fact 9 Assemblers (Alternate: Steel Rotor) were short, and the belt was full at 1,200/min. The model copied the right numbers but pinned them on the wrong things, and the number check let them through: 9 and 21 were both in the facts. You chose that the text gets no numbers (asked 2026-11-22). Branch `feat/no-numbers-in-explanations`, uncommitted.

## What you decided

- **The model explains in words; the list beside it gives the numbers,** each with its label. Names keep theirs (Conveyor Belt Mk.6).

Written into ADR-0030 (grounding rules, and *the model gets no numbers and writes none*), with the glossary's *unsupported numbers* and the AI enhancements doc in line.

## How it's built

- **`ExplanationPrompt`:**
  - the rule "copy every number" became "write no numbers: no counts, rates or percentages";
  - `Simplify` removes every property with a number value before the model sees the facts (`JsonNode.GetValueKind() == JsonValueKind.Number`). The response still carries the full facts for the panel.
  - `NextStep` no longer counts lines: *Lay another Conveyor Belt Mk.6 beside it.* (two lines), *Lay more lines of … beside it.* (more), *Rebuild it as lines of … side by side.* The panel shows how many.
- **`NumberCheck`** changed meaning: it no longer looks for numbers the facts hold, but flags every number outside a name the facts give. It removes those names from the text first (longest first), so the 6 of *Mk.6* passes and *6 machines* doesn't.
- **The panel's warning** says *Numbers in the text: …* and points at the list below.

### Technical choices to overrule

- **The field keeps its name, `unsupportedNumbers`:** no number is supported in the text now.
- **The model doesn't get the counts of machines either** (`machines`, `traced`, `count`). It needn't say how many; the list does.

## Tests

| File | What |
|---|---|
| `NumberCheckTests.cs` (rewritten, 4) | Words alone pass; numbers copied from the facts are flagged; numbers in names are part of the name; a number outside a name is flagged even when a name holds it |
| `ExplanationTests.cs` | The model is given no number but those in names; *Lay more lines …* for three lines; the next step's wording without counts; a model's 45, 30 and 15 all flagged |
| `explain-panel.spec.ts` | The warning names the numbers and points at the list |

1139 .NET and 245 web tests pass; the build is clean (464 kB).

**Tried on your snapshot** (Learning), the same question: *"The Assembler (Alternate: Steel Rotor) machines are short of Wire, as the path shows a bottleneck at the Conveyor Belt Mk.6. The next step is to Lay another Conveyor Belt Mk.6 beside it to increase capacity."* No numbers flagged. It still mentions "the path" and copies the capital L, which is wording the evals can measure.

## .NET notes

- **`JsonNode.GetValueKind()`** tells a mutable JSON node's kind (number, string, …) without knowing its CLR type. Like Jackson's `JsonNode.isNumber()`. The properties are collected with `ToList()` before removing them, since a `JsonObject` can't change while it's enumerated, like Java's `ConcurrentModificationException`.
