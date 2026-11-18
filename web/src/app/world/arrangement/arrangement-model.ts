// The optimal arrangement's page, apart from Leaflet.
import { RoutedBeltResponse } from '../../api/models/routed-belt-response';
import { WorldPoint } from '../../api/models/world-point';
import { WorldRectangle } from '../../api/models/world-rectangle';
import { perMinute } from '../../shared/display';
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

/** A foundation is 4 metres high: the belt floor's ceiling is given in foundation heights. */
const foundationHeightMetres = 4;

/** A ceiling in foundation heights, in metres. */
export function ceilingMetres(heights: number): number {
  return heights * foundationHeightMetres;
}

/** A ceiling in metres, in foundation heights. */
export function ceilingHeights(metres: number): number {
  return metres / foundationHeightMetres;
}

/** A routed belt on the map: through its corners, straight from one to the next. */
export function beltLine(corners: readonly WorldPoint[]): [number, number][] {
  return corners.map((corner) => toMapPoint({ x: corner.x, y: corner.y, z: 0 }));
}

/**
 * The factory a routed belt serves and its other end: another factory, or the spot of a node's extractor, station or
 * drone port (decided 2026-11-18).
 */
export function beltEnds(
  belt: Pick<RoutedBeltResponse, 'from' | 'to'>,
  factories: ReadonlySet<string>,
): { factory: string; spot: 'start' | 'end' | null } {
  if (factories.has(belt.from) && factories.has(belt.to)) return { factory: belt.from, spot: null };
  return factories.has(belt.to)
    ? { factory: belt.to, spot: 'start' }
    : { factory: belt.from, spot: 'end' };
}

/** How long a ramp of 2 metres is along its belt: half a foundation. */
const rampMetres = 4;

/**
 * A routed belt on the belt floor in 3D, in metres from a middle point: x east, y up, z along the game's y. It comes
 * down its lift from the factory floor at the ceiling, runs each straight stretch at its height, changes height where
 * it turns, and goes up its lift at the other end. A change of 2 metres is a ramp over the last 4 before the turn; 4
 * or more is a lift, straight up or down at the turn (decided 2026-11-18). Runs from before levels lie on the floor.
 */
export function beltPath3d(
  belt: Pick<RoutedBeltResponse, 'corners' | 'heights' | 'lifts'>,
  middle: { x: number; y: number },
  ceilingMetres: number,
): [number, number, number][] {
  const { corners } = belt;
  const heights = belt.heights.length > 0 ? belt.heights : corners.slice(1).map(() => 0);
  const at = (point: WorldPoint, height: number): [number, number, number] => [
    (point.x - middle.x) / 100,
    height,
    (point.y - middle.y) / 100,
  ];
  const onLift = (point: WorldPoint) =>
    belt.lifts.some((lift) => lift.x === point.x && lift.y === point.y);

  const path = [at(corners[0], ceilingMetres), at(corners[0], heights[0])];
  for (let i = 1; i < corners.length - 1; i++) {
    const [before, corner, after] = [heights[i - 1], corners[i], heights[i]];
    if (before === after) {
      path.push(at(corner, before));
    } else if (onLift(corner)) {
      path.push(at(corner, before), at(corner, after));
    } else {
      const from = corners[i - 1];
      const length = Math.hypot(corner.x - from.x, corner.y - from.y);
      const back = Math.min(rampMetres * 100, length) / length;
      const start = {
        x: corner.x - (corner.x - from.x) * back,
        y: corner.y - (corner.y - from.y) * back,
      };
      path.push(at(start, before), at(corner, after));
    }
  }

  const last = corners[corners.length - 1];
  path.push(at(last, heights[heights.length - 1]), at(last, ceilingMetres));
  return path;
}

/** What a routed belt carries, as its tooltip says it: "Iron Ingot · 600/min"; null for runs from before loads. */
export function carries(
  belt: Pick<RoutedBeltResponse, 'item' | 'perMinute' | 'pipe'>,
  name: (item: string) => string,
): string | null {
  if (belt.item == null || belt.perMinute == null) return null;
  return `${name(belt.item)} · ${perMinute(belt.perMinute)}${belt.pipe ? ' · pipe' : ''}`;
}

/** What a proposed factory says when pointed at, on the map and in 3D: its name, and how far it moves. */
export function factoryTooltip(name: string, metres: number): string {
  return `<b>${name}</b><br>${metres > 0 ? `moves ${metres} m` : 'stays'}`;
}

/**
 * The level a routed belt runs on, from 0 on the floor: 1 on one stacked belt, 2 on two. "level 2", or each level in
 * turn where it changes: "levels 0 → 1 → 3". Null for runs from before levels.
 */
export function levelText(levels: readonly number[]): string | null {
  const changes = levels.filter((level, i) => i === 0 || level !== levels[i - 1]);
  if (changes.length === 0) return null;
  return changes.length === 1 ? `level ${changes[0]}` : `levels ${changes.join(' → ')}`;
}

/** What a routed belt says when pointed at, on the map and in 3D: what it carries, and its level. */
export function beltTooltip(
  belt: Pick<RoutedBeltResponse, 'item' | 'perMinute' | 'pipe' | 'levels'>,
  name: (item: string) => string,
): string {
  return [carries(belt, name) ?? 'From a run before loads', levelText(belt.levels)]
    .filter((part) => part !== null)
    .join(' · ');
}

/**
 * A routed belt in 3D as straight stretches from point to point, each with the level it belongs to, for building it
 * level by level: a run on its own level; a ramp or lift between two on the lower; its lifts at the ends on the level
 * they reach. From 0 for runs from before levels.
 */
export function beltStretches3d(
  belt: Pick<RoutedBeltResponse, 'corners' | 'heights' | 'levels' | 'lifts'>,
  middle: { x: number; y: number },
  ceilingMetres: number,
): { from: [number, number, number]; to: [number, number, number]; level: number }[] {
  const levelAt = (height: number) => belt.levels[belt.heights.indexOf(height)] ?? 0;
  const path = beltPath3d(belt, middle, ceilingMetres);
  return path.slice(1).map((to, i) => ({
    from: path[i],
    to,
    level: levelAt(Math.min(path[i][1], to[1])),
  }));
}
