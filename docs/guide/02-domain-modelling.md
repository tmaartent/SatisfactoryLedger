# 02 · Domain modelling

The domain projects (`*.Domain`, `SharedKernel`) are the heart of the showcase. They contain **no** framework code: no EF attributes, no ASP.NET, no JSON attributes, no logging. Only C# and your business rules.

## 1. Language first

- Every class, method and property name comes from the [glossary](../domain/glossary.md). If you're tempted to write `CalculateRatio` or `FactoryManager`, stop and find the domain word.
- Methods express **intent**, not data changes: `blueprint.AddStep(recipe, machines)` rather than `blueprint.Steps.Add(...)`, and `factory.Restack(height)` rather than `factory.StackHeight = 3`.
- When the code needs a new concept, add it to the glossary in the same commit.

## 2. Value objects (start here: M1)

A value object is defined by its value, not by an identity. It's immutable, validates itself on creation, and has value equality.

**Candidates:** `Rate`, `ClockSpeed`, `SomersloopCount`, `StackHeight`, `MachineCount`, `ItemId`, `RecipeId`, `TransportTier`, `ItemBalance`, `Segment`.

`ClockSpeed` is a nice second exercise after `Rate`. It has a range (1–250%), a precision (4 decimals), and behaviour (`RequiredPowerShards`, BR-B10). `SomersloopCount` is only valid relative to a building's slot count. Think about where that check lives: in the value object, or in `ProductionStep`, which knows the building?

Best practices:
- **Make invalid states unrepresentable.** A `StackHeight` of 0 or −1 can't exist, because creation fails. After that, nobody needs to check it again.
- Use `readonly record struct` or `sealed record` for value semantics. Think about the default `struct` value (`default(Rate)` with a 0 denominator!). That's a trap worth its own test.
- **Factory method plus result** for input that can be invalid (`StackHeight.Create(int)` returns a success or a failure with a reason). Use an exception only for programmer errors.
- **Strongly typed IDs** (`BlueprintId`, `FactoryId`) instead of raw `Guid`, so you can't pass a factory ID where a blueprint ID is expected.
- Put **behaviour** on the value object: `Rate` has `+`, `−`, `×`, comparison, `IsZero`, `IsNegative`, `ToDecimal()`. This is not an anaemic DTO.

**M1 exercise, `Rate` (ADR-0004).** Design it test-first. Things your tests should pin down:
- 45/8 and 90/16 are equal and have the same hash code (normalisation).
- A negative sign always lives in the numerator.
- A denominator of 0 is impossible.
- Creation from recipe data: amount × 60 ÷ cycle seconds.
- Arithmetic and comparison operators. `Rate` × integer (machine count, stack height).
- Display conversion with sensible rounding, *only* at the edge.

## 3. Entities and aggregates (M3, M4, M5)

An aggregate is a **consistency boundary**: a cluster of objects that must be valid *together*, changed only through its root, and saved in one transaction.

| Aggregate root | Contains | Invariants it protects |
|---|---|---|
| `Blueprint` | `ProductionStep` entities, declared inputs, main product | BR-B1, BR-B6. Steps reference recipes that exist in the catalog snapshot. |
| `Factory` | blueprint reference, stack height | BR-F1, BR-F3 (stack height within capacity) |
| `SupplyLine` | source, ordered `Tap`s and `Injection`s | BR-S1, BR-S10 (no cycles, which is partly a cross-aggregate check, see below) |

Best practices:
- **Small aggregates.** `Factory` and `SupplyLine` are separate. A world with 80 factories must not load as one giant object.
- **Reference other aggregates by ID only.** A `Tap` holds a `FactoryId`, not a `Factory`.
- **One aggregate per transaction.** If a use case seems to need two, either the boundary is wrong, or the second one should follow through a domain event (eventual consistency).
- **No public setters, no public mutable collections.** Expose `IReadOnlyList<ProductionStep>`.
- **Private constructor for EF, a factory method for the domain** (`Blueprint.Design(name, mainProduct)`).
- **Ask, don't tell**: `blueprint.Balance()` returns the item balances. The application layer doesn't calculate them.

