# Step 126 · Guardrails from the retro of step 125

The retro of step 125 (who owns a shared machine) found mistakes that a check could have caught, or a rule could have prevented. The owner asked for all of them: a lint rule against type assertions, a type-check command, one format for the recorded e2e answers, and the smaller ones.

## Design

No domain module changes; no interface does. What changed is the environment the agent works in.

- **Deletion test:** `disposeGeometry` (`world/stage/stage.ts`) is new. Delete it and the same `'geometry' in object` check reappears in the area and the belt floor, which both clear a scene. It earns its keep.

## What was built

1. **No `as` in the web code** (`@typescript-eslint/consistent-type-assertions`, `assertionStyle: 'never'`, `web/eslint.config.js`). 167 assertions in 31 files went. A script rewrote the DOM look-ups (`querySelector('…') as HTMLButtonElement` became `querySelector<HTMLButtonElement>('…')!`); the rest were rewritten by hand:
   - annotations where the value is `any` (`fixture.nativeElement`, three.js `userData`, `JSON.parse`, Playwright's `answer.json()`);
   - `instanceof` where a type is narrowed (`THREE.Mesh`, `Element`);
   - return types for tuples, and `satisfies MachineStatus[]` for the legends;
   - module augmentation for the Leaflet internals two files use (`remove-map.ts`, `building-outline.ts`);
   - `componentInstance['shapes']` for a private member in a spec;
   - a `FakeRenderer` class in `stage.spec.ts`, so `loop` has its type without a cast;
   - a `Window.maps` declaration in `e2e/arrangement.spec.ts`.

   **Test data the casts had hidden** now has every field: `map-model.spec.ts` (a machine with `contents` it doesn't have and without `inputContents`, buildings without `isGenerator`, a belt without `looseEnds`), `factory-page.spec.ts` (`as unknown as FactoryAnalysisResponse` on data that turned out whole), `map-page.spec.ts` (a `MachineSummary` with only an id).

   **Five assertions stay**, each with `eslint-disable-next-line … -- <why>`: the canvas context that stands in for jsdom's missing one (2), and `{} as TemplateRef<unknown>` in tabs that are never drawn (3).
2. **`ng lint` lints the e2e tests too.** Its `lintFilePatterns` (`web/angular.json`) were `src/` only, so the new rule would have missed `e2e/`; they now take `e2e/**/*.ts` and the root `*.ts`. That found one old finding, a needless escape in `playwright.config.ts`.
3. **`npm run typecheck`**: `ngc` for the app (templates included), `tsc` for the specs and for the e2e tests. It's in CI after the unit tests, and in the `CLAUDE.md` command list. Before, nothing type-checked the e2e tests: Playwright transpiles without checking.
4. **The recorded answers are in Prettier's format.** `e2e/record.mjs` formats what it writes with Prettier and the repo's config, and `e2e/fixtures/` is no longer in `.prettierignore` (the file held only that line, so it's gone). The fixture was reformatted once, the whole of its 20k lines; checked: formatting it again, as the recorder would, gives the same bytes. A hand edit followed by `npm run format` keeps that shape.
5. **The smaller ones:**
   - `CODING_STANDARDS.md`, *Tests*: ages in a test are fixed ids, never `Guid.CreateVersion7()`; and a type assertion left in the web code says why.
   - The primer's `Guid.CreateVersion7()` row says it's only to the millisecond.
   - `CLAUDE.md`: architecture review cards go into `docs/architecture/reviews/<date>.md`, and the step that does a card updates its **Status:** line. Pull requests are opened with `/pr` every time.
   - The user's global `~/.claude/CLAUDE.md` (outside the repo): commit messages from PowerShell 5.1 go through a file.

**Test runs:** web 487 passed (41 files); lint, Prettier and `npm run typecheck` clean; e2e on recorded answers 260 passed, 48 skipped (device-specific), 1 failed: `sweep.spec.ts` "has controls big enough for a finger on a touch screen" on the tablet, a file this step doesn't touch. Run alone three times (`--repeat-each=3`), the tablet's sweep passed every time, so I count it as flaky under the load of the full run.

## TypeScript points

| Here | In Java |
|---|---|
| **`x as T`** tells the compiler to trust you; nothing checks at run time. `{ id: 'a' } as MachineSummary` compiles whatever fields are missing. | A cast `(MachineSummary) obj`, except Java checks it at run time and TypeScript never does. |
| **`satisfies T`** checks a value against a type and keeps its own, narrower type. | No counterpart; the nearest is assigning to a variable of the interface type just to have it checked. |
| **Module augmentation**: `declare module 'leaflet' { interface Map { … } }` adds members to a library's types. | None; you'd reach the field by reflection. |
| **`obj['member']`** reaches a private member in a test, still typed. | Reflection with `setAccessible(true)`, unchecked. |

## Choices to overrule

- **The rule is `never`, not "only object literals"**: every `as` goes, in production code too, and the e2e tests are linted now. Five disables remain for stand-ins that only jsdom's or Angular's absence makes necessary.
- **`fadable` throws on a piece drawn with several materials**, where the cast assumed one; none is.
- **`typecheck` runs in CI as its own step**, after the unit tests (it needs the generated API client).

**Files:** `web/eslint.config.js`, `web/angular.json`, `web/package.json`, `web/playwright.config.ts`, `web/e2e/record.mjs`, `web/e2e/fixtures/api.json`, `web/.prettierignore` (removed); the 31 files the rule found, under `web/src/app` and `web/e2e`; `web/src/app/world/stage/stage.ts`; `.github/workflows/ci.yml`; `CLAUDE.md`; `CODING_STANDARDS.md`; `docs/guide/primer.md`; this log.
