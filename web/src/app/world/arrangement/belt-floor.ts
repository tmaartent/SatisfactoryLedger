import { ArrangementResponse } from '../../api/models/arrangement-response';
import { RoutedBeltResponse } from '../../api/models/routed-belt-response';
import { WorldPoint } from '../../api/models/world-point';
import { WorldRectangle } from '../../api/models/world-rectangle';
import {
  beltEnds,
  beltTooltip,
  CrossingMark,
  crossingMarks,
  factoryTooltip,
  jointArrows,
  jointColours,
  joinedWays,
  levelShown,
  lineJoints,
  movedMetres,
  relatedFactories,
  shownFactories,
  NodeNames,
  SourceKind,
  spotMark,
  suppliedBelts,
  wayMark,
} from './arrangement-model';

/** What the belt floor of a run is drawn from: the run, the factories' colours, the names, and the factories picked. */
export interface BeltFloorOf {
  arrangement: ArrangementResponse;
  /** A factory's colour, as the map draws it. */
  colourOf: (factoryId: string) => string | undefined;
  /** The catalog's names and the factory a machine belongs to (`spotNames`). */
  names: NodeNames;
  /** The factories picked: what supplies them stands out, the rest fades. */
  focused: ReadonlySet<string>;
  /**
   * The levels picked in 3D (decided 2026-11-18): only what runs on them shows, and a belt's pieces on the others fade;
   * null, or left out as on the map, for every level.
   */
  levels?: ReadonlySet<number> | null;
}

/** A factory on the plot the run proposes for it, above the floor. */
export interface FactoryMark {
  factoryId: string;
  plot: WorldRectangle;
  colour: string;
  /** Its name, and how far it moves. */
  tooltip: string;
  /** Edged wider: it's picked. */
  standsOut: boolean;
  /** With factories picked, one that doesn't feed one of them. */
  faded: boolean;
  /** Not shown in 3D: no belt on a level picked comes from it or goes to it. */
  hidden: boolean;
}

/** A routed belt or pipe on the floor. */
export interface BeltMark {
  /** Where it runs: its corners, heights, levels and lifts. */
  belt: RoutedBeltResponse;
  /** Its factory's colour; between two, the one it leaves, on the map and in 3D (decided 2026-11-28). */
  colour: string;
  tooltip: string;
  /** Drawn wider: it supplies a factory picked, or one picked makes it. */
  standsOut: boolean;
  faded: boolean;
  /** Not shown in 3D: it has no run on a level picked. */
  hidden: boolean;
  /** Whether its piece on a level fades in 3D: the belt does, or the level isn't picked. */
  fadedOn: (level: number) => boolean;
  /** What marks its end outside the arranged factories: a miner, an extractor, a station or machines; null for none. */
  spot: SpotMark | null;
  /** On the map, a dot at each end that doesn't join another belt (in 3D the lifts are part of the belt). */
  lifts: LiftMark[];
}

/**
 * Where a line starts or ends outside the arranged factories (asked 2026-11-19 and 2026-11-25): a miner, red; an
 * extractor in the colour of its fluid; a station or drone port, dark; machines in their factory's colour, or grey.
 */
export interface SpotMark {
  kind: SourceKind;
  at: WorldPoint;
  colour: string;
  tooltip: string;
  faded: boolean;
  hidden: boolean;
}

/** A belt's lift on the map, where it goes up into a factory or down to a spot. */
export interface LiftMark {
  at: WorldPoint;
  /** The belt's colour at a factory; the map's dark dot at a spot. */
  colour: string;
  /** The spot's mark, drawn instead of the dot, at the end where the belt's spot is. */
  spot: SpotMark | null;
  /** An amber ring: the belt leaves the floor here for an existing belt outside the area that isn't drawn coming in. */
  ring: boolean;
}

/** What the belt floor shows: each mark with where it is, its colour, whether it's faded, and its tooltip. */
export interface BeltFloor {
  factories: FactoryMark[];
  belts: BeltMark[];
  ways: WayMark[];
  joints: JointMark[];
  crossings: CrossingOnFloor[];
}

/**
 * A supply line's priority merger, orange, smart splitter, blue, or splitter, white (decided 2026-11-18 and 2026-11-19),
 * with the ways the items go in and out of it on the floor (asked 2026-11-26).
 */
