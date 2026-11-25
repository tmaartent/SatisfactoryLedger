# Step 110 · Arrows on a splitter's or merger's ways in and out

**Goal:** in 3D, a splitter or merger clicked shows where items come in and go out (asked 2026-11-26). Branch `feat/splitter-arrows`, from `main` after PR #41.

## What you decided (2026-11-26)

- **When it's clicked or tapped:** its arrows show, with its label, until you click or tap elsewhere, or it again.
- **In every 3D view:** the arrangement's belt floor, and the area's 3D view, which the factory page shows too.
- **Vertical splitters and mergers:** the game data has none. Its five attachments, *Conveyor Splitter*, *Smart Splitter*, *Programmable Splitter*, *Conveyor Merger* and *Priority Merger*, all sit on a belt; the only vertical pieces are the *Conveyor Lifts*. Nothing to plan for.

### Choices to overrule

- **A way in cyan, a way out amber**, 3 m long, lying flat on the belt just outside the splitter or merger, drawn over everything so a belt in front doesn't hide it.
- **In the area's view, a way in or out by a lift or straight into a machine has no arrow:** there's no belt end at the port to read it from.

## How it works

- **The arrangement** (`jointArrows`, `arrangement-model.ts`): a priority merger is where a top-up belt ends on a line, a splitter or smart splitter where a belt starts on one. The line is the other belt of the same item through that point; at a corner it comes in one way and goes on the other. Arrows half a foundation from the joint: a merger takes in the top-up and the line and sends the line on; a splitter takes in the line and sends it on and the branch off. None up in a factory.
- **The area, as built** (`attachmentArrows`, `area-model.ts`): the API gives no connections with the belts, but the save keeps a belt's curve the way it runs. A belt whose curve ends at the splitter brings items in, one that starts there takes them out, its arrow pointing along the curve's tangent there.
- **How near is "at"**, measured on the Desert world's 5,013 splitters and mergers:

  | Reach | Splitters or mergers that look wrong |
  |---|---|
  | 2.5 m across, 2 m high | several hundred: neighbours' belts, and splitters stacked on poles 2 m apart |
  | 1.5 m across, 2 m high | 281 |
  | **1.5 m across, 0.5 m high** | **5**, and 13 with no belt (a lift or a machine straight on it) |

  A belt's end lies 75 to 125 cm from its own splitter's middle and at its height, 12,167 of them exactly.
- **Drawing** (`flow-arrow.ts`): one arrow, shaft and head, shared by both views. `BeltFloor3d.showArrows` on a click, `AreaPage.drawAttachmentArrows` when a splitter or merger is the building looked at.

## TypeScript and three.js points

| Here | Elsewhere |
|---|---|
| **`depthTest: false` and `renderOrder`**: the arrows are drawn last and over everything, so they're never hidden inside the splitter's box or behind a belt. | In Java 3D or a game engine: an overlay pass. |
| **`window.ng.getComponent(element)`**, in a development build: the component behind an element, its signals and fields, from the browser's console or a script. Used here to pick a splitter and move the camera before a screenshot. | Spring Boot DevTools' actuator, roughly: a look inside the running app, for development only. |

## Tests

Written first, failing on compile (no `jointArrows`, no `attachmentArrows`):
- **Arrangement:** a priority merger takes in the line and the top-up and sends the line on; a splitter takes in the line and sends it on and the branch off; where the line turns at the joint it comes in one way and goes on the other; none up in a factory.
- **Area:** a belt ending at it comes in, its arrow on the belt just outside it pointing the way the belt runs; one starting at it goes out; a belt only passing near, or on the floor above, isn't one of its.

456 web tests pass; lint, format, build; Playwright on the arrangement and defining a factory (73) pass. Looked at in the running app on the Desert world: a splitter in the area's view, a priority merger on the arrangement's floor, clicked.
