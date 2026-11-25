# Step 101 · Placing again when belts are left over

**Goal:** the owner asked whether a run whose belts can't all be routed without crossing could just be retried (2026-11-25). Same working tree as step 100 (`feat/arrangement-touch`), committed apart.

## Why a plain retry wouldn't help

- **Routing one layout always comes out the same:** `BeltRouter` already tries up to 8 times, laying the belts that failed first.
- **Placing isn't the same each time:** CP-SAT's 8 workers race, so another placement can route where the first didn't.

The owner chose option 2 of four: **the service places again by itself** and keeps the best.

## What was built

- **`ArrangementSolver.Solve(problem, timeLimit, avoid, seed)`:**
  - The layouts in `avoid` aren't found again: in each one, at least one factory's corner lies a square or more left, right, below or above where it stood. Each such move is a boolean the model may choose (`OnlyEnforceIf`), and at least one must hold (`AddBoolOr`).
  - The seed goes to CP-SAT (`random_seed`), so each search goes another way.
- **`ArrangementDocuments.Solve`:**
  - It places, then routes.
  - When belts are left over, it places again, avoiding the layouts tried, up to **3 layouts**. It keeps the one with the fewest unrouted belts and pipes, then the least belt work, and stops as soon as everything routes.
  - The routing moved into a function of its own (`Route`).
- **`ArrangementProposalDocument.Placements`:** how many layouts were tried. It defaults to 1, so stored runs from before read as one.
- **The API:** the run's summary passes it on as `placements` (`api/openapi.json`).
- **The page:** the run's summary line says "best of 3 placements" when more than one was tried.
- **Docs:** BR-O2 and ADR-0028 amended.

### Choices to overrule

- **At most 3 layouts.**
- **Each layout gets the run's full solver time,** so a run with leftovers takes up to three times as long (Thorough: up to 30 minutes).
- **"Best" means the fewest unrouted belts, then the least belt work.**

## On the owner's Desert world

The same request again (Fast, supply lines, the same area and keep-out part):

- **Before:** 1 layout, 3 belts unrouted.
- **Now:** 3 layouts, none of them routed everything; the best left **2** unrouted. 7 levels used, 88 s.

An improvement, not a cure: these leftovers come from crossings, not from a lack of levels (7 of 16 used).

## .NET points

- **CP-SAT's half-reification:** `model.Add(x <= a).OnlyEnforceIf(b)` holds the constraint only when `b` is true. Several such `b`s with `AddBoolOr` say "at least one of these". It's the usual way to express "differs from".
- **Optional record parameters as a format version:** `int Placements = 1` at the end of the record's parameters. `System.Text.Json` uses the parameter's default when the stored JSON lacks the field, so old proposals still read.

## Tests

- **Solver:** a placement asked to avoid an earlier one puts a factory elsewhere, at no lower cost.
- **Documents:** a layout whose belts all route is placed once. In a strip one square wide with one level, which can never take the link's two belts, it tries 3 layouts and keeps one belt unrouted.
- **API:** a run that routes everything says 1 placement.
- **Web:** the summary line names the placements when more than one.
- **Results:**
  - 1,237 .NET tests pass, with no build warnings;
  - 422 web tests pass; so do lint and the format check;
  - the Playwright recording still fits the changed API.
