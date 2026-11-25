# Step 100 · The optimal arrangement on touch screens

**Goal:** the owner found the arrangement page didn't look good on touch screens yet (2026-11-25). Branch `feat/arrangement-touch`, from main.

## What was wrong, from screenshots on a phone, an iPad and a phone held sideways

- **The map was buried:** on a phone it started about 2½ screens down, below ten lines of explanation, seven fields and the table of runs.
- **It opened on the whole world,** the area to arrange a small box in a corner.
- **The explanation pointed to a Settings tab that didn't exist,** and the field labels were cut off ("Plot east-west (foundatio…").

## The owner's choices

- **All four on every screen, desktop too:**
  - the map first, filling the screen;
  - the explanation and the settings in tabs;
  - opening on the area;
  - short labels.
- **The runs as a list, like the snapshot picker, each with a delete button.** Step 85 had moved the settings back out of a tab: there, beside the table of runs, they read as unclear. With the runs in their own list, the tab works.

## What was built

- **`RunPicker`** (`world/arrangement/run-picker.*`):
  - The button names the run shown: when it was asked for, and how it ended. While a new area is drawn it shows how many earlier runs there are, and with none, "No runs yet".
  - Its list has every run, newest first, each with a one-line summary and a delete button.
  - The table of runs is gone.
- **`runStatus` and `runSummary`** (`arrangement-model.ts`):
  - `runStatus` is how a run ended; it moved out of the page.
  - `runSummary` sums a run up in one line, e.g. "6 × 6 · roads 4 · 4 · supply lines · −34% · 3 not routed · Optimal".
- **The Settings tab, first, and the Info tab,** declared in the page's template as on the world map (`appSidebarTab`):
  - Settings has two fields a row: Plot E–W, Plot N–S, Road E–W, Road N–S and Belt floor, with "Sizes in foundations, of 8 m each" said once above them.
  - **Arrange** is now a click, not the form's submit, so it works with the tab closed.
- **The map fills the page:**
  - The page is a column as high as the shell leaves it (`100dvh − 112px`, on a phone `− 127px`), and the map takes what the header and the toolbar don't.
  - A `ResizeObserver` keeps Leaflet's size right when a tab opens beside it, as on the world map.
- **Opens on the area** (`fitToArea`): the one handed over by the world map, or the shown run's once the runs arrive. Only once: an area drawn on the page afterwards is where the player already looks.
- **The shell on a phone** keeps room for the snapshot picker only on pages that have one (`main.picks-snapshot`): 116 px at the bottom there, 59 px (the bar of tabs) elsewhere.

### Choices to overrule

- The runs list sits in the toolbar above the map, beside Arrange and Reset.
- Settings is the first tab, open by default with a mouse; Info second.
- A phone held sideways (360 px high) still scrolls a little: the header and the toolbar take half of it.

## After the owner tried it (2026-11-25)

- **Select mode ends when Arrange is pressed:** the run came back with the map still selecting.
- **A Factories tab, back:**
  - Step 87 had removed it, as clicking factories on the map did the job. But with no names on the map, what lit up couldn't be told.
  - It lists the factories in the area. Before a run, those whose machines' middle lies inside the area drawn (`factoriesInArea`); with a run, the run's (`arrangedFactories`).
  - A click picks one alone and lets go on a second, as a click on the map does (`toggleFocus`), and the picked one stands out.
- **Names on the map, for the highlighted factories only:** the one picked and those lit up with it (`highlighted`).
  - First every factory in the area was named, which piled up on a phone; the owner then asked for the highlighted ones only.
  - Each name sits at the factory's middle: on its proposed plot with a run, where it stands before one. A tap on a name picks the factory.
  - A name that would lie over one already shown is hidden until a closer zoom leaves room; the picked one's name comes first, so it always shows.
- **What a run's belts and pipes carry, on the map** (asked after): with a factory picked, the routed belts into or out of it that are 30 m long or more show their item's icon in a pill halfway along (`beltsToLabel`, `halfway`). These are the same pills and the same length as the lines before a run; the pill drawing is one helper for both (`itemPill`).

## Tests

- **Unit:**
  - `runStatus` and `runSummary` (4 tests);
  - `RunPicker` (6 tests);
  - the page: the settings are in the first tab and Arrange is above the map; the explanation is in an Info tab; the runs are in the picker, with no table; Arrange works with the tab closed. These replace step 85's "settings on the page".
- **Browser:**
  - The explanation is read from the Info tab.
  - The page opens with its map in view.
  - The sweep now holds the arrangement page to a phone's screen, as the world map: it fits without scrolling.
  - axe found the run picker's name ("Run: none") didn't contain what it shows ("No runs yet"). Both now come from one label.
- **After the owner tried it:**
  - `factoriesInArea`, `arrangedFactories` (3 tests); `beltsToLabel` and `halfway` (3 tests);
  - the page: select mode ends on Arrange; the Factories tab lists the area's factories, or says to draw an area first, and picks one and lets go; the map names only the picked factory and those lit up with it (5 tests).
  - Step 87's test "has no Factories tab" went.
- **Results:**
  - 421 unit tests pass; so do lint, the format check, the build and `tsc`.
  - Playwright: 273 runs, 225 pass and 48 are skipped as meant, twice in a row.
