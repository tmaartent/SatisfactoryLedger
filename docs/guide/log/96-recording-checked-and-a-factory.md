# Step 96 · The recording checked against the API, and a factory in the test world

**Goal:** the next Playwright work, in the order proposed after step 95 and chosen by the owner (2026-11-25): first close the risk that the recorded answers go stale, then give the test world a factory, so the tests reach the pages that need one. Journeys against the real stack come after, as their own step. Branch `test/recording-contract`, from main.

## 1. The recording checked against the API's description

The layout tests replay answers recorded once (step 95). If the API changed shape, they'd go on passing against old answers. `api/openapi.json` describes every answer, and CI already fails when it changes uncommitted. Now it also checks the recording:
- **`web/e2e/recording.spec.ts`:** for each recorded answer, the API's path for it (its template: `/snapshots/{id}/map` takes any id), the schema for its status, and the body validated against it with **ajv** (JSON Schema 2020-12, which OpenAPI 3.1 uses). OpenAPI's number formats (`double`, `int32`) say how a number is stored, which JSON doesn't tell: any number fits them.
- A Playwright project of its own, **`recording`**: run once, without a browser. The four device projects leave it out.
- A failure says which answer, which field, and what to do: *"no longer fits the API: record again with npm run e2e:record"*.
- `ajv` and `ajv-formats` are direct dev dependencies now; they were there already, pulled in by others.

**Does it catch drift?** Broken on purpose, then restored:
- the API grew a required field in the map's answer: *"must have required property 'rails'"*;
- the recording renamed one of the map's fields: *"must have required property 'conveyors'"*.

**What it doesn't catch:** a field the API dropped but the recording still has. The schemas allow extra fields, and a page ignores them, so that's the harmless direction.

## 2. A factory in the test world

- **`e2e/record.mjs`** makes the factory *Iron Plates* in the test world through the API, unless it's there: its two smelters and the constructor making plates, as the .NET tests' plates factory. It records the factory's page too, and keeps the factory's id in the recording beside the snapshot's.
- **`e2e/factory.spec.ts`**, a factory's building on the map (step 92): on a touch screen the first tap shows its label with *"tap again to open"*; a second tap, or a tap on the label, opens the factory; with a mouse a click opens it at once. The map is opened focused on a smelter (`?focus=…`) and zoomed in three times: at first a smelter is some 10 px wide, two stacked, and a tap at the middle landed on their seam.
- **The sweep** has the factory page now: no new findings on it.
- **Every factory preview** is answered by the shared API answers (`e2e/api.ts`): the factory page's 3D view asks for one too, not only defining a factory. `define.spec.ts` no longer answers it itself.
- `whole(page)` follows the app's own rule for the snapshot picker (`app.ts`, `picksSnapshot`): a factory's page has none, as the arrangement's hasn't. A regex for it behaved differently under Playwright than under Node, for a reason not pinned down; splitting the path does the same plainly.

## Seen, not fixed

- Leaving the map while it zooms logs an error from Leaflet (*"Cannot read properties of undefined (reading '_leaflet_pos')"*): a tap that opens a factory right after zooming does it. Nothing visible goes wrong.
- On a phone, a building's label at the map's left edge can stick out 1 px.

## TypeScript / testing points

- **OpenAPI 3.1 is JSON Schema:** the API description's schemas validate JSON directly with any JSON Schema validator; ajv's `addSchema` with an `$id` makes the `$ref`s resolve.
- **A project without a browser:** a Playwright test that names no `page` starts none: the recording check takes milliseconds.

## Tests

- 237 runs (13 recording checks, and 56 tests × 4 devices): 187 pass, 50 skipped as meant, twice in a row. Exit code 0.
- 394 web unit tests, lint, format check and build pass.
