import { ArrangedFactoryResponse } from '../../api/models/arranged-factory-response';
import { ArrangementSummary } from '../../api/models/arrangement-summary';
// The optimal arrangement's page, apart from Leaflet.
import { RoutedBeltResponse } from '../../api/models/routed-belt-response';
import { WorldPoint } from '../../api/models/world-point';
import { WorldRectangle } from '../../api/models/world-rectangle';
import { perMinute } from '../../shared/display';
import { FootprintDocument } from '../../api/models/footprint-document';
import { buildingBoxes } from '../area/area-model';
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

/**
 * The machines of an arranged factory where the run would put them (decided 2026-11-18: drawn as the map draws
 * machines): every factory is a blueprint that fills its plot, so the rectangle around their boxes, turned the way
 * they face, stands in the middle of the proposed plot. They keep their places to each other and the way they face.
 * The plot the factory is on now isn't the reference: the plots start at the drawn area's corner, not where the
 * blueprints were placed.
 */
export function placedMachines<
  T extends { className: string; position: { x: number; y: number; z: number }; yaw: number },
>(
  machines: readonly T[],
  footprints: ReadonlyMap<string, FootprintDocument>,
  plot: WorldRectangle,
): T[] {
  if (machines.length === 0) return [];
  const corners = machines.flatMap((machine) => buildingBoxes(machine, footprints).flat());
  const middle = (values: number[]) => (Math.min(...values) + Math.max(...values)) / 2;
  const dx = (plot.x0 + plot.x1) / 2 - middle(corners.map((corner) => corner.x));
  const dy = (plot.y0 + plot.y1) / 2 - middle(corners.map((corner) => corner.y));
  return machines.map((machine) => ({
    ...machine,
    position: { x: machine.position.x + dx, y: machine.position.y + dy, z: machine.position.z },
  }));
}

/** The belt work saved, as a share of what it is now: "−34%". */
export function saving(costNow: number, cost: number): string {
  if (costNow <= 0) return '—';
  return `−${Math.round(((costNow - cost) / costNow) * 100)}%`;
}

/** A factory in the area to arrange: the Factories tab lists it, and the map names it at its middle (asked 2026-11-25). */
export interface AreaFactory {
  factoryId: string;
  name: string;
  /** In the world's centimetres. */
  middle: { x: number; y: number };
  /** What a pick in the Factories tab zooms to (step 105): its plot in a run, or the rectangle around its machines. */
  bounds: WorldRectangle;
}

/**
 * The factories in the area before a run, by name: those whose machines' middle (of the rectangle around their
 * positions) lies inside it. None before an area is drawn.
 */
export function factoriesInArea(
  machines: readonly { id: string; position: WorldPoint }[],
  owners: ReadonlyMap<string, readonly { factoryId: string; name: string }[]>,
  area: WorldRectangle | null,
): AreaFactory[] {
  if (!area) return [];
  const spans = new Map<string, { name: string; x0: number; y0: number; x1: number; y1: number }>();
  for (const machine of machines) {
    const owner = owners.get(machine.id)?.[0];
    if (!owner) continue;
    const { x, y } = machine.position;
    const span = spans.get(owner.factoryId);
    if (span)
      Object.assign(span, {
        x0: Math.min(span.x0, x),
        y0: Math.min(span.y0, y),
        x1: Math.max(span.x1, x),
        y1: Math.max(span.y1, y),
      });
    else spans.set(owner.factoryId, { name: owner.name, x0: x, y0: y, x1: x, y1: y });
  }
  const [left, right] = [Math.min(area.x0, area.x1), Math.max(area.x0, area.x1)];
  const [top, bottom] = [Math.min(area.y0, area.y1), Math.max(area.y0, area.y1)];
  return [...spans]
    .map(([factoryId, { name, x0, y0, x1, y1 }]) => ({
      factoryId,
      name,
      middle: { x: (x0 + x1) / 2, y: (y0 + y1) / 2 },
      bounds: { x0, y0, x1, y1 },
    }))
    .filter(
      ({ middle }) =>
        middle.x >= left && middle.x <= right && middle.y >= top && middle.y <= bottom,
    )
    .sort((a, b) => a.name.localeCompare(b.name));
}