export interface JointMark {
  kind: 'smart-splitter' | 'splitter' | 'merger';
  at: WorldPoint;
  /** On the floor, at the belt's height in metres, where the belt joins another there; else null, up in the factory. */
  height: number | null;
  colour: string;
  tooltip: string;
  /** Half a foundation from it, pointing the way the items go; none up in a factory. */
  arrows: ReturnType<typeof jointArrows>;
  /** Faded with its belt, on the map and in 3D (decided 2026-11-28), and in 3D on a level not picked. */
  faded: boolean;
  hidden: boolean;
}

/** Where a routed belt crosses another as a last resort (step 102): an amber mark, to check in the game. */
export type CrossingOnFloor = Omit<CrossingMark, 'level'> & {
  /** Faded with its belt, on the map and in 3D (decided 2026-11-28), and in 3D on a level not picked. */
  faded: boolean;
  hidden: boolean;
};

/**
 * An existing belt outside the area that routed belts join where they leave the floor (decided 2026-11-18), from the
 * building it starts at to where it comes into the area; with the way each joining belt gets there from the floor's
 * edge. It stands out and fades with the belts joining it (step 106).
 */
export interface WayMark {
  /** As built, from the building outside to where it comes in. */
  points: WorldPoint[];
  /** Where it comes in, on the area's edge: an amber ring. */
  entry: WorldPoint;
  /** From each joining belt's end on the floor's edge to the entry, at right angles. */
  links: WorldPoint[][];
  colour: string;
  tooltip: string;
  standsOut: boolean;
  faded: boolean;
  /** Not shown in 3D: none of the belts joining it runs on a level picked. */
  hidden: boolean;
  /** The building it starts at, marked as a spot is; null for none. */
  spot: { kind: SourceKind; at: WorldPoint; colour: string; tooltip: string } | null;
}

/**
 * What the belt floor of a run shows (card C of 2026-11-28), the same flat on the map and in 3D: the two views only
 * draw these marks.
 */
export function beltFloor({
  arrangement,
  colourOf,
  names,
  focused,
  levels = null,
}: BeltFloorOf): BeltFloor {
  const floor: Floor = {
    arrangement,
    colourOf,
    names,
    focused,
    levels,
    arranged: new Set(arrangement.factories.map((factory) => factory.factoryId)),
    shown: suppliedBelts(arrangement.belts, focused),
  };
  return {
    factories: factoryMarks(floor),
    belts: arrangement.belts.map((belt) => beltMark(floor, belt)),
    ways: wayMarks(floor),
    joints: jointMarks(floor),
    crossings: crossingsOnFloor(floor),
  };
}

/** What every mark is worked out from: the input, the factories arranged, and the belts that stand out. */
type Floor = Required<BeltFloorOf> & {
  arranged: ReadonlySet<string>;
  shown: ReadonlySet<RoutedBeltResponse>;
};

/**
 * Whether a piece on the levels `own` fades and hides in 3D, for the levels picked (`levelShown`), when its belt or
 * belts run on `belts`; faded too when `faded` already, for the factories picked.
 */
function onLevels(
  floor: Floor,
  own: readonly number[],
  belts: readonly number[],
  faded: boolean,
): { faded: boolean; hidden: boolean } {
  const shown = levelShown(floor.levels, own, belts);
  return { faded: faded || shown === 'faded', hidden: shown === 'hidden' };
}

function factoryMarks({ arrangement, colourOf, focused, levels }: Floor): FactoryMark[] {
  const related = relatedFactories(focused, arrangement.links);
  const connected = shownFactories(levels, arrangement.belts);
  return arrangement.factories.flatMap(({ factoryId, name, now, proposed }) =>
    proposed
      ? [
          {
            factoryId,
            plot: proposed,
            colour: colourOf(factoryId) ?? '#90a4ae',
            tooltip: factoryTooltip(name ?? '(deleted)', movedMetres(now, proposed)),
            standsOut: focused.has(factoryId),
            faded: related !== null && !related.has(factoryId),
            hidden: connected !== null && !connected.has(factoryId),
          },
        ]
      : [],
  );
}

