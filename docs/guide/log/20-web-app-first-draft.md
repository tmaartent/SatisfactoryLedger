# Step 20 · The web app, a first draft

**Goal:** a usable UI for the API: import a save, watch its analysis, read the findings, define factories, browse the catalog.
**Result:**
- **What it does:** an Angular 22 app with Angular Material (dark theme) and a client generated from `api/openapi.json`.
- **How it runs:** Aspire starts it next to the API and the analysis service. The dev server proxies `/api` to the API found by service discovery.
- **Tests:** 15 Vitest tests, green; two of them mutation-checked. The build is green. Checked under Aspire: `http://localhost:4200/api/v1/world/snapshots` reaches the API through the proxy, and the findings and a deep link answer 200.
- **Not checked:** the screens in a browser. I can't see them, so the layout is yours to judge.

**Decisions you made (ADR-0019):** ng-openapi-gen; Angular Material.

**Decisions I made, for you to overrule:** see "Choices worth a second look".

## What was built

```
Shell (app.ts/html)          toolbar: Snapshots · Catalog
world/snapshots-page         list, newest first · "Import a save" upload · polls every 2 s while an analysis is pending
world/snapshot-page          header with analysis status · tabs: Findings · Factories · What it holds
world/findings               shared by snapshot and factory: summary cards · tabs for machines (filter + paging),
                             bottlenecks, unused supply, unknown supply
world/define-factory-dialog  output belt (the candidates of BR-W12, described in words) · name · input items
world/factory-page           output now and at 100% · the factory's findings
catalog/catalog-page         Docs and node-table imports with their counts · search · items, recipes, belts and pipes,
                             extractors, resource nodes
shared/display               class names as words · centimetres as metres · %, /min
shared/catalog-names         item and recipe names from the catalog, loaded once
shared/problem               the API's problem details as a message · whenAnalysed (waits out 202)
AppHost                      AddJavaScriptApp("web", "../../web", "start") with WithReference(api)
web/proxy.conf.js            /api → services__api__https__0 (or https://localhost:7044 without Aspire)
```

## How it works

- **The client.** `npm start`, `build` and `test` first run `generate:api` (ng-openapi-gen). It writes one function per operation (`getSnapshots`, `analyseFactory`, …) and an `Api` service. A page calls `api.invoke(getSnapshot, { id })`. The generated folder is git-ignored.
- **Data on a page** is a `resource`: a signal with `value()`, `error()` and `reload()`, and an async `loader`.
  - The snapshot page's analysis resource gets `params` only once the snapshot isn't pending any more, so it doesn't load before then.
  - A failed analysis answers 500 with its reason, and the page shows that reason.
- **Polling.** An `effect` sets a two-second timer while a snapshot is pending and reloads it. The effect's `onCleanup` clears the timer when the page is left. The factory page uses `whenAnalysed` instead: it asks again while the API answers 202.
- **Names.** The analysis speaks in class names. `CatalogNames` loads items and recipes once and gives their names.
  - Buildings have no names in the API yet, so `Build_ConstructorMk1_C` becomes "Constructor Mk1" by rule.
  - Positions are centimetres in the game, shown in metres.
- **Under Aspire** the web app is resource `web`, at `http://localhost:4200`. `WithReference(api)` passes the API's address as `services__api__https__0`, and `proxy.conf.js` reads it.

## What it took (the surprises)

1. **ng-openapi-gen 1.x generates functions, not services.** The 0.x style (`WorldService.getSnapshots()`) is gone. You get `fn/world/get-snapshots.ts` and call it via `Api.invoke`. The generator's own message still says "2 services": those are the `Api` helper and the configuration.
2. **Table rows are `any` in the template.** `*matCellDef="let m"` doesn't know the row type. With strict templates, `limits[m.limit]` doesn't compile (TS7053). The fix is a typed method (`limitLabel(machine: MachineFinding)`).
3. **`fixture.whenStable()` waits on pending HTTP.** The first findings test hung for 5 s, because `CatalogNames` had a request open. The tests stub `CatalogNames`, so they're about the findings, not the catalog.
4. **`Aspire.Hosting.NodeJs` ends at 9.5.2.** For Aspire 13 it's `Aspire.Hosting.JavaScript`. `AddJavaScriptApp` adds no endpoint of its own, so the AppHost declares `WithHttpEndpoint(port: 4200, isProxied: false)`.

## Tests (one behaviour each)

| Where | Tests |
|---|---|
| `shared/display.spec.ts` | A building is named by its class name without prefix and suffix · an item without a catalog name likewise · a recipe stays readable · a position shows in metres · no position is a dash · utilisation as a whole percentage · a rate per minute with at most two decimals |
| `shared/problem.spec.ts` | Asks again while the analysis is pending, then gives the findings · stops asking when the page is left · tells the reason the API gave |
| `world/findings.spec.ts` | Says why a starved machine runs below 100%, by the name of what it lacks · shows only the machines held back the way the filter says |
| `app.spec.ts` | Offers the snapshots and the catalog |

**Mutation check:** making the machine filter always true, and changing "Not enough", each fail their test.

