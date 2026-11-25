# Step 97 · Journeys against the real stack

**Goal:** the third of the Playwright follow-ups (step 95's choice "both"): check that the web app, the API, the analysis service, PostgreSQL and RabbitMQ fit together, with nothing recorded.

## The owner's choices (2026-11-25)

- **The journey:** import `Development.sav` and see its map; define a factory in 3D and save it; its page shows what it makes. **Then delete the factory, as the last step,** so the journey can run again.
- **After a merge to main,** not on every PR: it doesn't hold up a PR's checks.
- **The desktop only:** defining a factory on a touch screen is already covered by the layout tests (`define.spec.ts`), on every device.

ADR-0008 amended; `docs/guide/07-quality-and-delivery.md` has the new job.

## What was built

- **`web/e2e/stack/journey.spec.ts`:** one test in four `test.step`s:
  1. import the save with the picker's button and a real file chooser: the map opens, and the API answered its map with 200;
  2. in the 3D view of the test world's area, *Define a factory*, shift-drag over the machines, name it *Journey Plates*, *Save*: the POST answers 201;
  3. its link in the Factories tab opens its page, which waits for the background analysis: the Output table lists Iron Plate;
  4. *Delete*, confirmed: the map opens, and the API answers 404 for the factory.
- **Leaves the database as it found it:**
  - the factory is deleted by the journey itself, as its last step; the snapshot it imported is deleted after it, through the API;
  - when a run fails halfway, `afterEach` deletes what it made; `beforeAll` also deletes a *Journey Plates* an aborted run left, since its machines would be taken (BR-W5);
  - a new database (CI's) gets the Catalog first, from the Docs file the Catalog tests use; one that has a Catalog keeps it.
- **`web/playwright.stack.config.ts`,** `npm run e2e:stack`: desktop Chrome, one worker, 3 minutes a test, `ng serve` on port 4301. The layout config ignores `e2e/stack/`.
- **The recorder deletes the factory it made** after recording: otherwise *Iron Plates* holds the smelters the journey chooses. One that was already there is kept.
- **`area`** (the test world's area query) is shared from `e2e/test.ts`.
- **CI job `journeys`**, on a push to main or by hand:
  - PostgreSQL 17 and RabbitMQ 4 as service containers;
  - the API (`--launch-profile http`: port 5219, Development, so it migrates) and the analysis service run with `dotnet run`, in the background, wired by the same connection strings (`ConnectionStrings__ledger`, `ConnectionStrings__rabbitmq`) and service address (`services__api__http__0`) as the AppHost gives them;
  - the dev server's proxy (`proxy.conf.js`) already reads that address;
  - the report and both services' logs are uploaded when it fails.

### Technical choices to overrule

- **Not the AppHost in CI:** it also starts Ollama, on a GPU the runner hasn't got. The cost: the job wires the services itself, and has to follow when the AppHost's wiring changes.
- **The 3D view opened by its address,** not by drawing on the map: drawing is covered by the layout tests, and a map drawn from a real snapshot has no fixed place for the machines.
- **The snapshot is deleted through the API,** not with the picker: the picker names snapshots by session, and locally there are more *Development* snapshots.
- **No resource nodes in CI:** the asserts need none. The miners' rates are then unknown, so in the model the plates' rate "now" may differ; the journey checks only that Iron Plate leaves.

## TypeScript / testing points

- **`test.step`** names the parts of one test in the report, like JUnit 5's `@Nested` tests run in order, without separate tests that share state.
- **`page.waitForEvent('filechooser')`** catches the dialog the browser would open, and `setFiles` answers it: the hidden `<input type="file">` is used as a person uses it.
- **GitHub Actions `services:`** start containers next to the job, reachable on `localhost` through their mapped ports, much like Testcontainers but declared in the workflow. Their `--health-cmd` makes the job wait until they're ready.
- **ASP.NET Core configuration from the environment:** `ConnectionStrings__ledger` is `ConnectionStrings:ledger`, the double underscore standing for the colon, as Spring's relaxed binding reads `SPRING_DATASOURCE_URL`.

## Tests

- **Not run yet against the real stack:** the AppHost was stopped by Claude Code for low memory and wasn't restarted unasked. Before running: delete the *Iron Plates* factory the earlier recording left in the Development world, or the journey's save is refused (409 `World.MachinesTaken`).
- The layout suite still lists 237 tests and leaves the journey out; `npx playwright test -c playwright.stack.config.ts --list` lists the journey alone. `tsc -p e2e/tsconfig.json`, lint and the format check pass.
