import * as L from 'leaflet';

/**
 * Takes a map down when its page is left, also while it zooms. Leaflet 1.9 ends a zoom animation from a timer that
 * remove() doesn't clear, and the timer then reads the panes remove() took away: "Cannot read properties of undefined
 * (reading '_leaflet_pos')", as when a factory is opened by tapping its label while the map still zooms to it (found by
 * the layout tests, step 98). The timer does nothing once the animation is over: that's said first, by the only flag
 * it checks. Leaflet has no public call for it.
 */
export function removeMap(map: L.Map): void {
  (map as unknown as { _animatingZoom: boolean })._animatingZoom = false;
  map.remove();
}