function beltMark(floor: Floor, belt: RoutedBeltResponse): BeltMark {
  const { arranged, colourOf, focused, names, shown } = floor;
  const faded = !shown.has(belt);
  const ends = beltEnds(belt, arranged);
  const colour = colourOf(ends.factory) ?? '#e0e0e0';
  const spot = spotOf(floor, belt, faded);
  // Spots whose existing belt comes into the area: their amber ring stands where it comes in, drawn with the way.
  const comingIn = (spotId: string) => floor.arrangement.ways.some((way) => way.spotId === spotId);
  const lift = (at: WorldPoint, end: 'start' | 'end'): LiftMark => {
    const atSpot = ends.spot === end;
    return {
      at,
      colour: atSpot ? '#37474f' : colour,
      spot: atSpot ? spot : null,
      ring: atSpot && belt.leavesFloor && !comingIn(end === 'start' ? belt.from : belt.to),
    };
  };
  const [first, last] = [belt.corners[0], belt.corners[belt.corners.length - 1]];
  return {
    belt,
    colour,
    tooltip: beltTooltip(belt, names.item),
    standsOut: focused.size > 0 && !faded,
    faded,
    hidden: levelShown(floor.levels, [], belt.levels) === 'hidden',
    fadedOn: (level) => onLevels(floor, [level], belt.levels, faded).faded,
    spot,
    // None where it joins another belt, at a splitter or merger.
    lifts: [
      ...(belt.startsOnBelt || !first ? [] : [lift(first, 'start')]),
      ...(belt.endsOnBelt || !last ? [] : [lift(last, 'end')]),
    ],
  };
}

/** A spot's mark at its end of the belt, on the level the belt runs on there; none without corners. */
function spotOf(floor: Floor, belt: RoutedBeltResponse, faded: boolean): SpotMark | null {
  const mark = spotMark(belt, floor.arranged, floor.names);
  if (!mark) return null;
  const [at, level] =
    mark.end === 'start'
      ? [belt.corners[0], belt.levels[0]]
      : [belt.corners[belt.corners.length - 1], belt.levels[belt.levels.length - 1]];
  if (!at) return null;
  const { kind, colour, tooltip } = mark;
  return { kind, at, colour, tooltip, ...onLevels(floor, [level ?? 0], belt.levels, faded) };
}

function wayMarks(floor: Floor): WayMark[] {
  const { arrangement, arranged, colourOf, focused, names, shown } = floor;
  return arrangement.ways.flatMap((spot) =>
    joinedWays([spot], arrangement.belts, arranged, names).map((way): WayMark => {
      const faded = !way.joining.some((belt) => shown.has(belt));
      const joiningLevels = [...new Set(way.joining.flatMap((belt) => belt.levels))];
      const mark = wayMark(spot, arrangement.belts, names);
      return {
        points: way.points,
        entry: way.entry,
        links: way.links,
        colour: colourOf(way.factory) ?? '#e0e0e0',
        tooltip: way.tooltip,
        standsOut: focused.size > 0 && !faded,
        ...onLevels(floor, way.levels, joiningLevels, faded),
        spot: mark && { kind: mark.kind, at: mark.at, colour: mark.colour, tooltip: mark.tooltip },
      };
    }),
  );
}

function jointMarks(floor: Floor): JointMark[] {
  const { arrangement, names, shown } = floor;
  return arrangement.belts.flatMap((belt) =>
    lineJoints([belt], names.item).map((joint): JointMark => ({
      kind: joint.kind,
      at: joint.at,
      height: joint.height,
      colour: jointColours[joint.kind],
      tooltip: joint.tooltip,
      arrows: jointArrows(belt, joint.kind === 'merger' ? 'end' : 'start', arrangement.belts),
      ...onLevels(floor, [joint.level], belt.levels, !shown.has(belt)),
    })),
  );
}

function crossingsOnFloor(floor: Floor): CrossingOnFloor[] {
  return floor.arrangement.belts.flatMap((belt) =>
    crossingMarks([belt]).map(({ at, height, level, tooltip }): CrossingOnFloor => ({
      at,
      height,
      tooltip,
      ...onLevels(floor, [level], belt.levels, !floor.shown.has(belt)),
    })),
  );
}
