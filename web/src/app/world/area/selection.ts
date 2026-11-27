// Selecting machines for a factory in the 3D view (BR-W2), apart from three.js: what a click or a dragged box does.

import { PressReach, ScreenPoint } from '../stage/stage';

/** A rectangle on screen, in pixels from the canvas's top left. */
export interface ScreenRect {
  left: number;
  top: number;
  right: number;
  bottom: number;
}

/** A click adds a machine that isn't selected, and takes out one that is. The selection given stays as it was. */
export function toggle(selection: ReadonlySet<string>, id: string): Set<string> {
  const next = new Set(selection);
  if (!next.delete(id)) next.add(id);
  return next;
}

/** The rectangle between where a drag started and where it is now, whichever way it went. */
export function rectBetween(a: ScreenPoint, b: ScreenPoint): ScreenRect {
  return {
    left: Math.min(a.x, b.x),
    top: Math.min(a.y, b.y),
    right: Math.max(a.x, b.x),
    bottom: Math.max(a.y, b.y),
  };
}

/**
 * Whether a box drawn by touch is a tap (asked 2026-11-24): its finger moved no more than a click may, so the click on
 * the machine under it adds or takes it out, and no box is offered.
 */
export function isTap(box: ScreenRect): boolean {
  return Math.hypot(box.right - box.left, box.bottom - box.top) <= PressReach;
}

/** How high the choices for a box drawn by touch stand, in pixels, and how far from the box. */
const ChoicesHeight = 56;
const ChoicesGap = 8;

/** The part of a view in sight, in pixels from the view's top: not off the screen, under the toolbar or the tabs. */
export interface SeenRange {
  top: number;
  bottom: number;
}

/**
 * Where the choices for a box drawn by touch stand, in pixels from the top of the view (asked 2026-11-24: they must be in
 * sight, where the finger let go): just below the box when they fit there, in the part of the view in sight; else just
 * above it; else on the side with more room, as near the box as what's in sight lets them.
 */
export function choicesTop(box: ScreenRect, seen: SeenRange): number {
  const below = box.bottom + ChoicesGap;
  const lowest = seen.bottom - ChoicesGap - ChoicesHeight;
  if (below <= lowest) return below;
  const above = box.top - ChoicesGap - ChoicesHeight;
  const highest = seen.top + ChoicesGap;
  if (above >= highest) return above;
  return seen.bottom - box.bottom > box.top - seen.top ? lowest : highest;
}

/** The machines whose middle lies inside a box on screen, edges included: a box adds them (shift) or takes them out (alt). */
export function inBox(
  onScreen: readonly (ScreenPoint & { id: string })[],
  box: ScreenRect,
): string[] {
  return onScreen
    .filter(
      (machine) =>
        machine.x >= box.left &&
        machine.x <= box.right &&
        machine.y >= box.top &&
        machine.y <= box.bottom,
    )
    .map((machine) => machine.id);
}
