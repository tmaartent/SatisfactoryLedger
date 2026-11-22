# Step 70 · Define factories in one go

**Goal:** saving a factory in the 3D view opened its page, so defining the next one meant drawing the area again. Now the 3D view stays and shows the Factories tab, so a big area can be split into several factories in one go (asked 2026-11-23). Branch `feat/map-info-tab`, uncommitted, with steps 68 and 69.

## How it's built

- **`AreaPage.save()`** no longer navigates. It stops defining, reloads the factories (the new one's machines get its colour) and, in an area's own 3D view, shows the Factories tab (`sidebar.show('factories')`).
- **On a factory's page** the view is embedded and has no Factories tab: nothing changes there. The page still reloads its findings on `saved`.
- The router isn't used by `AreaPage` any more, so it's gone from it.

Written into ADR-0031.

### Technical choice to overrule

- **The new factory isn't highlighted in the list:** it's there among the others, edged in its colour in 3D. Its page is one click away (*open*).

## Tests

None new: `AreaPage` starts WebGL, which jsdom doesn't have, so it has no spec, as for its other clicks (step 63). 267 web tests pass, exit code 0; the build is clean. Not tried in a browser.
