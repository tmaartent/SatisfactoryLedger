# 06 · Frontend (Angular 22)

The scaffold in `web/` is the default Angular 22 setup: standalone components, zoneless change detection, Vitest, SCSS. Keep it modern. Don't import patterns from old Angular tutorials (NgModules, Zone.js, `*ngIf`).

## Structure

```
web/src/app/
  core/            app-wide singletons: HTTP interceptors (ProblemDetails → errors), layout shell
  shared/          dumb reusable UI: rate display, balance badge (green/red), item icon
  api/             GENERATED client from openapi.v1.json. Never edit by hand.
  features/
    catalog/       browse items and recipes
    blueprints/    list, designer, balance view
    factories/
    supply-lines/  line editor + analysis
    world/         network graph
  app.routes.ts    lazy-loaded feature routes (loadComponent / loadChildren)
```

Features mirror the backend modules and use the same domain words. A feature may use `shared/` and `api/`, but never another feature's internals.

## Modern Angular practices

- **Signals for state:** `signal`, `computed`, `linkedSignal`, `effect` (sparingly, only for side effects that leave Angular).
- **Data fetching with `httpResource` / `resource`:** declarative loading, error and value states driven by signals. Reach for RxJS only where streams really fit (debounced search, websockets).
- **New control flow** (`@if`, `@for` with `track`, `@switch`, `@defer` for the heavy graph view).
- **`input()`, `output()`, `model()`** functions instead of decorators. `inject()` instead of constructor injection.
- **Smart vs presentational components:** a page (container) talks to services and state. Presentational components only get inputs and emit outputs. They're easy to test and reuse.
- **Forms:** the blueprint designer is a dynamic form (a list of steps). Look at Angular's **signal forms**, checking their stability status in your version, or typed reactive forms with `FormArray`.
- **State per feature:** a signal-based store service per feature (or NgRx SignalStore if you want to learn it) is enough. Don't introduce global Redux-style state without a reason.

## Generated API client (ADR-0006)

- Generate with **orval** or **openapi-generator** from `../api/openapi.v1.json` into `src/app/api/`, via an `npm run generate:api` script.
- Map ProblemDetails error codes to user-friendly messages in **one place** (an interceptor plus a message catalog).
- Show rates with a single `RatePipe` or `<app-rate>` component: always the same precision and unit.

## Making it look slick

- Pick **one** component library and stick to it. Angular Material (with M3 theming) is the safe choice. Tailwind for layout and spacing combines well with it. Define colour tokens once: *balanced* (green), *deficit* (red), *surplus/spare* (blue/amber), *stranded* (grey).
- A dark theme by default suits the game's industrial feel.
- **The showcase view is the world graph (M6):** factories as nodes, supply lines as edges, edge thickness = flow, red = deficit. Candidates: **ngx-vflow** (Angular-native node editor), or **Cytoscape.js** / **ELK.js** for automatic layout. Try one in a spike branch first.
- **Supply line analysis view:** a horizontal "belt" diagram with taps and injections in order, flow per segment, and the overflow branch drawn off the merger. It's the visual version of the worked example in business rules.
- Accessibility: never use colour alone. Pair green/red with an icon and text ("deficit 8.75/min").

## Testing

- **Vitest** (scaffold default) + **Angular Testing Library**: test components the way users use them (find by role or label, click, assert visible text), not through component internals.
- Test presentational components with inputs, and pages with the API client mocked at the HTTP level (`provideHttpClientTesting`).
- **Playwright** for a few end-to-end journeys against the real backend (via Aspire): "design a blueprint → see it balanced", "add a tap → line turns red".

## Pitfalls
- Business rules in the frontend. The balance is computed by the API, and the UI only displays it. Duplicated logic will drift.
- Hand-written interfaces that duplicate the generated client.
- One giant component per page. Split into presentational parts early.

## Done when (per feature)
- [ ] Lazy-loaded route, standalone components, signals for state
- [ ] Uses only the generated client for HTTP
- [ ] Loading, empty and error states designed, not just the happy path
- [ ] Component tests with Testing Library, plus an e2e journey for each milestone
