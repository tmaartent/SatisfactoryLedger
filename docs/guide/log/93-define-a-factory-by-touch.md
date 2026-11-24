# Step 93 · Defining a factory by touch

**Goal:** define or edit a factory in 3D on a touch screen (asked 2026-11-24: "I like the current approach with the rectangle button, implement it there too"). With a mouse, a click adds or takes out one machine, and a box dragged with shift or alt adds or takes out every one inside it; a touch screen has no keys to hold. Branch `feat/define-by-touch`, from `main` after PR #28.

## What was built

- **While defining**, the 3D view has the select button in its top right corner, on a touch screen only, as on the maps (ADR-0033). It turns on *boxing*: the view stops turning, and a one-finger drag draws the blue box the shift-drag draws.
- **On letting go**, the view offers **Add**, **Take out** and **Cancel** for the machines inside the box. Boxing goes on after a choice, so the next box can be drawn at once; the button (a cross now) ends it, and so do Cancel or Save in the header.
- **A tap** while boxing draws no box: it adds or takes out the machine under it, as a tap always does while defining.
- **The words for touch:** the hint at the bottom of the view and the steps in the Info tab speak of tapping and of the select button, not of shift and alt. On a touch screen, the colour legend leaves room for the button.

## How

- `world/area/selection.ts`: `isTap(box)`: a box no wider, corner to corner, than a click may move (`ClickReach`, 4 px) is a tap. The click handler in `AreaPage` now uses `ClickReach` too, so the two agree.
- `world/area/area-page.ts`:
  - `boxing` and `drawnBox`, signals; `toggleBoxing()`, `useBox(mode)`, `letGoOfBox()`.
  - `listenForBoxes`, the shift and alt drag, now also draws while boxing, with a third mode, `offer`: on release the box stays, as `drawnBox`, instead of being applied. `useBox` applies it with the same `boxSelect` the keys use, against where the machines lie on screen now: the view can't turn while boxing, so that's where they lay under the box.
  - **No pointer capture while boxing.** The keyed drag captures the pointer on the stage; that would keep a tap's release from the canvas, whose click handler adds the machine. A touch already stays with the element it pressed, so boxing needs no capture.
  - A `const` copy of the mode before `chosen.update(...)`: TypeScript doesn't carry the narrowing of an outer `let` into a callback, as the variable could change before the callback runs.
- The button and the choices are the maps' (`button.area-select`, `.area-choices` in `styles.scss`). Inside the 3D stage the button gets `z-index: 1`: the maps lift it over Leaflet's panes with 1000, which here put it over the toolbar when the view was scrolled under it.

**The choices beside the box** (asked 2026-11-24, after trying it on a phone: drawing worked, but no choices came on letting go). In headless Chrome they came, at the bottom of the view, as on the maps. On a phone the 3D view is 65% of the screen's height and lies below the overview, so with the page scrolled to its upper part, its bottom, and the choices with it, lay off the screen or behind the bar of tabs. They now stand just below the box drawn, above it when there's no room below, or at the top of the view: `choicesTop(box, height)` in `selection.ts`, an inline `top`, and in `area-page.scss` a rule that undoes the maps' bottom placement for the stage. Seen on the owner's phone: the choices came.

**In sight, not just in the view** (asked 2026-11-24: a box drawn near the bottom of the screen had its choices below it, off the screen). `choicesTop(box, seen)` now takes the part of the view in sight, `SeenRange`: `AreaPage.seenRange` cuts the stage's box to the window, to below the toolbar, which the page scrolls under, and clear of the sidebar where it lies over the stage, by `coveredSides` from the map (`world/map/visible-part.ts`), as the bar of tabs does on a phone. Below the box if they fit there, else above it, else on the side with more room, 8 px inside what's in sight. Checked with real touch events: the view scrolled to run off the screen, a box drawn to just above the bar of tabs, the choices stood above it, in sight.

**Add did nothing** (asked 2026-11-24, on the phone). The choices lie over the canvas, inside the stage, and the stage listens for every press while boxing, to start a box: a finger pressing *Add* reached it first, and it let go of the box drawn before the button's click came to use it. A box now starts only from a press on the canvas itself (`event.target`). My first check clicked the button from script, which fires no press, so it missed this; checked again with real touch events on the buttons: Take out took 18 of 20 machines out, a new box and Add put them back.

**The Info tab while defining** (asked 2026-11-24, after trying it on a phone: the tab hid the 3D view, said too much, and what the machines chosen make and take stood below all of it):
- **Opened later on a phone.** Starting to define or edit opens the Info tab only where it stands beside the view (`showStepsBeside`: `show('info')`, then `closeOnPhone()`). On a phone it opens after a box's *Add* or *Take out* (`useBox`), as it already did after a tap on a machine.
- **The how-to steps only with a mouse:** on a touch screen the hint in the view says how; the list is hidden by `@media (hover: none)`, inside the `.how` rule. Outside it, earlier in the stylesheet, it lost to `.how`'s own `display: grid` at the same specificity.
- **What they make and take first:** the count of machines chosen and where items leave and come in now stand right under the tab's title; the machine tapped follows, then the steps.

### Technical choices to overrule

- **Boxing stays on after a choice**, as on the arrangement page.
- **Icons**: `add` and `remove` for Add and Take out.

## Tests

- `world/area/selection.spec.ts`: a finger let go of after moving no more than a click may is a tap; further, either way, a box. The choices stand below the box, above it without room below in what's in sight (the view scrolled off the screen, or under the toolbar), and on the side with more room when neither fits.
- `AreaPage` has no spec: three.js can't start in jsdom, which has no WebGL, and a do-nothing context, as the map's spec gives Leaflet, isn't enough (three.js turns parts of it into strings and fails). The wiring was tried in headless Chrome instead.
- 378 web tests pass (exit code 0); lint, the format check and the build pass.
- **With real touch events** over the DevTools protocol, at 390 × 844 and a pixel ratio of 2, on Alclad Aluminium Sheets: Edit; the select button; a drag over the machines drew the box and offered Add, Take out and Cancel, and the camera didn't move; Take out took 18 of the 20 machines out, and boxing went on.
