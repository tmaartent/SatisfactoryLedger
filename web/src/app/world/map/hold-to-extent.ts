import * as L from 'leaflet';

/** What holding a map needs of it: as much of a Leaflet map as is used here. */
interface Holdable {
  getSize(): { x: number; y: number };
  getBoundsZoom(bounds: L.LatLngBounds): number;
  setMinZoom(zoom: number): unknown;
  setMaxBounds(bounds?: L.LatLngBounds): unknown;
}

/**
 * Holds a map to the map picture (asked 2026-11-20, and for the optimal arrangement 2026-11-23): it zooms out no
 * further than the whole of it and moves no further than its edges, which mapExtent widens where buildings stand past
 * them. Without a picture, the whole world can be seen, down to the least zoom.
 */
export function holdToExtent(
  map: Holdable,
  extent: [[number, number], [number, number]] | null,
  leastZoom: number,
): void {
  if (!extent) {
    map.setMaxBounds();
    map.setMinZoom(leastZoom);
    return;
  }
  const size = map.getSize();
  // Not laid out yet: the zoom that fits it is known once the map has a size, and a resize asks again.
  if (size.x === 0 || size.y === 0) return;
  const bounds = L.latLngBounds(extent);
  map.setMinZoom(map.getBoundsZoom(bounds));
  map.setMaxBounds(bounds);
}
