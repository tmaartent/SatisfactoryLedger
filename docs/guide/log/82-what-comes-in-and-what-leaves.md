# Step 82 · What comes in and what leaves, on a factory's Info tab

**Goal:** a factory's page shows, at the top of its Info tab, its totals: what comes into the factory from outside and what leaves it, per item. Only what crosses its edge, not the intermediate products its machines make and take themselves (asked 2026-11-23). Branch `feat/node-filter`, uncommitted, after step 81 (PR #22 is open).

## How it's built

- **No new figures:** the factory's analysis already has them. Its `input` and `output` are per item, and only what crosses the factory's edge (BR-W4); *At 100%* is net, what its machines take less what they make themselves (step 42). The page's Input and Output tables below the 3D view use the same, with sources, destinations and shortages; the Info tab has the short form: the item with its icon, what crosses now, and at 100%.
- **The tab is the 3D view's**, so the page can't write into it directly. `AreaPage` takes a template for the top of its Info tab (`infoTop`, a `TemplateRef`); the factory page declares the totals as `<ng-template #totals>` and hands it over. The 3D view draws it with `*ngTemplateOutlet`, except while defining, when the tab has the steps. An area drawn on the map gives none. The template is the factory page's, so the factory page's styles reach it (`.totals` in its inline styles, `factory-page.ts`).

Written into ADR-0031.

### Technical choices to overrule

- **Now, then at 100%:** *60/min · 120/min at 100%*. Shortages and where it comes from or goes are in the tables below the 3D view.
- **"Nothing"** under a heading with no items, rather than leaving the heading out.

## Tests

1 new on the factory page: the Info tab's top lists ore coming in and plates leaving, now and at 100%. The spec's stand-in for the 3D view draws `infoTop` as the real one does. 312 web tests pass, exit code 0; lint and the format check pass; the build is clean. Not tried in a browser.