**Thinking exercise:** BR-F3 needs the per-copy rates of the blueprint (another aggregate, another module) and the max belt/pipe capacity (Catalog data, another module). How does the `Factory` aggregate check that invariant without loading the `Blueprint` aggregate? *Hint:* the application layer passes the needed facts in as a value (a "per-copy I/O snapshot"), or the check lives in a domain service. BR-F6 and BR-F7 answer what happens when the blueprint changes *after* the factory was placed: the factory keeps a snapshot of the last balanced inputs and outputs and gets flagged. Work out how that snapshot gets updated (domain event → handler in FactoryNetwork) and write it down as an ADR.

**Follow-up:** BR-B13 means the Blueprint module needs the max capacity too. Since everything is assumed unlocked (Q8), it's plain Catalog data, reached through `Catalog.Contracts` like recipes. Ask yourself: should the module look it up on every check, or receive it as a value? Which one keeps the domain project free of dependencies?

### Invariant or validation state?

Not every rule is an invariant that rejects a change. Compare:
- **BR-B13** (an input over belt capacity): never valid, so the command fails.
- **BR-B14** (unbalanced): normal while designing, so the change is accepted and the blueprint *reports* that it's unbalanced. The rule is enforced elsewhere: placing a factory (BR-F1) checks that the blueprint is balanced.

Ask this question for every rule: "Can the object legitimately be in this state for a while?" If yes, it's a status, not a rejection. Getting this wrong makes a model either unusable (it rejects every intermediate step) or useless (it accepts everything).

## 4. Domain services (M5, M7)

Logic that doesn't naturally belong to one entity: stateless, pure, with a domain-language name.

- `SupplyLineAnalyzer`: walks a line's points and produces a `LineAnalysis` (BR-S2…S6). Pure input → output, which makes it ideal for TDD and property-based testing.
- `LayoutSuggester` (M7): BR-P1…P3. Use the **strategy pattern** behind an interface (`ILayoutStrategy`). Start with a greedy "largest demand first" strategy, and add an exhaustive one for small inputs later. Tests then check that both strategies satisfy the same properties.

A domain service doesn't call repositories or the database. The application layer loads the data and passes it in.

## 5. Domain events

Something happened that domain experts care about: `ProductionStepAdded`, `FactoryRestacked`, `TapAdded`, `SupplyLineRanDry`.

- Name them in the past tense. They're raised by the aggregate and collected on it, then dispatched after a successful save.
- Use them now for in-module side effects and UI updates. Later they become integration events (chapter 08).
- Don't build an event infrastructure before the first real use. A list of events on the aggregate root plus a test that checks it was raised is enough to start.

## 6. Errors in the domain

- Expected business failures (stack too high, step with unknown recipe) → return a **result** with an error code from a closed set (`Factory.StackExceedsBeltCapacity`). The API maps codes to ProblemDetails.
- Exceptions are only for broken invariants that indicate a bug.
- Pick one result approach and use it consistently: a small hand-rolled `Result<T>` in SharedKernel, or a library such as ErrorOr or FluentResults. Record the choice in an ADR.

## Pitfalls

- **Anaemic model:** classes with only getters and setters, and all logic in "services". This is the most common failure in DDD showcases, and reviewers spot it immediately.
- **EF leaking into the domain** (navigation properties added "because EF needs them", `virtual` everywhere).
- **Primitive obsession:** `decimal rate`, `int stackHeight`, `Guid id` everywhere.
- **Over-engineering:** a generic `Entity<TId>` / `AggregateRoot` / `ValueObject` base-class hierarchy before you have three concrete examples. Records already give you value equality.

## Done when (per milestone)
- [ ] Every new concept is in the glossary
- [ ] Every new invariant is in business rules with an ID, and has a test named after it
- [ ] The domain project has no package references (check the csproj)
- [ ] You can explain each aggregate boundary in one sentence
