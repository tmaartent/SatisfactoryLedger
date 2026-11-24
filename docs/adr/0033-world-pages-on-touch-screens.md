# ADR-0033: The World pages on phones and tablets

- **Status:** Accepted
- **Date:** 2026-11-24

## Context
The World pages were made for a mouse and a wide screen:
- **Areas need keys:** holding shift and dragging opens an area in 3D, ctrl and dragging arranges it. A touch screen has neither key, and a one-finger drag pans the map.
- **What a building is shows on hover only.** A tooltip opens when the mouse is over a building, belt or node. A touch screen has no hover, and a tap already highlights a factory.
- **The sidebar is too wide for a phone:** its open tab is 340 pixels, and the rail beside it 48. On a phone (about 360 to 430 pixels wide) an open tab covers the whole map.
- **The page is cut off at the bottom:** heights were set in `vh`, which on a phone's browser includes the space behind the address bar.

## Decision
*(Decided by the owner 2026-11-24.)*
- **A phone** is a screen narrower than 600 pixels, where Angular Material's toolbar turns mobile. **A touch screen** is one where nothing hovers (`@media (hover: none)`). The two are apart: a tablet is wide but has no keys, and a narrow desktop window has them.
- **On a phone, the sidebar is a bar of tabs along the bottom** *(asked: bottom sheet)*. Its open tab is a sheet above that bar, over the map, at half the screen's height or all of it:
  - a swipe up on its handle raises it to full height, but never over the toolbar;
  - a swipe down lowers it to half height, or from there closes it;
  - a tap on the handle switches between half and full height;
  - what the shell puts above the tabs (the snapshot picker) stays above the bar with no tab open *(amended 2026-11-24, asked: it was hidden then, but the snapshot is worth seeing always)*; the map ends above the two.

  Tablets keep the sidebar on the right, its open tab over the map below 960 pixels (ADR-0031).
- **On a phone, the legend is folded behind a button** in the map's corner, as it covers much of the map; elsewhere it's always open.
- **On a touch screen, the hint about shift and ctrl doesn't show.**
- **A 3D view on a phone opens with the tab closed** *(asked 2026-11-24)*: a sheet over it would hide half of it. A click on something in the view opens the Info tab, as everywhere.
- **The 3D view's hint, on a touch screen:** "Drag to look, pinch to zoom, two-finger drag to move. Click any object to inspect it." *(asked 2026-11-24)*. With a mouse it keeps its words for the wheel and the right button.
- **Heights in `dvh`**, the height the browser shows, not `vh`.
- **Picking an area on a touch screen: a select button on the map** *(asked; not a long press, and not desktop only)*. It turns on a select mode, where a one-finger drag draws the area instead of panning. On letting go, **3D** and **Arrange** say where it opens. *(Step 91.)* The optimal arrangement has the same button *(2026-11-24)*: there **Area** makes the rectangle the area to arrange, and **Keep out**, once there is an area, a part where no factory may stand; selecting goes on after a choice, so the next part can be drawn at once. The button shows only on a touch screen; with a mouse, shift and ctrl stay as they are.
- **Defining a factory in 3D on a touch screen: the same select button** *(asked 2026-11-24, step 93)*. While defining, it turns on boxing: the view stops turning, and a one-finger drag draws a box. On letting go, **Add** and **Take out** say what happens to every machine inside it; boxing goes on after a choice. A tap still adds or takes out the one machine under it.
- **A tap on a building highlights its factory, as now, and shows what it is** *(asked)*, as its tooltip says on hover; a tap elsewhere clears both. *(Step 92.)*
  - Leaflet already opens a tooltip on a click, which a tap is: a building of no factory, a belt or a node said what it is at a tap before step 92. A factory's building didn't: a click on it opens the factory's page at once, so on a touch screen its label was never seen.
  - **On a touch screen, the first tap on a factory's building says what it is and highlights its factory; a second tap on the same building opens the factory's page** *(asked 2026-11-24; rather than leaving it, or a link in the label)*. Its label says "tap again to open", and a tap on the label opens the page too *(asked 2026-11-24: the words invite one)*. With a mouse, a click still opens the page at once: the pointer showed the label on its way.

## Alternatives considered
- **Keep the sidebar on the right on a phone, only narrower:** the least work, but the map is hidden while a tab is open.
- **A full-screen tab on a phone:** simpler than a sheet, but the map and a tab can't be seen together.
- **A long press, then a drag, to pick an area:** no button, but hard to find.
- **No areas on touch screens:** phones and tablets would only look.

## Consequences
- The layout's breakpoints are in CSS. Tests in jsdom can't apply them: what they check is the behaviour (the sheet's swipes, the legend's button). The layout was looked at in headless Chrome at a phone's and a tablet's size, with touch emulated; it needs a look on a real phone.
