// Selecting machines for a factory in the 3D view (BR-W2), apart from three.js: what a click or a dragged box does.

/** A point on screen, in pixels from the canvas's top left. */
export interface ScreenPoint {
  x: number;
  y: number;
}

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
  return { left: Math.min(a.x, b.x), top: Math.min(a.y, b.y), right: Math.max(a.x, b.x), bottom: Math.max(a.y, b.y) };
}

/** A box adds (shift) or takes out (alt) every machine whose middle lies inside it on screen, edges included. */
export function boxSelect(
  selection: ReadonlySet<string>,
  onScreen: readonly (ScreenPoint & { id: string })[],
  box: ScreenRect,
  mode: 'add' | 'remove',
): Set<string> {
  const next = new Set(selection);
  for (const machine of onScreen) {
    if (machine.x < box.left || machine.x > box.right || machine.y < box.top || machine.y > box.bottom) continue;
    if (mode === 'add') next.add(machine.id);
    else next.delete(machine.id);
  }
  return next;
}
