# Step 77 · Bottlenecks and supply on the map

**Goal:** the map's Info tab gets a card for the bottlenecks, and the map gets two tabs, Unused supply and Unknown supply, with what the findings page lists of them (asked 2026-11-23). Branch `feat/node-filter`, uncommitted, after step 76.

## How it's built

- **`ShownStatus`** (`shown-status.ts`) is now a union: the machines or generators of a status, as before, or `{ of: 'bottlenecks' }`. A TypeScript *discriminated union*: `of` tells which, and after `shown.of === 'bottlenecks'` the compiler knows there's no `limit`.
- **`buildingsShown`** (`map-model.ts`) gives the bottlenecks' ids for it: belts and pipes. The map's `highlighted` set then holds them, so every building is dimmed, and `fitToHighlighted` rings each one halfway along (`locate`, as a finding's focus does) and zooms to fit them.
- **`FindingsSummary`** takes `bottlenecks`, a count, and shows it in a row of its own only when it's given: the map gives `findings.bottlenecks.length`; the findings and factory pages give nothing and are unchanged. Red when there are any, green at none. Two counts' statuses are compared field by field (`sameStatus`).
- **The two tabs** (`map-page.html`, `unused` and `unknown`, order 0.2 and 0.4: after Factories, before Snapshots), only with a snapshot. A list rather than the findings' table: the sidebar is narrow.
  - **Unused supply:** each source with its icon, linked to it on the map (`[routerLink]="[]"` with `?focus=`, which the map already follows), and its item, *gives 120/min of 480/min*. The most left over first (`unusedSupply`).
  - **Unknown supply:** each source, *its stock confirms it can* where it does, and per item what it's assumed to give, the most first (`deliveries`), and how long its stock lasts.

Written into ADR-0031.

### Technical choices to overrule

- **The tabs' icons:** a piggy bank (`savings`) for unused, a question mark (`help_outline`) for unknown.
- **Unused supply sorted by what's left over**, not as the analysis orders it.
- **The bottleneck row is the map's only:** the findings page has its Bottlenecks tab.

## Tests

5 new: the bottleneck row, shown when given and clicked to show them; none without a count (`findings-summary.spec.ts`); bottlenecks as what's shown (`map-model.spec.ts`); and on the map page, the two tabs' entries with their links. The map page's tab list has the two tabs too. 309 web tests pass, exit code 0; lint and the format check pass; the build is clean. Not tried in a browser.