## .NET / Angular points worth noticing (all in the primer, Frontend)

| Here | Java/Spring | Note |
|---|---|---|
| `AddJavaScriptApp(...).WithReference(api)` | Docker Compose service with `depends_on` and an env var | Aspire runs `npm start` and passes the API's address. |
| `resource({ params, loader })` | — (closest: a `CompletableFuture` bound to the view) | Loading state, error and value as signals. |
| `input.required<string>()` + `withComponentInputBinding()` | `@PathVariable` | Route parameters as component inputs. |
| `api.invoke$Response(fn, params)` | `RestClient...retrieve().toEntity()` | The whole response, status included. |
| `proxy.conf.js` | Spring Cloud Gateway route / Vite proxy | Same origin in dev, so no CORS. |

## Choices worth a second look

1. **Port 4200 fixed, not proxied by Aspire.** Simple, and `ng serve`'s default. Two AppHosts at once would clash.
2. **Machines are paged (25 per page) and filtered, not sorted.** The API already orders them worst first. Sorting by column can come next.
3. **Building names by rule** ("Constructor Mk1"), because the API doesn't expose building names. A `GET /catalog/buildings` would make them exact.
4. **Polling every 2 s** while pending, rather than server push (SignalR or SSE). It's a learning showcase worth doing later.
5. **No world map yet:** that was your idea for the second draft. The positions are all there (metres in every table).
6. **`npm audit`** reported vulnerabilities after `npm install`. I haven't looked into them yet.

## After your first look: machines at "100%" that were still held back

You saw "Wire can't leave" on a constructor showing 100% (at 3171, −1914, 98 m). Two causes:

1. **The UI rounded up.** That constructor runs at 99.84%, and `percent()` rounded it to "100%". A machine below 100% now shows one decimal, rounded down ("99.8%"). Only full speed shows "100%".
2. **The solver called every shortfall a finding**, down to 10⁻⁹. In your save, 280 held-back machines read 1.0000 at the API's 4 decimals.
   - **You decided:** from 99.99% a machine counts as running (BR-A11, `SteadyState.RunningFrom`).
   - `AnalysisDocument.CurrentVersion` is now 2, so stored analyses are computed again at the next start (ADR-0017).
   - The 99.84% wire line is still output blocked: 0.16% is more than noise.
   - **Edge:** a machine at 99.986% is held back but reads 0.9999 at 4 decimals. The UI shows it as 99.9%.

Tests: `SteadyStateTests`, a machine held back by less than a hundredth of a percent is running · one held back by a fiftieth of a percent is still output blocked. `display.spec.ts`, a machine below 100% never shows as 100% · full speed shows as 100%.

## Then: the API died at startup (a bug from step 19)

After the restart the API stopped within seconds (exit code −532462766, `0xE0434352`: an unhandled .NET exception). The page kept loading, and the catalog looked empty.
- **The cause:** `AnalysisRequeue` is a `BackgroundService`, and those run while the host is still starting. Until now it never had anything to republish. The version bump gave it both snapshots, its `PublishAsync` came before Wolverine was running, and it threw `WolverineHasNotStartedException`. By default an exception in a background service stops the host.
- **Why it hung instead of failing:** Aspire's proxy kept port 7044 open with no API behind it.
- **The data was never at risk:** the database still held both snapshots and the catalog.
- **Why the tests missed it:** they checked the requeue's *query*, never a start with snapshots to republish.
- **The fix:** the requeue waits for `ApplicationStarted`, and logs a failure instead of throwing.
- **The test:** an API that starts with snapshots to analyse keeps running and has them analysed. It starts a second API host on the same database and broker. It failed with exactly this exception before the fix.

All 554 .NET tests and 15 web tests are green.

The catalog page now says when it can't reach the API, like the snapshots page, instead of showing empty tables.

## Then: a "Running" filter that could never match, and two more decisions

The machine findings listed only machines below 100%, yet the UI offered a "Running" filter. Your decisions:
- **The findings list every machine**, the running ones last (worst first). BR-A11 is amended, and the factory findings follow, because they're filtered from the world's.
- **A machine at 99.9% or more counts as running** (`SteadyState.RunningFrom` = 0.999), down from 99.99%. In your save, 190 smelter-row machines between 99.9% and 99.99% were flagged. That's about what the solver leaves when a part settles slowly.
- `AnalysisDocument.CurrentVersion` is 3, so stored analyses are computed again at the next start.

The UI's "Machines" tab and its "Running" filter now show what they say.

**Tests:**
- **Domain:** less than a tenth of a percent short is running · a fifth of a percent short is still output blocked.
- **World:** a machine running fully is listed without a cause · every machine is listed, the one running worst first · a factory lists only its own machines.
- **API:** the findings list every machine, the running ones last.
- **Web:** the running machines on their own, at 100% and without a cause.

Three World tests encoded the old rule ("a running machine is not a finding") and were rewritten.

## Next

Your review of the screens (`dotnet run --project src/SatisfactoryLedger.AppHost`, then `web` in the dashboard). After that, the world map.
