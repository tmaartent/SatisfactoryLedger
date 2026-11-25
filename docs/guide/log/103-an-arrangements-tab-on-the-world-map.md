# Step 103 · An Arrangements tab on the world map

**Goal:** the optimal arrangement could only be reached by drawing a rectangle on the world map: a ctrl-drag (step 83), or on a touch screen selecting an area and choosing Arrange (step 91). That was its only way in once the findings page went (step 81). The owner found that not ideal and asked for a tab on the world map (2026-11-25). Branch `feat/arrangement-touch`, after step 102.

## The owner's choice

Of four options (the runs, an *Arrange an area* button, a run's area outlined on the map, delete buttons), **only the world's runs**.

## What was built

- **The Arrangements tab** (`map-page.html`), right after Factories:
  - The world's runs of the optimal arrangement, newest first, as the API lists them (`getArrangements` by the snapshot's world name, as the arrangement page asks).
  - Each run is a link to `/snapshots/{id}/arrangement?run={run}`, so the run opens on its page. It shows when the run was asked for (`d MMM, HH:mm`) over its summary, `runSummary` from `arrangement-model.ts`, so it reads as in the run picker: "6 × 6 · roads 4 · 4 · −34% · 1 not routed · Optimal".
  - With no runs it says how to make one: *None yet. Hold ctrl and drag over an area to arrange its factories, or select an area and choose Arrange.*
  - While loading, a progress bar; if the call fails, the problem.
- **`MapPage.arrangements`**, a `resource` that waits for the snapshot's world name, and `runSummary` passed to the template.
- **Styles:** a run's layout, date over summary, is now one global rule for the run picker's list and the tab (`styles.scss`). The tab's link style is global too: the map page's own stylesheet went 260 bytes over Angular's 4 kB budget per component, as in step 91.

### Choices to overrule

- **The tab's place, name and icon:** after Factories, "Arrangements", `grid_view` (the Arrange button's icon).
- **The runs are the world's, not only this snapshot's,** as on the arrangement page. A run of another snapshot of the same world opens on this snapshot's page, as picking it in the run picker does.
- **No other way in:** the arrangement page opened without a run or area still shows the newest run. A *New arrangement* link wasn't asked for.

## Angular points

- **`[routerLink]` with `[queryParams]`:** `['/snapshots', id, 'arrangement']` plus `{ run: run.id }` builds `/snapshots/…/arrangement?run=…`, and the link is a real `<a href>`: middle-click and *open in new tab* work. Spring MVC's nearest is `UriComponentsBuilder` in a Thymeleaf `th:href="@{…(run=${id})}"`.
- **A `resource` that waits for another's value:** `params` returns `undefined` until the snapshot is loaded, and the loader doesn't run before then. It's a dependent query without `switchMap`; Java has no counterpart in the template layer.
- **Component style budgets:** `angular.json` warns at 4 kB of styles per component, and fails at 8 kB. Shared rules move to `styles.scss`, where Angular's view encapsulation doesn't apply. See `primer.md`, *Frontend*, "A style budget per component".

## Tests

- **`map-page.spec.ts`**, 3 new, each failing before the code:
  - the tab lists the world's runs, newest first, each summed up in a line, and asks the API for the world's runs;
  - a run links to its arrangement page;
  - with no runs, it says how to arrange an area.
- **Changed:** the tab order test now has Arrangements after Factories.
- **Results:**
  - 429 web tests pass, exit code 0; lint, the format check and the e2e type check pass. The build is clean, with no budget warning.
  - **Playwright:** 223 passed, 47 skipped, 3 failed in the full run, all on the factory page (axe on an iPhone, and fit and finger size on a tablet), which this step doesn't touch. Rerun alone, all 10 of the factory page's sweep tests pass on tablet and iPhone: flaky under the full run's load.
- **Not tried in a browser:** the AppHost had stopped.
