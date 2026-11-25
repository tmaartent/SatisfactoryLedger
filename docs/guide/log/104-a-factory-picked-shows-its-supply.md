# Step 104 · A factory picked shows its supply, back to the source

**Goal:** the owner found the highlighting on the arrangement map wrong, from two screenshots of the Desert world's run with supply lines (2026-11-25):

- Modular Engines takes rubber from a freight platform, but the rubber belt lit up didn't point to a station.
- Superposition Oscillators lit up a Crystal Oscillator belt to a factory not involved, and a Dark Matter Crystal belt to Ballistic Warp Drives, though Dark Matter Crystals are an input.

Branch `feat/arrangement-touch`, after step 103.

## What was wrong

The routing follows the rules; the highlighting didn't fit them. With supply lines a belt runs from one factory on the line to the next, with a splitter under each (BR-S3, decided 2026-11-19). The rubber line in run `01a212f4…`:

```
freight platform → Heat Sinks → Turbo Motors → Cooling Systems → Modular Engines → Radio Control Systems → Crystal Oscillators
```

The page lit the belts with the picked factory at either end:

- **Into it:** the leg into Modular Engines starts under Cooling Systems, so the platform stayed dark.
- **Out of it:** the Modular Engines → Radio Control Systems leg is the rubber running on past it, not something it makes. The same goes for Superposition Oscillators → Ballistic Warp Drives (Dark Matter Crystals) and → Radio Control Systems (Crystal Oscillators).

## The owner's choices

- **A factory picked shows its supply, back to the source,** and what it makes. The legs running on past it stay dimmed.
- **The routing stays as it is.**

## What was built

- **`suppliedBelts(belts, focused)`** (`arrangement-model.ts`), the one rule for what stands out:
  - With nothing picked, every belt.
  - **Into a factory picked:** the belt, and its way back. A belt carrying an item from a factory that item comes into runs on from there. Its way back goes on through every belt bringing the item into that factory: the line's leg and its top-ups (`injection`). An overflow's way back goes only through the top-ups, whose smart splitter it leaves.
  - **Out of a factory picked:** only a belt carrying what the factory makes, that is, an item that doesn't come into it.
  - A belt from before loads were kept (`item` null) isn't traced back.
- **Used everywhere a factory picked lights belts up:**
  - on the map, the line's weight and dimming (`drawBelts`);
  - the item labels (`beltsToLabel`);
  - the 3D view's fading. Its belts, mergers and splitters, spot drums and crossing balls carry their belt (`userData['routed']`) instead of their two ends.
- **Without supply lines nothing changes:** every belt runs from its source to its consumer, so the way back is that belt alone.

### On the owner's run, checked against the data

- **Modular Engines** lights up:
  - its rubber back to the freight platform, through Heat Sinks, Turbo Motors and Cooling Systems;
  - its motors back to the Motors factory, through Turbo Motors;
  - its Smart Plating, and the Modular Engines to Thermal Propulsion Rockets.
- **Superposition Oscillators** lights up:
  - its Dark Matter Crystals;
  - its Crystal Oscillators, back through Computers to the Crystal Oscillators factory;
  - its aluminium sheet from a drone port;
  - its outputs: Superposition Oscillators to AI Expansion Servers, and Dark Matter Residue, its byproduct, to Dark Matter Crystals.

  The belts to Ballistic Warp Drives and Radio Control Systems stay dimmed.

### Choices to overrule

- **The factories a line passes stay dimmed,** though their legs light up. The factories that stand out are still those picked and those feeding them by a link (`relatedFactories`). Heat Sinks, Turbo Motors and Cooling Systems are passed by the rubber line, not suppliers of Modular Engines.
- **A factory that takes and makes the same item** would have its output taken for the line running on. No recipe in the game does that, I believe, but a loop through a factory could.

## TypeScript points

- **A `Set` of objects is by reference,** as a Java `HashSet` of objects without `equals`: `suppliedBelts` returns the very belt objects it was given, and `shown.has(belt)` finds them. A copy (`{ ...belt }`) wouldn't be found. The 3D view keeps the belt object itself on its meshes for that reason.
- **A recursive closure with a guard:** `back` adds a belt before walking on, so a loop of belts can't recurse for ever. It's the same as a visited set in a Java depth-first search.

## Tests

- **`arrangement-model.spec.ts`**, 7 new on the owner's rubber line. The 4 that say what must *not* light up failed against a stub lighting everything:
  - not the line running on;
  - what it makes, and only that (6 belts);
  - an overflow traced to its top-up only;
  - an old belt not traced back.

  The 3 inclusion tests (back to the source, top-ups, none picked) passed against that stub too, so they guard rather than drive.
- **Changed:** the label test's belt into the plates factory now carries iron ore. As iron plates, the belt out would read as plates running on past it.
- **Results:**
  - 436 web tests pass, exit code 0; lint, the format check, the build and the e2e type check pass;
  - the Playwright arrangement tests pass (19 passed, 5 skipped).
- **Not tried in a browser:** the AppHost stopped again.
