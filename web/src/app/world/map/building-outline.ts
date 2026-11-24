import * as L from 'leaflet';

import { inAnyRing } from './ring-hit';

/** What a Leaflet polygon keeps once drawn, which its typings leave out: its rings in screen pixels, and their bounds. */
interface Drawn {
  _pxBounds?: L.Bounds;
  _parts: L.Point[][];
}

/**
 * A building's footprint, its boxes as the rings of one polygon, that a pointer finds anywhere on it (asked 2026-11-24).
 * The map draws on a canvas, where Leaflet itself tells which shape is under the pointer, by `_containsPoint`. Its
 * polygon's own counts the boxes together, so where two overlap, as in a machine's middle, neither a hover nor a click or
 * a tap reached it; only its edges did. Here a point counts on any of its boxes (inAnyRing), or on its edge, as before.
 */
export class BuildingOutline extends L.Polygon {
  _containsPoint(point: L.Point): boolean {
    const drawn = this as unknown as Drawn;
    if (!drawn._pxBounds?.contains(point)) return false;
    const onEdge = (
      L.Polyline.prototype as unknown as {
        _containsPoint(this: unknown, point: L.Point, closed: boolean): boolean;
      }
    )._containsPoint;
    return inAnyRing(point, drawn._parts) || onEdge.call(this, point, true);
  }
}
