# Step 62 · The explanation stays

**Goal:** following a link in the explanation (to the map, then the 3D view, where you found the break in the Steel Pipe belt) closed the explanation. It should stay (asked 2026-11-22). Branch `feat/mcp-server`, uncommitted.

## What you decided

- **A panel docked right**, not a dialog. It lives in the app's shell, so it's beside every page: the map, an area's 3D view, the factory page and the snapshot page.
- **Fixed width (380 px), collapsing to a strip** with the lightbulb, to give the page its width back.
- **One at a time:** a new question replaces it.
- **It closes on a switch to another snapshot**, where its links would lead into another world. It stays on pages of no snapshot, such as the Catalog.
- **On narrow screens (below 960 px) it lies over the page** from the right.
- **Links still go to the map.** Highlighting a building in the 3D view you're in is for later.

Written into ADR-0030.

## How it's built

```
App shell
├─ toolbar
└─ .shell (flex)
   ├─ main → <router-outlet>          (any page)
   └─ @if question → @defer → <app-explain-panel>
                                        ▲ reads
Explainer (root service): question, models, model, asking, explanation, error, collapsed
   ▲ explain(machine, snapshotId)
findings table · 3D panel (area page, also inside the factory page)
```

- **`Explainer`** (`world/explain/explainer.ts`) holds the state that the dialog component held. It's created once for the app, so it outlives navigation.
  - `explain()` sets the question and asks. A counter drops an answer that arrives after a newer question was asked.
  - The model list is loaded once.
  - A `NavigationEnd` listener closes it when the URL's snapshot id changes.
- **`ExplainPanel`** (`explain-panel.ts`, `.html`, `.scss`) replaces `ExplainDialog`: the same content, with a header (collapse, close) and a footer (model choice, Explain again). Its links lost `mat-dialog-close`.
- **`App`** shows the panel beside `<main>`. It's `position: sticky` below the toolbar, at the window's height, and on narrow screens `position: fixed` over the page.
- **`@defer`**: the panel's code loads with the first question. Without it the initial bundle went to 630 kB, over the 500 kB budget; with it, 469 kB.
- **The Leaflet map** (`map-page.ts`) follows the window's size only. A `ResizeObserver` calls `invalidateSize()` when the panel opens or collapses. The 3D views had one already.

## Tests

| File | What |
|---|---|
| `explainer.spec.ts` | Explainable or not; the question named in game names, asked with the first model; a newer question wins over a late answer; reopens when collapsed; stays across links and pages without a snapshot; closes on another snapshot |
| `explain-panel.spec.ts` (was `explain-dialog.spec.ts`) | As before, plus collapse and back, and close |
| `app.spec.ts` | No panel until a question; then beside the page |
| `findings.spec.ts` | The button asks the `Explainer`, not a dialog |
| `map-page.spec.ts` | A `ResizeObserver` stub: jsdom has none |

232 web tests pass, and the build is clean. Not checked in a browser yet.

## Angular notes

- **State in a root service, not the component:** a component dies with its route or dialog, while a `providedIn: 'root'` service lives as long as the app. That's how the explanation survives navigation. In Spring terms, it's a singleton bean versus a request-scoped one.
- **`@defer`** (primer): a block whose components load as their own chunk the first time it renders.
- **`host: { '[class.collapsed]': ... }`** binds a class on the component's own element, so the shell's stylesheet can tell a collapsed panel apart (`app-explain-panel:not(.collapsed)`).
