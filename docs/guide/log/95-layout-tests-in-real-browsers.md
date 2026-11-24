# Step 95 · Layout tests in real browsers

**Goal:** keep what steps 90 to 94 fixed on phones and tablets from breaking again, in CI (asked in step 90: "do we need new CI tests for responsiveness?"; taken up 2026-11-24). Branch `feat/playwright-layout`, from main.

## The owner's choices

- **Both:** layout tests against recorded API answers in every CI run, and a few journeys against the real stack. This step does the first; the second is step 96.
- **Assertions,** not screenshot comparisons.
- **Four devices:** a phone with Chromium (Pixel 7), a phone with WebKit (iPhone 15), a tablet (iPad, WebKit) and a desktop with a mouse (1280 × 800).

ADR-0008 had Playwright for "a few critical user journeys against the real backend": amended.

## What was built

- **Playwright** 1.65.1 (`@playwright/test`), and `@types/node` for the config's `process.env`.
- **`web/playwright.config.ts`:** the four devices as projects; Playwright starts `ng serve` on port 4300 (beside the AppHost's 4200, which it reuses locally if it runs).
- **The recording:**
  - `web/e2e/record.mjs` opens the map, the 3D view and the arrangement of the test world (`Development.sav`, imported into the running app) and saves every GET answer of the API into `web/e2e/fixtures/api.json` (418 kB, 11 answers). `npm run e2e:record -- <snapshot id>`.
  - Left out: map art and icons (the game's art), the player's other worlds from the snapshot list, and the resource-node table but for the test world's own nodes (it comes from the player's `resource-nodes.json`, which stays out of the repo).
  - Kept out of Prettier (`.prettierignore`).
- **`web/e2e/api.ts`, `answerFromRecording`:** answers the API from the recording with `page.route`; map art and icons get a 404, and so does anything else, which the test then lists.
- **`web/e2e/layout.spec.ts`**, run once per device. An `auto` fixture answers the API for every test and fails it when the page asked for something not recorded.
  - The world map: no sideways scroll; the sidebar collapsed on a touch screen and open with a mouse; the legend folded on a phone; on a touch screen, the select button draws an area and the choices *3D, Arrange, Cancel* show, wholly in view; with a mouse, ctrl-drag opens the arrangement; the snapshot picker's arrow at its right edge.
  - The 3D view: no sideways scroll; the canvas as big as its stage (step 90's bug); the hint for fingers or a mouse; the sidebar collapsed on a touch screen.
- **npm scripts** `e2e` (with `pree2e` generating the API client, as `pretest` does) and `e2e:record`.
- **CI job `e2e`** ("Layout (Playwright)"): Node, `npm ci`, the two browsers with their Linux libraries, `npm run e2e`, the report uploaded when it fails. No .NET, no PostgreSQL.
- `web/e2e/tsconfig.json`, to type-check the tests (`npx tsc -p e2e/tsconfig.json`): Playwright runs TypeScript without checking types.

### More tests, asked after the first review

The owner asked how complete the tests were, then for the first two of five follow-ups:
- **Defining a factory in 3D** (`e2e/define.spec.ts`): the steps' tab stays closed where it would cover the view; the help list with a mouse only; on a touch screen the select button draws a box and offers *Add, Take out, Cancel*, in view also for a box down to the bottom of the view; Add chooses the machines and the Info tab shows what they make; Cancel chooses nothing; with a mouse, shift-drag chooses. The preview is a POST, answered in the test itself.
- **The snapshot picker** stays in view with the sidebar collapsed (`layout.spec.ts`).
- **The optimal arrangement** (`e2e/arrangement.spec.ts`): no sideways scroll; the intro for fingers or keys; on a touch screen a selection offers *Area, Keep out, Cancel*, in view, and the page stays selecting after *Keep out*; with a mouse no select button.
- The fixture and helpers moved to `e2e/test.ts`, for every test file: `test` (with the recorded answers), `pages`, `touch`, `phone`, `covered`, `noSidewaysScroll`, `inView`, `dragOver`.

**Found by them, and fixed:** on a tablet (an iPad, 810 px wide), starting to define a factory opened the Info tab **over the 3D view's select button**: the tab lies over the page up to 960 px wide (`sidebar.scss`), but only a phone kept it closed. `SidebarTabs.closeOnPhone` is now `closeWhereItCovers` (`(max-width: 960px)`), with a unit test for a tablet. ADR-0033 updated.

**What the tests had to learn to act like a person:**
- A drag only where the element is what a finger touches: on a phone the bar of tabs lies over the 3D view's bottom, and a drag let go there never reached the view (`dragOver` moves such a point up).
- Scroll to it first: the arrangement's map starts below the window on a phone; a person scrolls to it before drawing.
- "In view" means touchable too: `inView` checks that the element is what lies at its middle, not something over it.
- Wait for the world, not just the canvas: the 3D view's canvas stands before the world arrives, and a box drawn then was lost under load. The 3D tests wait for "5 machines".
- Expectations get 15 s (`expect.timeout`): with four browsers at once the dev server is slow to serve the first pages. One that holds passes at once.

### The sweep: every page against rules that hold everywhere

Asked after the owner found hand-written tests hard to make (2026-11-25): `e2e/sweep.spec.ts` opens every page (the start map, the Catalog, a snapshot's map, the 3D view, the optimal arrangement) on every device and checks:
- **no sideways scroll;**
- **a map page fits a phone without scrolling up and down:** it's made to fill the screen above the picker and the bar of tabs;
- **no control covered:** at the middle of every visible button, link and field, a finger touches that control. A form field's own label doesn't count, and a control under a bar fixed to the window is first scrolled free, by the page only, as a person would;
- **targets of 24 × 24 px at least** on a touch screen (WCAG 2.2 AA, target size), a Material button with its touch target;
- **accessibility:** nothing axe (`@axe-core/playwright` 4.14.1) finds against WCAG 2.1 A and AA.

A new page needs only its address in the sweep's list. The first versions of the rules were too strict, or too lenient, in turn: a form field's label "covering" its input; scrolling the map's own containers, which moved the legend out from under what covered it; checking before the picker came.

**Found by the sweep, and fixed:**
- **The start map on a phone ran 56 px under the snapshot picker:** its two lines of intro push it down, and the **Legend** button lay under the picker's touch target, so a tap on it opened the snapshot menu. The start map is shorter on a phone (`.map.with-intro`).
- **The snapshot picker could take very long to come in the 3D view, or not at all on a busy phone:** it loads when the browser is idle (`@defer (on idle)`), and the 3D view draws every frame. Now `@defer (on idle; on timer(2s))`: on idle, or after 2 s at the latest.
- **The Catalog's and the factory page's wide tables** scroll sideways on a phone, but a keyboard couldn't reach them: `tabindex="0"`, `role="region"` and a name (axe: scrollable-region-focusable).
- **The snapshot picker's name for a screen reader**, "Snapshot: none picked", didn't hold what it shows, "Pick a snapshot", which voice control goes by: now "Snapshot: Pick a snapshot" (axe: label-content-name-mismatch).

The suite now runs four browsers at a time locally (`workers: 4`), with 60 s a test: with eight, the laptop starved the pages.

### Technical choices to overrule

- **One recording, by path.** The tests use the recorded snapshot's id. A page that asks for a new endpoint fails its test until it's recorded again.
- **The tablet is an iPad on WebKit.** A Chromium tablet (Galaxy Tab) would test Chrome on a larger screen instead.
- **Skips, not separate files, per device:** a test that has no meaning on a device says why it's skipped (ctrl-drag on a touch screen, the select button with a mouse).
- **Retries in CI:** one, to ride out a slow start; locally none.

## Also: the arrangement's area is blue

As asked after step 94: the arrangement page draws its area, the rectangle being drawn and a run's dashed outline in the blue of the ctrl rectangle on the world map (`dragColours.arrangement`); keep out stays red. A separate commit.

## Do the tests catch what they guard?

Two earlier fixes undone for a moment, then run:
- the canvas sized to twice its stage: *the canvas as big as its stage* failed on all four devices;
- the sidebar open from the start: *the sidebar collapsed on a touch screen* failed on the three touch devices (the 3D view on a phone still closes it, as `closeOnPhone` does, and passed there).

Both restored.

## TypeScript / testing points

- **`page.route`** answers inside the browser, before a request leaves: the dev server's proxy to the API is never asked. WireMock does it from outside the process.
- **Fixtures** (`test.extend`) are named as a test's parameters and built for it, like a JUnit 5 parameter resolver; `auto: true` runs one for every test unasked.
- **Waiting is built in:** `expect(locator).toBeVisible()` retries until it holds or times out, so the tests have no sleeps.
- **Import attributes:** `import recorded from './fixtures/api.json' with { type: 'json' }`.

## Tests

- 188 runs (47 tests × 4 devices): 149 pass, 39 skipped as meant; about two minutes locally, twice in a row. Exit code 0.
- 394 web unit tests, lint, format check, build, and `tsc -p e2e/tsconfig.json` pass.
- The `npm audit` findings (vite, typescript-eslint, minimatch) were there before this step; not touched here.
