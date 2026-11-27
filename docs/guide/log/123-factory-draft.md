# Step 123 · The factory draft leaves the 3D view

**Goal:** card 6 of the architecture review of 2026-11-26: take the define-a-factory workflow out of `AreaPage`, so the factory's page no longer reaches into the 3D component, and the workflow gets unit tests without WebGL. A **pure refactor**: nothing the player sees changes (grilling Q2). Card 5 (one 3D stage) is out of scope (Q1). Built with `/implement` in the grilling's own session (Q8); branch `refactor/factory-draft`, from `main`.

## Design

- **Module:** `FactoryDraft` (`web/src/app/world/factory-draft.ts`), the code home of the new glossary term **Factory Draft** (Q3): a factory being defined or edited and not saved yet. It owns the machines chosen, the name, BR-W5 for choosing, the debounced **Factory Preview**, and saving.
- **Interface (Q12):** `within({ snapshotId, world, factories })` once; read-only `defining`, `editing`, `chosen`, `blocked`, `canSave`, `saveError`, `preview`; the writable `name`; `start`, `edit(factoryId)`, `choose(id)`, `box(ids, mode)`, `stop`, `save`; the event `saved$`.
- **Interface change, AreaPage:** its public `defining`, `factoryName`, `canSave`, `saveError`, `edit`, `save`, `stopDefining` and the `saved` output are gone. Nothing outside it calls into it any more: `FactoryPage` and `AreaPage` both talk to the draft.
- **Interface change, selection:** `boxSelect(chosen, onScreen, box, mode)` becomes `inBox(onScreen, box)`. It's screen geometry only; adding or taking out is the draft's `box`.
- **Lifetime (Q4):** one draft per page. `FactoryPage` gives `providers: [FactoryDraft]`; `AreaPage` gives `providers: [ownDraftOrThePages]`, the page's draft when it's inside one, else its own.
- **Deletion test:** delete `FactoryDraft` and its state goes back into the 1000-line component, with the factory page reaching into it through `viewChild` and a stub that copies its fields. It earns its keep: it's the only thing both pages share, and its 17 tests run without a browser.

## How it was built (`/tdd`, seams agreed in Q12 to Q14)

Every slice started red: first a compile error (no such member), then green with only what the slice needed.

1. `start`, `choose` toggles, `canSave` (a machine, a name, no save running), `stop`: 5 tests.
2. BR-W5: `choose` and `box` skip another factory's machines; `edit` loads a factory's machines and name, and its own machines stay choosable; `edit` does nothing before the factories are known: 4 tests.
3. The preview follows the choice after a 300 ms pause, only while defining (fake timers): 2 tests.
4. `save`: define for a new factory, edit for an existing one, trimmed; `saved$` fires; not twice at once; a refusal keeps the draft and says why: 4 tests.
5. Rewire both pages. `factory-page.spec.ts` now drives the **real** draft through the page's header and checks what reaches the `Api` fake; its `AreaPageStub` lost its 10 copied workflow fields and keeps only what the real 3D view does for the draft (`within`).
6. Found while writing this log: the provider. I first put `providers: [FactoryDraft]` on the `/area` **route**. A route's injector outlives the route, so a half-made draft would have survived leaving the 3D view. Before this step the state lived in the component and was gone. Two red tests on `ownDraftOrThePages` (the page's draft is shared; a page of its own has its own, gone with it), then the provider moved onto `AreaPage`.

**Tests changed:** `factory-page.spec.ts`. Its tests checked calls on the stub (`area().edit`, `area().save`, `area().stopDefining`, `area().saved.emit`); they now check the requests made and what the page shows. Two tests were merged into the new "saves its machines under the name typed, then asks for its findings again", and one new one checks a refused save. `selection.spec.ts`: the two `boxSelect` tests (add, take out) became one `inBox` test; add and take out are in the draft's tests.