/** A run's factories, by name: at the middle of the plot it proposes, or where they stand when it proposes none. */
export function arrangedFactories(factories: readonly ArrangedFactoryResponse[]): AreaFactory[] {
  const middleOf = (rectangle: WorldRectangle) => ({
    x: (rectangle.x0 + rectangle.x1) / 2,
    y: (rectangle.y0 + rectangle.y1) / 2,
  });
  return factories
    .map((factory) => ({
      factoryId: factory.factoryId,
      name: factory.name ?? '(deleted)',
      middle: middleOf(factory.proposed ?? factory.now),
      bounds: factory.proposed ?? factory.now,
    }))
    .sort((a, b) => a.name.localeCompare(b.name));
}

/** How a run ended, or that it's still computed. */
export function runStatus(run: ArrangementSummary): string {
  if (run.status === 'pending') return 'Computing…';
  if (run.status === 'failed') return `Failed: ${run.error ?? ''}`;
  return run.quality === 'optimal'
    ? 'Optimal'
    : run.quality === 'feasible'
      ? 'Best found in time'
      : 'No layout found: nothing placed';
}

/**
 * A run in one line, as the list of runs shows it under when it was asked for (asked 2026-11-25): its plots and roads
 * in foundations, supply lines when it routes them, the belt work it saves, the belts it left unrouted, its last-resort
 * crossings (step 102), how many placements that took when more than one (step 101), how it ended.
 */
export function runSummary(run: ArrangementSummary): string {
  const size = (metres: number) => String(Math.round(foundations(metres) * 10) / 10);
  const parts = [
    `${size(run.plotWidthMetres)} × ${size(run.plotDepthMetres)}`,
    `roads ${size(run.roadXMetres)} · ${size(run.roadYMetres)}`,
  ];
  if (run.supplyLines) parts.push('supply lines');
  if (run.costNow !== null && run.cost !== null) parts.push(saving(run.costNow, run.cost));
  if (run.unroutedBelts) parts.push(`${run.unroutedBelts} not routed`);
  if (run.lastResortCrossings)
    parts.push(`${run.lastResortCrossings} crossing${run.lastResortCrossings === 1 ? '' : 's'}`);
  if ((run.placements ?? 1) > 1) parts.push(`best of ${run.placements} placements`);
  parts.push(runStatus(run));
  return parts.join(' · ');
}

/**
 * The area to arrange as the address gives it (?x0=…&y0=…&x1=…&y1=…, centimetres): a drag with ctrl held on the world
 * map opens the page so (asked 2026-11-23). None when a corner is missing or isn't a number.
 */
