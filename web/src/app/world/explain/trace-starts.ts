import { TraceCause } from '../../api/models/trace-cause';
import { MoreCapacity, TraceBuildings, TraceStart, TraceStep } from './assistant';

/** Where a shortage or blockage starts, as the explanation panel lists it beside the model's text. */
export interface StartRow {
  cause: string;
  item: string;
  /**
   * What the buildings it starts at make, send, carry or take, per minute; null when the starts on this row give
   * different rates, or when the cause's rate is always nothing.
   */
  perMinute: number | null;
  /** What the buildings it starts at do with the item: "Sent by", "Taken at full speed by". */
  atLabel: string;
  at: Place[];
  /** "Short of it" or "Can't get it away": the machines on the way that the start holds back first. */
  heldLabel: string | null;
  held: Place[];
  /** The items on the way from the machines asked about, before this one: "Nuclear Pasta ← Pressure Conversion Cube". */
  via: string | null;
  /** "Also taken by" or "Also supplied by", when others share the item. */
  othersLabel: string | null;
  others: Place[];
  /** A bottleneck: what carries all that's offered to it, "2 lines of Conveyor Belt Mk.6 side by side", and how much. */
  needs: { what: string; perMinute: number } | null;
}

/** Buildings of one kind: "Converter (Dark Matter Residue)", how many, and the first one's id to show on the map. */
export interface Place {
  name: string;
  /** The building alone, without its recipe: for its icon. */
  building: string;
  count: number;
  perMinute: number | null;
  focus: string | null;
}

/**
 * What the buildings a start is at do with the item, per cause, and whether its rate says anything. A Record over the
 * API's union, so a new cause won't compile until it has its label. The cause's own words come with the trace, as the
 * ledger's term (causeTerm): the MCP server keeps them, for the model and this list alike.
 */
const causes: Record<TraceCause, { at: string; rate: boolean }> = {
  tooFewProducers: { at: 'Made at full speed by', rate: true },
  beltFull: { at: 'At capacity', rate: true },
  sourceAtLimit: { at: 'Given by', rate: true },
  takenByOthers: { at: 'Sent by', rate: true },
  nothingBringsIt: { at: 'At', rate: false },
  noRecipe: { at: 'Without a recipe', rate: false },
  unknownSupply: { at: 'Sent by', rate: true },
  producerOutputBlocked: { at: 'Sent by', rate: true },
  tooFewConsumers: { at: 'Taken at full speed by', rate: true },
  suppliedByOthers: { at: 'Taken by', rate: true },
  consumerStarved: { at: 'Taken by', rate: true },
  storageFull: { at: 'Full', rate: false },
  nowhereToGo: { at: 'At', rate: false },
  unclear: { at: 'At', rate: false },
};

interface Row extends Omit<StartRow, 'at' | 'held' | 'others'> {
  at: Map<string, Merged>;
  held: Map<string, Merged>;
  others: Map<string, Merged>;
}

interface Merged extends Place {
  ids: Set<string>;
}

/**
 * The trace's starts, one row per cause, item and way there. A trace lists a start for each path that reaches it, so the
 * same cause often comes more than once (four Freight Platforms, four starts): one row holds them, counting each machine
 * once. Starts on different ways stay apart, so a start further up says which shortage it belongs to.
 */
export function whereItStarts(starts: TraceStart[]): StartRow[] {
  const rows = new Map<string, Row>();
  for (const start of starts) {
    const way = wayTo(start);
    const needs = start.moreCapacity ? needsOf(start.moreCapacity) : null;
    const key = `${start.cause}|${start.item}|${way.via ?? ''}|${needs?.what ?? ''}|${needs?.perMinute ?? ''}`;
    const cause = causes[start.cause];
    let row = rows.get(key);
    if (!row) {
      row = {
        cause: start.causeTerm.charAt(0).toUpperCase() + start.causeTerm.slice(1),
        item: start.item,
        perMinute: cause.rate ? start.perMinute : null,
        atLabel: cause.at,
        at: new Map(),
        heldLabel: way.heldLabel,
        held: new Map(),
        via: way.via,
        othersLabel: null,
        others: new Map(),
        needs,
      };
      rows.set(key, row);
    } else if (row.perMinute !== start.perMinute) {
      row.perMinute = null;
    }
    add(row.at, start.at);
    add(row.held, way.held);
    add(row.others, start.others ?? []);
    if (row.others.size > 0)
      row.othersLabel = start.cause === 'suppliedByOthers' ? 'Also supplied by' : 'Also taken by';
  }
  return [...rows.values()].map((row) => ({
    ...row,
    at: places(row.at),
    held: places(row.held),
    others: places(row.others),
  }));
}

function needsOf(more: MoreCapacity): { what: string; perMinute: number } {
  return {
    what: more.lines > 1 ? `${more.lines} lines of ${more.tier} side by side` : more.tier,
    perMinute: more.carriesPerMinute,
  };
}

/**
 * The way from the machines asked about to a start: the machines of its last step, which the start holds back first,
 * and the items before the start's own, upstream joined by "←" (made from), downstream by "→" (goes on to).
 */
function wayTo(start: TraceStart): {
  heldLabel: string | null;
  held: TraceBuildings[];
  via: string | null;
} {
  const last = start.path.at(-1);
  if (!last) return { heldLabel: null, held: [], via: null };
  const upstream = last.shortOf !== undefined;
  const items: string[] = [];
  for (const step of start.path) {
    if (items.at(-1) !== itemOf(step)) items.push(itemOf(step));
  }
  if (items.at(-1) === start.item) items.pop();
  return {
    heldLabel: upstream ? 'Short of it' : "Can't get it away",
    held: lastStep(start.path).map((step) => ({
      building: step.building,
      recipe: step.recipe,
      count: step.machines,
      ids: step.ids,
    })),
    via: items.length > 0 ? items.join(upstream ? ' ← ' : ' → ') : null,
  };
}

/** The last step's machines: the path lists a step's machines one kind at a time, each with the step's item. */
function lastStep(path: TraceStep[]): TraceStep[] {
  const item = itemOf(path[path.length - 1]);
  let first = path.length - 1;
  while (first > 0 && itemOf(path[first - 1]) === item) first--;
  return path.slice(first);
}

function itemOf(step: TraceStep): string {
  return step.shortOf ?? step.cannotLeave ?? '';
}

/**
 * Adds the machines not on the row yet, and what they take or give. A group lists only its first ten ids, so it counts
 * by its count, less the listed machines the row has already; a group whose listed machines are all there is one seen
 * again on another path.
 */
function add(places: Map<string, Merged>, groups: TraceBuildings[]): void {
  for (const group of groups) {
    const name = group.recipe ? `${group.building} (${group.recipe})` : group.building;
    const place = places.get(name) ?? {
      name,
      building: group.building,
      count: 0,
      perMinute: null,
      focus: group.ids[0] ?? null,
      ids: new Set<string>(),
    };
    places.set(name, place);
    const added = group.ids.filter((id) => !place.ids.has(id));
    if (group.ids.length > 0 && added.length === 0) continue;
    added.forEach((id) => place.ids.add(id));
    place.count += group.count - (group.ids.length - added.length);
    if (group.perMinute !== undefined) place.perMinute = (place.perMinute ?? 0) + group.perMinute;
  }
}

function places(merged: Map<string, Merged>): Place[] {
  return [...merged.values()].map(({ name, building, count, perMinute, focus }) => ({
    name,
    building,
    count,
    perMinute,
    focus,
  }));
}
