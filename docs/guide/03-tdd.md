# 03 · Test-driven development

## The cycle

1. **Red:** write *one* small test for the next bit of behaviour. Run it and watch it fail **for the right reason**. A compile error counts as red.
2. **Green:** write the *simplest* code that passes. Hard-coding is allowed. The next test will force generalisation ("triangulation").
3. **Refactor:** with all tests green, improve names, remove duplication, extract concepts. Tests and production code both get refactored.

Commit at green, and again after the refactor. Stay in the cycle for minutes, not hours.

## Where to start each feature

Start from the **business rule list**, not the class diagram. For BR-S4 (priority merger), write a short list of examples first. That's your test list:

| Remaining (primary) | Secondary | Capacity | Expected flow after | Expected overflow |
|---|---|---|---|---|
| 500 | 1200 | 1200 | 1200 | 500 |
| 0 | 1200 | 1200 | 1200 | 0 |
| 500 | 300 | 1200 | 800 | 0 |
| 1200 | 600 | 1200 | 1200 | 600 |

Pick the simplest one, make it pass, then the next. The design of `SupplyLineAnalyzer` *emerges* from this. You don't draw it up front.

## Naming and structure

- One test class per behaviour or concept (`PriorityMergerTests`, `StackHeightTests`), not one per production class.
- Name tests after behaviour in domain language: `Injection_draws_secondary_only_up_to_free_capacity` or `BR_S4_…`. A failing test name should tell the story without opening the file.
- **Arrange / Act / Assert**, with a single act. Several asserts are fine when they describe one outcome.
- Use `[Theory]` with data rows for example tables like the one above.

## Test data builders

Building a `Blueprint` with five steps in every test drowns the intent. Create **builders** in the test project: `ABlueprint.ForReinforcedIronPlates().WithInput(IronOre, 600).Build()`. Tests then only mention what matters for *that* test. Named game data (`IronOre`, `IronPlateRecipe`) lives in a small test catalog with realistic values from the wiki or `Docs.json`.

## Property-based testing (M1, M5, M7)

Examples test the cases you thought of. Properties test rules that must hold for **all** inputs. Use **CsCheck** to generate hundreds of random lines and check:

- `Rate`: a + b − b == a. Normalisation is idempotent. Equality is consistent with the hash code.
- `SupplyLineAnalyzer`:
  - No segment's flow ever exceeds capacity.
  - Total delivered to taps ≤ total supplied.
  - Delivered + remaining + overflow + stranded = supplied (conservation of items).
  - A primary input is always fully consumed before the secondary is drawn.
  - Within a section, every tap gets the same fraction of its demand (BR-S3), so reordering taps inside a section never changes the result.
- `LayoutSuggester`: every demand is met. The greedy strategy never uses more sources than "one per demand".

When a property fails, CsCheck shrinks the input to the smallest failing example. Copy that into a regular example test so the bug stays fixed.

## What not to do

- **Don't mock the domain.** Domain objects are fast and deterministic, so use the real ones. Mock (or better, use a real implementation of) only what crosses a process boundary.
- **Don't test private methods.** Test through the public behaviour. If something private feels like it needs its own tests, it probably wants to be its own concept, e.g. a value object.
- **Don't assert on implementation details** (that a list has a certain internal order, how many times a method was called). Assert on outcomes.
- **Don't chase 100% coverage.** Aim for every business rule covered. Coverage is a hint, not a goal.
- **Don't write all tests first.** That's not TDD. One test at a time.

## Speed

Domain tests must run in well under a few seconds in total. Use `dotnet watch test --project tests/SatisfactoryLedger.FactoryNetwork.Domain.Tests` (or your IDE's continuous testing) so feedback is immediate.

## Done when (per feature)
- [ ] Every business rule ID touched has at least one test named after it
- [ ] Example tables from the rules exist as `[Theory]` data
- [ ] Core calculations have property-based tests
- [ ] The git history shows the test → implementation → refactor rhythm
