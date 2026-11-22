# Step 63 · A sidebar of tabs

**Goal:** the explanation panel from step 62 took 380 px of its own, which worried you for the responsive site. Its collapsible bar on the right becomes a permanent part of every World page, and takes over the pages' own side panels (asked 2026-11-22). Branch `feat/mcp-server`, uncommitted, on top of step 62.

## What you decided

- **One sidebar on the right of every World page:** a rail of icons and the open tab. Clicking the open tab's icon collapses it to the rail. **Open by default.**
- **The pages' tabs:**

  | Page | Tabs | What moved there |
  |---|---|---|
  | World map | Factories · Snapshots · Explanation | the factories list right of the map, the snapshots column left of it |
  | Area in 3D | Info · Factories · Explanation | the panel right of the 3D view (*Factories here* is the Factories tab) |
  | Factory | Info · Explanation | the panel right of its 3D view |
  | Snapshot | Explanation | — |

- **Clicks show the tab that answers them, expanding the sidebar if it was collapsed:** a building in 3D shows Info (with *Explain why*, which stays), defining a factory shows Info (the steps), and a question shows Explanation.
- **Save and Cancel in the header** while defining a factory in the 3D view, as on the factory page.
- **The Explanation tab with nothing asked** says where to ask.
- **On the map, a click on a starved or output-blocked machine of no factory** asks why. A machine of a factory still opens the factory's page. Its tooltip says *Click to explain why*.

Written into ADR-0031, with ADR-0030's "stays beside every page" now pointing at the tab.

## How it's built

```
App shell (.shell, flex)
├─ main → <router-outlet>                 a page declares its tabs:
│                                           <ng-template appSidebarTab="info" label="Info" icon="info">…</ng-template>
│                                                │ SidebarTab directive: inject(TemplateRef) → add on init, remove on destroy
├─ @if World page: <ng-template appSidebarTab="explanation" [order]="9"> @defer { <app-explain-panel/> }
└─ @if any tab: <app-sidebar>             ▼
                 open tab (340 px) ◄── SidebarTabs (root): tabs, current, collapsed, show(key), toggle(key)
                 rail (48 px)                ▲ show('explanation')        ▲ show('info')
                                          Explainer                   AreaPage (click, define, edit)
```

- **`SidebarTabs`** (`sidebar/sidebar-tabs.ts`) is the state:
  - the tabs, sorted by their `order`;
  - the key last asked for (`chosen`), and `current`, which is that tab if the page has it, else the first;
  - `collapsed`.
  - `show(key)` opens a tab and expands the sidebar; `toggle(key)` is the rail's click.
- **`SidebarTab`** (`sidebar/sidebar-tab.ts`) is a directive on `<ng-template>`. It hands its template to the service while the page lives.
- **`Sidebar`** (`sidebar/sidebar.ts`, `.html`, `.scss`) lays it out:
  - the open tab with a header (its label, collapse) and the template drawn with `*ngTemplateOutlet`;
  - the rail.
  - Below 960 px the open tab lies over the page.
- **`App`:**
  - `inWorld` is a signal of the router's URL (`toSignal` over `NavigationEnd`). On World pages it declares the Explanation tab.
  - The sidebar is shown while there are tabs.
- **`Explainer`** lost `collapsed`: `explain()` calls `sidebar.show('explanation')`.
- **`ExplainPanel`:**
  - no collapse button of its own; its close button is now *Clear*;
  - with no question, the hint.
- **`MapPage`:**
  - the factories (`order` 0) and `<app-snapshots-sidebar>` (`order` 1) are tab templates;
  - `.with-sidebar` and `.map-row` are gone (the global `.with-sidebar` too);
  - `explainsWhy()` puts a click handler on a held-back machine of no factory.
- **`AreaPage`:**
  - the `<aside>` became the Info tab;
  - *Factories here* became the Factories tab (`order` 1), with *No factory has machines here* when empty, and only when not embedded;
  - the name, Save and Cancel moved into the header;
  - the click handler, `startDefining` and `startEditing` show Info.
  - The factory page needed no change: its embedded 3D view brings the Info tab.

### Technical choices to overrule

- **Tab icons:** `factory`, `folder_open` (Snapshots), `info` and `lightbulb`. The tab is 340 px wide.
- **The explanation's close button now clears it.** The tab stays, showing the hint.
- **Native `title` tooltips on the rail**, not Material's: see the primer.

## Tests

| File | What |
|---|---|
| `sidebar-tabs.spec.ts` (new, 5) | Open on the first tab; the explanation last whatever the order they're added in; `show` expands; the open icon collapses and opens again; the tab last asked for comes back with a page that has it |
| `sidebar.spec.ts` (new, 5) | A test page with two tabs: an icon each and the first open; the tab follows the page's own state; another icon opens its tab; the open one collapses; a tab the page takes away goes |
| `app.spec.ts` | The sidebar with the Explanation tab and its hint on a World page; the question in it; no sidebar in the Catalog |
| `explainer.spec.ts` | A question shows the Explanation tab, expanding a collapsed sidebar |
| `explain-panel.spec.ts` | *Clear*, then the hint (replaces collapse and close) |
| `map-page.spec.ts` | Factories and Snapshots read from a `Sidebar` beside the page; only Snapshots with no snapshot; *Click to explain why* in the tooltip of a starved smelter of no factory |

245 web tests pass. The build is clean, with the initial bundle at 464 kB (budget 500). Not checked in a browser yet. The 3D view's clicks are not unit-tested, because jsdom has no WebGL. Nor is the map click itself, since Leaflet draws on a canvas that jsdom doesn't have. Only its tooltip line is tested. The `clearRect` errors in `map-page.spec.ts` were there before this step.

## Angular notes

- **A template drawn in another component** (primer): the Info tab is the area page's template, so `selected()`, `startEditing()` and its `.panel` styles still work when the shell's sidebar draws it. Emulated style encapsulation marks elements by the component whose template they're written in, not by where they end up.
- **Tab order is not declaration order** (primer): the map's Factories tab sits inside `@if (id())`, so it's created after the Snapshots tab written below it. Hence `order`.
- **Material's tooltip is 70 kB** (primer): it put the shell over its budget.
- **`toSignal(router.events.pipe(...))`**: an RxJS stream as a signal, the bridge between the router (still Observables) and signal templates. Like adapting a `Flux` into a value holder that the view reads.