export function areaFromQuery(
  x0: string | undefined,
  y0: string | undefined,
  x1: string | undefined,
  y1: string | undefined,
): WorldRectangle | null {
  const corners = [x0, y0, x1, y1].map((corner) => (corner === undefined ? NaN : Number(corner)));
  if (corners.some((corner) => !Number.isFinite(corner))) return null;
  const [cx0, cy0, cx1, cy1] = corners;
  return { x0: cx0, y0: cy0, x1: cx1, y1: cy1 };
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

/**
 * How long the solver may look for a better layout. It stops at the time, with the best layout it found then, unless it
 * proves one the best first.
 */
export type SolverTime = 'fast' | 'balanced' | 'thorough';

const solverSeconds: Record<SolverTime, number> = { fast: 15, balanced: 60, thorough: 600 };

/** The seconds the solver gets: a quarter of a minute to try settings, a minute, or ten (the most the server allows). */
export function solverTime(time: SolverTime): number {
  return solverSeconds[time];
}

/** A run's seconds as the longest of the three it reaches, for runs asked for in seconds. */
export function solverTimeOf(seconds: number): SolverTime {
  return seconds >= solverSeconds.thorough
    ? 'thorough'
    : seconds >= solverSeconds.balanced
      ? 'balanced'
      : 'fast';
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

/**
 * The existing belts outside the area that routed belts join where they leave the floor (decided 2026-11-18): each with
 * where it comes into the area (its last point, on the area's edge); how each belt joining it gets there from its end
 * on the floor's edge, across the strip of the area that isn't floor and then along the area's edge, at right angles;
 * the factory of one of them for its colour; what it says when pointed at; the levels the belts joining it run on
 * there, to show it with them; and those belts, so it lights up when they do (step 106: it stood dimmed by the factory
 * of its colour, the first on a supply line, while the line was lit for a factory further on). One no routed belt
 * joins is left out.
 */
export function joinedWays<
  Belt extends Pick<
    RoutedBeltResponse,
    | 'from'
    | 'to'
    | 'leavesFloor'
    | 'corners'
    | 'levels'
    | 'item'
    | 'perMinute'
    | 'pipe'
    | 'injection'
  >,
>(
  ways: readonly { spotId: string; points: WorldPoint[] }[],
  belts: readonly Belt[],
  factories: ReadonlySet<string>,
  names: { item: (item: string) => string; building: (className: string) => string },
): {
  points: WorldPoint[];
  factory: string;
  entry: WorldPoint;
  links: WorldPoint[][];
  tooltip: string;
  levels: number[];
  joining: Belt[];
}[] {
  return ways.flatMap((way) => {
    const joining = belts.filter(
      (belt) => belt.leavesFloor && (belt.from === way.spotId || belt.to === way.spotId),
    );
    const entry = way.points[way.points.length - 1];
    if (joining.length === 0 || !entry) return [];
    const links = joining.map((belt) =>
      across(
        belt.from === way.spotId ? belt.corners[0] : belt.corners[belt.corners.length - 1],
        entry,
      ),
    );
    const carried = [
      ...new Set(joining.map((belt) => carries(belt, names.item)).filter((text) => text !== null)),
    ];
    return [
      {
        points: way.points,
        factory: beltEnds(joining[0], factories).factory,
        entry,
        links,
        tooltip: [
          `<b>Existing belt</b> from ${spotName(way.spotId, names.building)}`,
          ...carried,
        ].join('<br>'),
        levels: [
          ...new Set(
            joining.map((belt) =>
              belt.from === way.spotId
                ? (belt.levels[0] ?? 0)
                : (belt.levels[belt.levels.length - 1] ?? 0),
            ),
          ),
        ],
        joining,
      },
    ];
  });
}

/**
 * From a belt's end on the floor's edge to where its existing belt comes in on the area's edge, at right angles: across
 * the strip between them first, then along the area's edge; straight when they're in line.
 */
function across(end: WorldPoint, entry: WorldPoint): WorldPoint[] {
  const corner =
    Math.abs(entry.x - end.x) >= Math.abs(entry.y - end.y)
      ? { x: entry.x, y: end.y }
      : { x: end.x, y: entry.y };
  const same = (a: WorldPoint, b: WorldPoint) => a.x === b.x && a.y === b.y;
  return same(corner, end) || same(corner, entry) ? [end, entry] : [end, corner, entry];
}

/**
 * The building of a spot by its id, as the game names it: "Miner Mk.2", "Miner Mk.2 and 2 more" for buildings merged
 * onto one belt ("…Build_MinerMk2_C_2147339296+2"); the id itself when it names none.
 */
function spotName(spotId: string, building: (className: string) => string): string {
  const className = /(Build_\w+?_C)_\d+/.exec(spotId)?.[1];
  const more = /\+(\d+)/.exec(spotId)?.[1];
  return `${className ? building(className) : spotId}${more ? ` and ${more} more` : ''}`;
}

/** A miner red; an extractor blue while the catalog has no colour for its fluid (before an import that brings them). */
const minerColour = '#e53935';
const extractorColour = '#1e88e5';

/** The catalog's names, with what extractors take and fluids' colours: what a mark at a node needs. */
interface NodeNames {
  item: (item: string) => string;
  building: (className: string) => string;
  extractorForm: (className: string) => 'solid' | 'fluid' | null;
  fluidColour: (item: string) => string | null;
}

/**
 * What marks a node's building (asked 2026-11-19): a miner, red, or an extractor of a fluid or gas, in its colour in the
 * game, with what it is and what the belts from it carry when pointed at. Null at a station or drone port.
 */
function nodeMark(
  spotId: string,
  belts: readonly Pick<RoutedBeltResponse, 'item' | 'perMinute' | 'pipe' | 'injection'>[],
  names: NodeNames,
): { kind: 'miner' | 'extractor'; colour: string; tooltip: string } | null {
  const className = /(Build_\w+?_C)_\d+/.exec(spotId)?.[1];
  const form = className ? names.extractorForm(className) : null;
  if (form === null) return null;
  const item = belts.find((belt) => belt.item)?.item;
  const carried = [
    ...new Set(belts.map((belt) => carries(belt, names.item)).filter((text) => text !== null)),
  ];
  return {
    kind: form === 'solid' ? 'miner' : 'extractor',
    colour:
      form === 'solid' ? minerColour : ((item ? names.fluidColour(item) : null) ?? extractorColour),
    tooltip: [`<b>${spotName(spotId, names.building)}</b>`, ...carried].join('<br>'),
  };
}

/**
 * What marks a routed belt's end at a miner or an extractor in the area, and at which end. Null for a belt between
 * factories, at a station or drone port, which keep their dark dot, and for a belt that leaves the floor to join the
 * existing belt from its building outside the area: its end is on the floor's edge, and the building is marked at the
 * start of that belt (`wayMark`).
 */
export function spotMark(
  belt: Pick<RoutedBeltResponse, 'from' | 'to' | 'item' | 'perMinute' | 'pipe' | 'injection'> &
    Partial<Pick<RoutedBeltResponse, 'leavesFloor'>>,
  factories: ReadonlySet<string>,
  names: NodeNames,
): { kind: 'miner' | 'extractor'; end: 'start' | 'end'; colour: string; tooltip: string } | null {
  const { spot } = beltEnds(belt, factories);
  if (spot === null || belt.leavesFloor) return null;
  const mark = nodeMark(spot === 'start' ? belt.from : belt.to, [belt], names);
  return mark && { ...mark, end: spot };
}

/**
 * What marks the miner or extractor outside the area at the start of an existing belt the routed ones join (asked
 * 2026-11-19), saying what those routed belts carry. Null at a station or drone port, and for a belt no routed belt joins.
 */
export function wayMark(
  way: { spotId: string; points: WorldPoint[] },
  belts: readonly Pick<
    RoutedBeltResponse,
    'from' | 'to' | 'leavesFloor' | 'item' | 'perMinute' | 'pipe' | 'injection'
  >[],
  names: NodeNames,
): { kind: 'miner' | 'extractor'; at: WorldPoint; colour: string; tooltip: string } | null {
  const joining = belts.filter(
    (belt) => belt.leavesFloor && (belt.from === way.spotId || belt.to === way.spotId),
  );
  const at = way.points[0];
  if (joining.length === 0 || !at) return null;
  const mark = nodeMark(way.spotId, joining, names);
  return mark && { kind: mark.kind, at, colour: mark.colour, tooltip: mark.tooltip };
}

/**
 * How a piece of a belt shows in 3D for the levels picked (decided 2026-11-18): a run, a lift, a splitter or merger on
 * it, or the existing belt outside it joins. A belt with a run on a level picked shows whole, so it's seen how it
 * connects: its pieces on the levels picked as they are, the rest faded. One with no run on a level picked is hidden.
 * @param own the levels the piece is on
 * @param belt the levels its belt (or belts) run on
 */
export function levelShown(
  levels: ReadonlySet<number> | null,
  own: readonly number[],
  belt: readonly number[],
): 'full' | 'faded' | 'hidden' {
  if (levels === null) return 'full';
  if (!belt.some((level) => levels.has(level))) return 'hidden';
  return own.some((level) => levels.has(level)) ? 'full' : 'faded';
}

/**
 * The factories shown in 3D for the levels picked (decided 2026-11-18): with every level, all of them (null); else
 * those a belt or pipe running on a picked level comes from or goes to, so only what the lines shown connect shows.
 */
export function shownFactories(
  levels: ReadonlySet<number> | null,
  belts: readonly Pick<RoutedBeltResponse, 'from' | 'to' | 'levels'>[],
): Set<string> | null {
  if (levels === null) return null;
  return new Set(
    belts
      .filter((belt) => belt.levels.some((level) => levels.has(level)))
      .flatMap((belt) => [belt.from, belt.to]),
  );
}

/**
 * The factory picked once one is clicked (asked 2026-11-23): that one alone, letting go of the one picked before, even
 * when the one clicked was lit up as its supplier; none when the one picked is clicked again.
 */
export function pickedAlone(focused: ReadonlySet<string>, factoryId: string): ReadonlySet<string> {
  return focused.has(factoryId) && focused.size === 1 ? new Set() : new Set([factoryId]);
}

/**
 * The factories that stand out with some picked: those picked and those that feed them, by a link whether its belts were
 * routed or not; not those they feed. Null with none picked, when every factory does.
 */
export function relatedFactories(
  focused: ReadonlySet<string>,
  links: readonly { from: string; to: string }[],
): Set<string> | null {
  if (focused.size === 0) return null;
  const related = new Set(focused);
  for (const link of links) {
    if (focused.has(link.to)) related.add(link.from);
  }
  return related;
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
  belt: Pick<RoutedBeltResponse, 'corners' | 'heights' | 'lifts'> &
    Partial<Pick<RoutedBeltResponse, 'from' | 'to' | 'startsOnBelt' | 'endsOnBelt'>>,
  middle: { x: number; y: number },
  ceilingMetres: number,
  factories?: ReadonlySet<string>,
): [number, number, number][] {
  // At an end, a lift: up into a factory; down to the ground at a node, station or drone port, which stand on it, and
  // at the floor's edge, where a belt leaves to join an existing one (when it's known which ends are factories); none
  // where it joins another belt.
  const liftTo = (end: string | undefined, joins: boolean | undefined): number | null =>
    joins
      ? null
      : factories === undefined || (end !== undefined && factories.has(end))
        ? ceilingMetres
        : 0;
  const { corners } = belt;
  const heights = belt.heights.length > 0 ? belt.heights : corners.slice(1).map(() => 0);
  const at = (point: WorldPoint, height: number): [number, number, number] => [
    (point.x - middle.x) / 100,
    height,
    (point.y - middle.y) / 100,
  ];
  const onLift = (point: WorldPoint) =>
    belt.lifts.some((lift) => lift.x === point.x && lift.y === point.y);

  // A lift down from the factory or spot above, unless it starts on another belt, at its smart splitter.
  const start = liftTo(belt.from, belt.startsOnBelt);
  const path = [
    ...(start !== null && start !== heights[0] ? [at(corners[0], start)] : []),
    at(corners[0], heights[0]),
  ];
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
  // A lift up into the factory or spot, unless it ends on another belt, at its priority merger.
  path.push(at(last, heights[heights.length - 1]));
  const end = liftTo(belt.to, belt.endsOnBelt);
  if (end !== null && end !== heights[heights.length - 1]) path.push(at(last, end));
  return path;
}

/** What a routed belt carries, as its tooltip says it: "Iron Ingot · 600/min"; null for runs from before loads. */
export function carries(
  belt: Pick<RoutedBeltResponse, 'item' | 'perMinute' | 'pipe' | 'injection'>,
  name: (item: string) => string,
): string | null {
  if (belt.item == null || belt.perMinute == null) return null;
  return [
    name(belt.item),
    perMinute(belt.perMinute),
    ...(belt.pipe ? ['pipe'] : []),
    ...(belt.injection ? ['into a priority merger'] : []),
  ].join(' · ');
}

/**
 * A splitter or a priority merger of a supply line on the belt floor (decided 2026-11-18), on the map and in 3D: where a
 * belt carrying a top-up's overflow on starts, at its smart splitter; where a line runs on past a factory, at a splitter
 * on the floor (decided 2026-11-19); and where a top-up belt ends, at its priority merger; with what it says when pointed
 * at. On the floor, at the belt's height in metres, where the belt joins
 * another there; else null, up in the factory, where the belts go up into it.
 */
export function lineJoints(
  belts: readonly (Pick<
    RoutedBeltResponse,
    'corners' | 'heights' | 'levels' | 'item' | 'perMinute' | 'pipe' | 'injection' | 'overflow'
  > &
    Partial<Pick<RoutedBeltResponse, 'startsOnBelt' | 'endsOnBelt'>>)[],
  name: (item: string) => string,
): {
  kind: 'smart-splitter' | 'splitter' | 'merger';
  at: WorldPoint;
  height: number | null;
  level: number;
  tooltip: string;
}[] {
  return belts.flatMap((belt) => {
    const [first, last] = [belt.corners[0], belt.corners[belt.corners.length - 1]];
    if (!first || !last) return [];
    const what = belt.item ? name(belt.item) : 'the item';
    return [
      ...(belt.overflow
        ? [
            {
              kind: 'smart-splitter' as const,
              at: first,
              height: belt.startsOnBelt ? (belt.heights[0] ?? 0) : null,
              level: belt.levels[0] ?? 0,
              tooltip: `<b>Smart splitter</b><br>the overflow of ${what} runs on${belt.perMinute != null ? `: ${perMinute(belt.perMinute)}` : ''}`,
            },
          ]
        : belt.startsOnBelt
          ? [
              {
                kind: 'splitter' as const,
                at: first,
                height: belt.heights[0] ?? 0,
                level: belt.levels[0] ?? 0,
                tooltip: `<b>Splitter</b><br>the line of ${what} runs on${belt.perMinute != null ? `: ${perMinute(belt.perMinute)}` : ''}`,
              },
            ]
          : []),
      ...(belt.injection
        ? [
            {
              kind: 'merger' as const,
              at: last,
              height: belt.endsOnBelt ? (belt.heights[belt.heights.length - 1] ?? 0) : null,
              level: belt.levels[belt.levels.length - 1] ?? 0,
              tooltip: `<b>Priority merger</b><br>${carries(belt, name) ?? what}`,
            },
          ]
        : []),
    ];
  });
}

/** A supply line's priority merger orange, smart splitter blue, splitter white: on the map and in 3D. */
export const jointColours = {
  merger: '#ff9800',
  'smart-splitter': '#29b6f6',
  splitter: '#eceff1',
} as const;

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
  belt: Pick<RoutedBeltResponse, 'item' | 'perMinute' | 'pipe' | 'injection' | 'levels'>,
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
  belt: Pick<RoutedBeltResponse, 'corners' | 'heights' | 'levels' | 'lifts'> &
    Partial<Pick<RoutedBeltResponse, 'from' | 'to' | 'startsOnBelt' | 'endsOnBelt'>>,
  middle: { x: number; y: number },
  ceilingMetres: number,
  factories?: ReadonlySet<string>,
): { from: [number, number, number]; to: [number, number, number]; level: number }[] {
  const levelAt = (height: number) => belt.levels[belt.heights.indexOf(height)] ?? 0;
  const path = beltPath3d(belt, middle, ceilingMetres, factories);
  const last = path.length - 2;
  return path.slice(1).map((to, i) => {
    const from = path[i];
    // A lift at an end goes with the belt's run there, up to a factory or down to the ground; a ramp or lift between
    // two runs with the lower.
    const lift = from[0] === to[0] && from[2] === to[2];
    const height =
      lift && i === 0 ? to[1] : lift && i === last ? from[1] : Math.min(from[1], to[1]);
    return { from, to, level: levelAt(height) };
  });
}

/**
 * What the factory preview says of a factory picked: what supplies it, upstream only (FactoryDefinition.SuppliesIn):
 * the sources where its lines start and the pieces between them and it, its own lines from miners included, not its
 * belts between its own machines.
 */
export interface PickedConnections {
  supplies: ReadonlySet<string>;
  /** Per piece across which an input comes into the factory, the items it brings. */
  handovers: ReadonlyMap<string, readonly string[]>;
}

/**
 * What supplies the factories picked before a run (asked 2026-11-23), each factory a whole: the factories that feed
 * them, lit up whole, and the parts between, the belts, pipes, splitters and the miners, stations and drone ports where
 * the lines start, its own lines from miners included. Not what they feed, nor a factory fed from the same splitter. Each by the factory picked it belongs to: the one picked first where two share it,
 * and a picked factory its own.
 * @param ownerOf the factory a machine is in; undefined for anything else.
 */
export function relationsOf(
  picked: ReadonlyMap<string, PickedConnections>,
  ownerOf: (id: string) => string | undefined,
): { factories: Map<string, string>; parts: Map<string, string> } {
  const factories = new Map<string, string>();
  const parts = new Map<string, string>();
  for (const factoryId of picked.keys()) factories.set(factoryId, factoryId);
  for (const [factoryId, { supplies }] of picked) {
    for (const id of supplies) {
      const owner = ownerOf(id);
      if (owner) {
        if (!factories.has(owner)) factories.set(owner, factoryId);
      } else if (!parts.has(id)) parts.set(id, factoryId);
    }
  }
  return { factories, parts };
}

/** A piece of belt, lift or pipe by its two ends, in the game's centimetres. */
export interface LinePiece {
  id: string;
  from: { x: number; y: number; z: number };
  to: { x: number; y: number; z: number };
}

/**
 * The pieces of belt, lift and pipe that meet end to end, one line each (asked 2026-11-23: to say once per line what
 * it carries). Two pieces meet where their ends lie in the same metre, height included, so belts stacked above one
 * another stay apart. A splitter or merger between two pieces ends a line: its pieces don't meet end to end.
 */
export function lineGroups(pieces: readonly LinePiece[]): string[][] {
  const parent = new Map<string, string>(pieces.map((piece) => [piece.id, piece.id]));
  const find = (id: string): string => {
    let root = id;
    while (parent.get(root) !== root) root = parent.get(root)!;
    parent.set(id, root);
    return root;
  };
  const cell = (p: { x: number; y: number; z: number }) =>
    `${Math.round(p.x / 100)}:${Math.round(p.y / 100)}:${Math.round(p.z / 100)}`;
  const atEnd = new Map<string, string>();
  for (const piece of pieces)
    for (const end of [cell(piece.from), cell(piece.to)]) {
      const other = atEnd.get(end);
      if (other) parent.set(find(piece.id), find(other));
      else atEnd.set(end, piece.id);
    }
  const lines = new Map<string, string[]>();
  for (const piece of pieces) {
    const root = find(piece.id);
    lines.set(root, [...(lines.get(root) ?? []), piece.id]);
  }
  return [...lines.values()];
}

/**
 * What a line carries: what's on its belts at save time, and the items its handover into the factory picked brings (a
 * pipe, or an empty belt, says nothing of itself).
 */
export function lineItems(
  line: readonly string[],
  contents: ReadonlyMap<string, Record<string, number> | null>,
  handovers: ReadonlyMap<string, readonly string[]>,
): Set<string> {
  const items = new Set<string>();
  for (const id of line) {
    for (const item of Object.keys(contents.get(id) ?? {})) items.add(item);
    for (const item of handovers.get(id) ?? []) items.add(item);
  }
  return items;
}

/** How long a line must be to say what it carries, in centimetres: 30 metres. */
const LabelledLine = 3000;

/**
 * The lines to say what they carry (asked 2026-11-23): those long enough to run between two places, at least 30 m all
 * told. A factory's manifold is many short stubs from its splitters into its machines: labelling each piled icons up
 * on the factory.
 */
export function linesToLabel(
  lines: readonly string[][],
  lengthOf: (id: string) => number,
): string[][] {
  return lines.filter((line) => line.reduce((sum, id) => sum + lengthOf(id), 0) >= LabelledLine);
}

/** How long a run of corners is on the map, in centimetres. */
function lengthAlong(corners: readonly WorldPoint[]): number {
  let length = 0;
  for (let i = 1; i < corners.length; i++)
    length += Math.hypot(corners[i].x - corners[i - 1].x, corners[i].y - corners[i - 1].y);
  return length;
}

/**
 * The routed belts of a run that say what they carry (asked 2026-11-25), as the lines before a run: with a factory
 * picked, those it stands out with (suppliedBelts), 30 metres long or more, that carry something.
 */
export function beltsToLabel<
  Belt extends Pick<
    RoutedBeltResponse,
    'from' | 'to' | 'item' | 'corners' | 'injection' | 'overflow'
  >,
>(belts: readonly Belt[], focused: ReadonlySet<string>): Belt[] {
  if (focused.size === 0) return [];
  const shown = suppliedBelts(belts, focused);
  return belts.filter(
    (belt) => belt.item != null && shown.has(belt) && lengthAlong(belt.corners) >= LabelledLine,
  );
}

/**
 * The routed belts the factories picked stand out with (step 104, asked 2026-11-25): every belt while none is picked.
 * Otherwise, what supplies them, back to where it starts, and what they make. With supply lines a belt runs from one
 * factory on its line to the next, so the belt into a factory may start under the one before it: a belt carrying an item
 * from a factory that item comes into runs on from that factory, and its way back goes on through every belt bringing
 * the item there, the line's and its top-ups; an overflow's only through the top-ups, whose smart splitter it leaves.
 * Such a belt running on past a factory picked isn't what it makes, and stays out. A belt from before what it carries
 * was kept isn't traced back.
 */
export function suppliedBelts<
  Belt extends Pick<RoutedBeltResponse, 'from' | 'to' | 'item' | 'injection' | 'overflow'>,
>(belts: readonly Belt[], focused: ReadonlySet<string>): Set<Belt> {
  if (focused.size === 0) return new Set(belts);
  const into = (factory: string, item: string | null) =>
    item == null ? [] : belts.filter((belt) => belt.to === factory && belt.item === item);
  const shown = new Set<Belt>();
  const back = (belt: Belt): void => {
    if (shown.has(belt)) return;
    shown.add(belt);
    for (const before of into(belt.from, belt.item))
      if (!belt.overflow || before.injection) back(before);
  };
  for (const belt of belts)
    if (focused.has(belt.to)) back(belt);
    else if (focused.has(belt.from) && into(belt.from, belt.item).length === 0) shown.add(belt);
  return shown;
}

/** Where a routed belt crosses another as a last resort (step 102), as the map and the 3D view mark it. */
export interface CrossingMark {
  at: WorldPoint;
  /** The belt's height on its run there, in metres above the belt floor. */
  height: number;
  level: number;
  tooltip: string;
}

/**
 * The last-resort crossings of the routed belts (step 102, decided 2026-11-25): where one crosses another where one of
 * them turns, or parts from a stack swapped, on another level. Each at the belt's height on the first run it lies on.
 */
export function crossingMarks(
  belts: readonly Pick<
    RoutedBeltResponse,
    'corners' | 'heights' | 'levels' | 'lastResortCrossings'
  >[],
): CrossingMark[] {
  const between = (value: number, a: number, b: number) =>
    value >= Math.min(a, b) - 1 && value <= Math.max(a, b) + 1;
  return belts.flatMap((belt) =>
    belt.lastResortCrossings.map((at) => {
      const run = Math.max(
        0,
        belt.corners.findIndex(
          (corner, i) =>
            i + 1 < belt.corners.length &&
            between(at.x, corner.x, belt.corners[i + 1].x) &&
            between(at.y, corner.y, belt.corners[i + 1].y),
        ),
      );
      return {
        at,
        height: belt.heights[run] ?? 0,
        level: belt.levels[run] ?? 0,
        tooltip:
          '<b>Crosses another belt here</b><br>on another level: a last resort, where it could not get through otherwise',
      };
    }),
  );
}

/** The point halfway along a run of corners, around its turns. */
export function halfway(corners: readonly WorldPoint[]): WorldPoint {
  let left = lengthAlong(corners) / 2;
  for (let i = 1; i < corners.length; i++) {
    const [a, b] = [corners[i - 1], corners[i]];
    const step = Math.hypot(b.x - a.x, b.y - a.y);
    if (step >= left && step > 0) {
      const t = left / step;
      return { x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t, z: a.z };
    }
    left -= step;
  }
  return corners[corners.length - 1];
}

/**
 * How high a point of an existing belt outside the area is drawn in 3D, in metres over the belt floor (asked 2026-11-23:
 * as built, not flat): its height in the game relative to where it comes into the area, which lies on the floor, where
 * the routed belt joins it. On the floor where either height isn't known: a run from before heights were kept.
 */
export function wayHeight(point: WorldPoint, entry: WorldPoint): number {
  return point.z != null && entry.z != null ? (point.z - entry.z) / 100 : 0;
}
