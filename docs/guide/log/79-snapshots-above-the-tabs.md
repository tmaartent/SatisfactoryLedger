# Step 79 · The snapshots above the tabs

**Goal:** three changes to the World's sidebar (asked 2026-11-23):
- the map's Info tab says what the snapshot holds, under the cards, as the findings page's *What it holds* does;
- the snapshots leave the tabs: every other tab is about the snapshot shown, so picking it is apart from them, above the tabs (the owner chose a picker over the whole list, with tooltips on the import and for when there are none yet);
- the tabs' titles stand out more.

Branch `feat/node-filter`, uncommitted, after step 78 (PR #22 is open).

## How it's built

- **`WhatItHolds`** (`world/what-it-holds.ts`): the five counts, each said in words. The findings page's *What it holds* tab uses it instead of its own list, and the map's Info tab shows it under the overview, from the snapshot the map loads already. Named after the section: the API already has a `SnapshotCounts` type.
- **`SnapshotPicker`** (`world/snapshot-picker.*`) replaces `SnapshotsSidebar`, the map's Snapshots tab:
  - A button with the shown snapshot's name (*Pick a snapshot* without one) opens a **Material menu** (`mat-menu`) of every snapshot: its link to its map, when it was saved, and a delete button. The menu is an overlay outside the component, so its rows are styled in `styles.scss`.
  - Beside it, the import, with a tooltip on what it does and where the game keeps its saves.
  - Before any snapshot, the button is disabled, and the tooltip saying *No snapshots yet* and how to make one is on what holds it: a disabled button takes no pointer, so its own tooltip would never show.
  - **Narrow:** the picker is a CSS *container* (`container-type: inline-size`). When the space above the sidebar is as narrow as the rail (collapsed, or a narrow screen, where the open tab lies over the page), a container query hides the name and stacks the two icons. No code asks how wide the sidebar is.
- **The sidebar** (`sidebar.html`) takes what goes above its tabs as content (`<ng-content />` in `.above`), and lays it over the tab and the rail. The shell (`app.html`) puts the picker there on World pages, with the snapshot from the URL (`App.snapshotId`).
  - **`@defer (on idle)`:** the picker, with the menu, tooltip and dialog code it needs, first made the shell's initial bundle 636 kB, over its 500 kB budget. Deferred, it loads once the page is up, and the initial bundle is 467 kB again.
  - World in the toolbar no longer opens a Snapshots tab (`goToWorld` is gone): the picker is always there.
- **Tab titles** (`sidebar.scss`): the tab's icon beside its title, the title larger (`title-medium`, bold) in the primary colour, on a band of the surface-container colour with a line under it.

Written into ADR-0031.

### Technical choices to overrule

- **The picker shows the save date in the list** (*6 Nov, 08:07*), not in the button.
- **Deleting a snapshot is in the list,** beside each one, as it was in the tab.
- **The map's test helper `open`** now leaves an open tab open: without a snapshot, Resource nodes is the only tab, so it's open already, and a click on its icon collapsed it.

## Tests

New: `what-it-holds.spec.ts` (1), `snapshot-picker.spec.ts` (9, the Snapshots tab's tests carried over plus naming the snapshot shown, *Pick a snapshot*, the import's tooltip and the one for none yet), the sidebar's content above the tabs and its title band (2), the shell's picker (1), the map's Info tab saying what the snapshot holds (1). `snapshots-sidebar.spec.ts` is gone with its component; the map's and the shell's Snapshots-tab tests changed. 318 web tests pass, exit code 0; lint and the format check pass; the build is clean, initial bundle 467 kB. Not tried in a browser.
