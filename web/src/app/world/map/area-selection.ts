import { signal } from '@angular/core';
import * as L from 'leaflet';
import { Area, areaBetween } from '../area/area-model';
import { fromMapPoint } from './map-model';

/**
 * Selecting an area on a touch screen, with no keys to hold (asked 2026-11-24, ADR-0033): a button turns it on; the map
 * stops panning, and a one-finger drag draws a rectangle instead. Letting go keeps it, for the page to offer what to do
 * with it; a drag shorter than the page's minimum a side, as a tap, lets go of it. The world map's and the optimal
 * arrangement's.
 *
 * By pointer events: a finger moving fires no mouse events, which drawing with a key held listens to. A second finger,
 * as in a pinch, draws nothing.
 */
export class AreaSelection {
  readonly selecting = signal(false);
  /** The area drawn, in the game's centimetres, once let go; null while none is. */
  readonly drawn = signal<Area | null>(null);
  private map?: L.Map;
  private box: L.Rectangle | null = null;

  constructor(
    /** The least a side of an area, in the game's centimetres: a smaller drag is a tap. */
    private readonly minimum: number,
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
      const area = areaDrawn(from, map.mouseEventToLatLng(event), this.minimum);
      from = null;
      if (area) this.drawn.set(area);
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

/** What the area chooser for a mouse needs from its page. */
export interface AreaDragOptions<K extends string> {
  /** The least a side of an area, in the game's centimetres: a smaller drag is a tap, and chooses nothing. */
  minimum: number;
  /** What a press of the left button draws, by the keys held and what the page allows; null, and the map pans. */
  kindOf: (press: PointerEvent) => K | null;
  /** How the rectangle looks while it's dragged. */
  style: (kind: K) => L.PathOptions;
  /** The area chosen, of the kind its press drew, once let go. */
  drawn: (kind: K, area: Area) => void;
}

/**
 * Choosing an area with a mouse: a drag with a key held draws a rectangle, and letting go chooses the area inside it.
 * Which key draws what is the page's: on the world map, shift opens it in 3D and ctrl arranges it (asked 2026-11-23);
 * on the optimal arrangement, shift draws the area to arrange and ctrl a part kept out. The world map's and the optimal
 * arrangement's, beside AreaSelection for a touch screen.
 *
 * The map listens in the capture phase, so it hears the press before Leaflet's panning does, and can switch the panning
 * off in time. The page turns Leaflet's own shift-drag, zooming to a box, off.
 */
export class AreaDrag<K extends string> {
  private readonly current = signal<K | null>(null);
  /** What a drag on the map draws now; null while not drawing, and the map pans. */
  readonly drawing = this.current.asReadonly();

  constructor(private readonly options: AreaDragOptions<K>) {}

  /** Lets go of what a drag would draw, as the page starts over. */
  letGo(): void {
    this.current.set(null);
  }

  /** Listens on the map, once it's made. */
  attach(map: L.Map): void {
    let start: L.LatLng | null = null;
    let rectangle: L.Rectangle | null = null;
    // What the drag draws, kept from its start: letting go ends drawing before Leaflet's mouseup comes.
    let draws: K | null = null;
    map.getContainer().addEventListener(
      'pointerdown',
      (event) => {
        const kind = this.options.kindOf(event);
        if (!kind || event.button !== 0) return;
        this.current.set(kind);
        map.dragging.disable();
      },
      { capture: true },
    );
    // However the press ends, panning comes back: a press on a shape that stops it never reaches the map's mousedown.
    map.getContainer().addEventListener('pointerup', () => {
      if (this.drawing()) this.stop(map);
    });
    map.on('mousedown', (event: L.LeafletMouseEvent) => {
      const kind = this.drawing();
      if (!kind) return;
      draws = kind;
      start = event.latlng;
      rectangle = L.rectangle(L.latLngBounds(start, start), {
        ...this.options.style(kind),
        interactive: false,
      }).addTo(map);
    });
    map.on('mousemove', (event: L.LeafletMouseEvent) => {
      if (start && rectangle) rectangle.setBounds(L.latLngBounds(start, event.latlng));
    });
    map.on('mouseup', (event: L.LeafletMouseEvent) => {
      if (!start || !draws) return;
      const area = areaDrawn(start, event.latlng, this.options.minimum);
      const drew = draws;
      rectangle?.remove();
      [start, rectangle, draws] = [null, null, null];
      this.stop(map);
      if (area) this.options.drawn(drew, area);
    });
  }

  /** Stops drawing: the map pans again on a drag. */
  private stop(map: L.Map): void {
    this.current.set(null);
    map.dragging.enable();
  }
}

/** Whether a press holds ctrl, or ⌘ on a Mac: the key both pages' choosers take for their second kind of area. */
export function ctrlOrCommand(press: PointerEvent): boolean {
  return press.ctrlKey || press.metaKey;
}

/**
 * The area between two corners drawn on the map, in the game's centimetres, whichever corner the drawing started from;
 * null when a side is shorter than the minimum: a drag that small is a tap.
 */
function areaDrawn(from: L.LatLng, to: L.LatLng, minimum: number): Area | null {
  const area = areaBetween(fromMapPoint(from.lat, from.lng), fromMapPoint(to.lat, to.lng));
  return area.x1 - area.x0 >= minimum && area.y1 - area.y0 >= minimum ? area : null;
}
