import { signal } from '@angular/core';
import * as L from 'leaflet';

/** Two corners of a rectangle drawn on a map, where the drag started and where it was let go. */
export interface DrawnCorners {
  from: L.LatLng;
  to: L.LatLng;
}

/**
 * Selecting an area on a touch screen, with no keys to hold (asked 2026-11-24, ADR-0033): a button turns it on; the map
 * stops panning, and a one-finger drag draws a rectangle instead. Letting go keeps it, for the page to offer what to do
 * with it; a drag the page finds too small, as a tap, lets go of it. The world map's and the optimal arrangement's.
 *
 * By pointer events: a finger moving fires no mouse events, which drawing with a key held listens to. A second finger,
 * as in a pinch, draws nothing.
 */
export class AreaSelection {
  readonly selecting = signal(false);
  /** The rectangle drawn, once let go; null while none is. */
  readonly drawn = signal<DrawnCorners | null>(null);
  private map?: L.Map;
  private box: L.Rectangle | null = null;

  constructor(
    /** Whether a drag from one corner to the other is an area, not a tap. */
    private readonly keeps: (corners: DrawnCorners) => boolean,
    private readonly style: L.PathOptions,
  ) {}

  /** Listens on the map, once it's made. */
  attach(map: L.Map): void {
    this.map = map;
    const container = map.getContainer();
    let from: L.LatLng | null = null;
    container.addEventListener('pointerdown', (event) => {
      if (!this.selecting() || !event.isPrimary || event.button !== 0) return;
      this.clear();
      from = map.mouseEventToLatLng(event);
      this.box = L.rectangle(L.latLngBounds(from, from), {
        ...this.style,
        interactive: false,
      }).addTo(map);
    });
    container.addEventListener('pointermove', (event) => {
      if (from && event.isPrimary)
        this.box?.setBounds(L.latLngBounds(from, map.mouseEventToLatLng(event)));
    });
    const letGo = (event: PointerEvent) => {
      if (!from || !event.isPrimary) return;
      const corners = { from, to: map.mouseEventToLatLng(event) };
      from = null;
      if (this.keeps(corners)) this.drawn.set(corners);
      else this.clear();
    };
    container.addEventListener('pointerup', letGo);
    container.addEventListener('pointercancel', letGo);
  }

  /** The select button: selecting starts, and the map no longer pans; or it stops, and lets go of what was drawn. */
  toggle(): void {
    const on = !this.selecting();
    this.selecting.set(on);
    this.clear();
    if (on) this.map?.dragging.disable();
    else this.map?.dragging.enable();
  }

  /** Lets go of the rectangle drawn, still selecting. */
  clear(): void {
    this.box?.remove();
    this.box = null;
    this.drawn.set(null);
  }
}
