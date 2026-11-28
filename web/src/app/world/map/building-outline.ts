import * as L from 'leaflet';

import { inAnyRing } from './ring-hit';

/** What a Leaflet line or polygon keeps once drawn, which its typings leave out. */
declare module 'leaflet' {
  interface Polyline {
    /** Its rings in screen pixels, and their bounds. */
    _pxBounds?: Bounds;
    _parts: Point[][];
    /** Whether a point in screen pixels is on it: on its line, or inside it when closed. */
    _containsPoint(point: Point, closed?: boolean): boolean;
  }
}

/**
 * A building's footprint, its boxes as the rings of one polygon, that a pointer finds anywhere on it (asked 2026-11-24).
 * The map draws on a canvas, where Leaflet itself tells which shape is under the pointer, by `_containsPoint`. Its
 * polygon's own counts the boxes together, so where two overlap, as in a machine's middle, neither a hover nor a click or
 * a tap reached it; only its edges did. Here a point counts on any of its boxes (inAnyRing), or on its edge, as before.
 */
export class BuildingOutline extends L.Polygon {
  override _containsPoint(point: L.Point): boolean {
    if (!this._pxBounds?.contains(point)) return false;
    return (
      inAnyRing(point, this._parts) || L.Polyline.prototype._containsPoint.call(this, point, true)
    );
  }
}
