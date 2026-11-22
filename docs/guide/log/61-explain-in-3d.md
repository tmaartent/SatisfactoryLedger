# Step 61 · Explain from the 3D view

**Goal:** the explain button in the 3D view too, when a machine is clicked: in an area's 3D view, and in a factory's (asked 2026-11-22). Branch `feat/mcp-server`, uncommitted.

## What was built

- **Explain why** in the 3D panel, under why a starved or output-blocked machine runs as it does. It opens the same dialog as the findings table, for all the machines of that building and recipe. Generators get no button: the Assistant traces machines.
- **One component, both views:** the factory page embeds the area page (`app-area-page [embedded]`), so the button is in both.
- **`Explainer`** (`world/explain/explainer.ts`, new): an injectable service that decides whether a machine can be explained and opens the dialog. The findings table's `explain` and `explainable` now call it, so the two views can't drift apart.

## Files

| File | Change |
|---|---|
| `world/explain/explainer.ts`, `.spec.ts` | New: `explainable`, `explain` |
| `world/findings.ts` | Uses `Explainer` instead of opening the dialog itself |
| `world/area/area-page.ts`, `.html`, `.scss` | The button in the panel, while not defining a factory |

225 web tests pass, and the build is clean.

## Angular / TypeScript notes

- **`@Injectable({ providedIn: 'root' })`** makes one instance for the whole app, created the first time something injects it. Spring's singleton `@Service` is the nearest match, but Angular creates it lazily and drops it from the bundle when nothing injects it.
- **`recipe?: string | null` vs `string | null`:** the 3D view's buildings have an optional recipe (`undefined` when it isn't there), while the API's request type allows only `null`. `machine.recipe ?? null` turns one into the other. TypeScript keeps `undefined` and `null` apart; Java has only `null`.

## Also: the way to a start, in words

On your world, the Nobelisk assemblers were explained as "starved for Sulfur". The trace's path did say how: they lack Black Powder, and the Black Powder assemblers lack Sulfur. qwen2.5:7b skipped from the first step to the start's item.

`ExplanationPrompt` now gives the model each start's path as a sentence, its `way`, and not the steps. A new rule says to name what the machines asked about lack first, as the way does.
- **Upstream:** *"The Assembler (Nobelisk) machines are short of Black Powder, made by the Assembler (Black Powder) machines, which are short of Sulfur."*
- **Downstream:** *"The Smelter (Iron Ingot) machines can't get Iron Ingot away, to the Constructor (Iron Plate) and Constructor (Alternate: Coated Iron Plate) machines, which can't get Iron Plate away."*

The model's JSON is written with `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, so *can't* stays as written for the model to copy. The default encoder escapes `'` as `\u0027`, for JSON that may end up inside HTML. "Unsafe" is about HTML; a prompt is never HTML. Two new tests, in `Assistant.Tests/ExplanationTests.cs`.