**Test runs:** web 475 passed (40 files); lint clean; `ng build` clean; e2e on recorded answers 261 passed, 48 skipped (device-specific); the journey against the real stack 1 passed. Its first run failed at "its page shows Iron Plate", for the reason log 97 names: the Development world still had the *Iron Plates* factory, which owned the smelters, so BR-W5 kept them out of the journey's box (the code before this step skipped them the same way). With the owner's agreement that factory was deleted, and the journey passed.

## Angular / TypeScript points

| Here | In Java / Spring |
|---|---|
| **`@Injectable()` without `providedIn`**, listed in a component's `providers`: one instance per component tree, destroyed with it. Primer: *A service per page*. | A bean in a scope tied to one page; `providedIn: 'root'` is the singleton. |
| **A route's `providers` outlive the route.** That's why the 3D view provides its own draft. Primer: *A route's `providers` outlive the route*. | No direct counterpart; like a bean scoped to a URL mapping that's never torn down. |
| **`useFactory` + `inject(FactoryDraft, { skipSelf: true, optional: true }) ?? new FactoryDraft()`**: the parent's instance, or a new one. Primer: *`useFactory` with `inject(X, { skipSelf… })`*. | `ObjectProvider<X>.getIfAvailable(X::new)`. |
| **`saved$`, a `Subject` as `Observable`**, with `takeUntilDestroyed()` in both pages' constructors. A signal wouldn't tell "saved again" for the same id. Primer: *An event from a service*. | `ApplicationEventPublisher` and an `@EventListener`. |
| **`asReadonly()`**: the draft writes `_chosen`, the pages read `chosen`. A `ReadonlySet` inside keeps `add` from compiling. | A private field with a getter returning `Collections.unmodifiableSet`. |
| **A signal set by `within` instead of a plain field**: `blocked` is a `computed` over `context()`. Read before `within`, a plain field would have cached an empty set for good, because a `computed` only re-runs when a signal it read changes. | No counterpart; Java getters compute on each call. |
| **Fake timers:** `vi.useFakeTimers()`, `TestBed.tick()` to run the effects, `vi.advanceTimersByTime(299)`. Primer: *Fake timers with signals*. | Awaitility with a fake `Clock`. |

## Choices to overrule

- **The draft stops before `saved$` fires.** AreaPage emitted `saved` and then stopped. Now a subscriber never sees a draft that's saved but still defining. Nothing reads the draft in between, so the player sees the same.
- **`choose` checks `defining` itself.** AreaPage checked it before calling. The rule now sits with the state it guards.
- **AreaPage doesn't read `draft.blocked()`**, though Q12 said it would (found in review). It passes every machine or generator tapped or boxed, and the draft leaves out another factory's. So BR-W5 is decided in one place; `machinesOnScreen` lost its own `blocked` filter. `blocked` stays public: the draft's tests read it, and a 3D view could grey those machines out with it.
- **`start`, `edit` and `stop` all go through one private `reset`** (found in review: the three repeated the same resets). `start` now also empties the choice and the name; `stop` had already done that, and the *Define a factory* button only shows after a `stop`.
- **`choosable` (is it a machine or generator) stays in AreaPage.** The draft only sees ids; what kind of building a mesh is belongs to the 3D view.
- **AreaPage opens the Info tab from an `effect` on `draft.defining()` (Q10)**, so it also opens when the factory's page starts editing from its header. Before, `startEditing` called `showStepsBeside` directly, so it's the same.
- **AreaPage shrank by 90 lines (1126 → 1036)**, not the "hundreds" the card guessed. The workflow was smaller than the card's diagram made it look; most of AreaPage is the 3D stage, which is card 5.
- **The file sits in `world/`**, next to `factory-deletion.ts`, not in `world/area/`: both pages use it, and it has nothing 3D.

**Files:** `web/src/app/world/factory-draft.ts`, `factory-draft.spec.ts` (new); `world/area/area-page.ts`, `area-page.html`, `selection.ts`, `selection.spec.ts`; `world/factory-page.ts`, `factory-page.html`, `factory-page.spec.ts`; `GLOSSARY.md` (Factory Draft); `docs/guide/primer.md`; this log.
