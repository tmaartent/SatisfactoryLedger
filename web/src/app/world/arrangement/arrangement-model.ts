// The optimal arrangement's page, apart from Leaflet.
import { WorldRectangle } from '../../api/models/world-rectangle';
import { fromMapPoint, toMapPoint } from '../map/map-model';

/** A rectangle of the world as two opposite corners on the map, as the map places buildings. */
export function mapBounds(rectangle: WorldRectangle): [[number, number], [number, number]] {
  return [
    toMapPoint({ x: rectangle.x0, y: rectangle.y0, z: 0 }),
    toMapPoint({ x: rectangle.x1, y: rectangle.y1, z: 0 }),
  ];
}

/** How far a factory would move: the straight line between the centres, in metres. */
export function movedMetres(now: WorldRectangle, proposed: WorldRectangle): number {
  const centre = (r: WorldRectangle) => [(r.x0 + r.x1) / 2, (r.y0 + r.y1) / 2];
  const [[x0, y0], [x1, y1]] = [centre(now), centre(proposed)];
  return Math.round(Math.hypot(x1 - x0, y1 - y0) / 100);
}

/** The belt work saved, as a share of what it is now: "−34%". */
export function saving(costNow: number, cost: number): string {
  if (costNow <= 0) return '—';
  return `−${Math.round(((costNow - cost) / costNow) * 100)}%`;
}

/** The area to arrange from two corners dragged on the map, in the game's centimetres, whichever way it was dragged. */
export function areaBetween(a: [number, number], b: [number, number]): WorldRectangle {
  const [p, q] = [fromMapPoint(a[0], a[1]), fromMapPoint(b[0], b[1])];
  return {
    x0: Math.min(p.x, q.x),
    y0: Math.min(p.y, q.y),
    x1: Math.max(p.x, q.x),
    y1: Math.max(p.y, q.y),
  };
}

/** A foundation is 8 by 8 metres: players lay out plots and roads in foundations. */
const foundationMetres = 8;

/** A size in foundations, in metres. */
export function metres(foundations: number): number {
  return foundations * foundationMetres;
}

/** A size in metres, in foundations. */
export function foundations(metres: number): number {
  return metres / foundationMetres;
}
