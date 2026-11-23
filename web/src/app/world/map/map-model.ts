// The world map's logic, apart from Leaflet: where things go, and what colour they get.

import { CurvePointDocument } from '../../api/models/curve-point-document';
import { FootprintDocument } from '../../api/models/footprint-document';
import { buildingBoxes, fromAbove } from '../area/area-model';
import { MachineFinding } from '../../api/models/machine-finding';
import { MachineLimit } from '../../api/models/machine-limit';
import { MapProvider } from '../../api/models/map-provider';
import { WorldMapResponse } from '../../api/models/world-map-response';
import { Position } from '../../shared/display';
import { ShownStatus } from '../shown-status';

/** How a machine runs, or 'unknown' while its snapshot's analysis isn't ready. */
export type MachineStatus = MachineLimit | 'unknown';

/**
 * A game position (centimetres) as a point on the map: [latitude, longitude] in metres, as Leaflet's flat
 * coordinate system (CRS.Simple) wants them. The game's y grows southwards, the map's latitude upwards.
 */
export function toMapPoint(position: Position): [number, number] {
  return [-position.y / 100, position.x / 100];
}

/**
 * The game's map picture under the map (ADR-0020): each tile with the corners of its part of the world, as map points.
 * The API gives them in centimetres; the map has metres, north up. Nothing without map art.
 */
export function mapArtTiles(
  art:
    { tiles: { x0: number; y0: number; x1: number; y1: number; url: string }[] } | null | undefined,
): { url: string; bounds: [[number, number], [number, number]] }[] {
  return (art?.tiles ?? []).map((tile) => ({
    url: tile.url,
    bounds: [
      toMapPoint({ x: tile.x0, y: tile.y0, z: 0 }),
      toMapPoint({ x: tile.x1, y: tile.y1, z: 0 }),
    ],
  }));
}

/** The corners of the world the map picture shows, as map points: the map is held to it (asked 2026-11-20). */
export function mapArtExtent(
  art: { x0: number; y0: number; x1: number; y1: number } | null | undefined,
): [[number, number], [number, number]] | null {
  return art
    ? [toMapPoint({ x: art.x0, y: art.y0, z: 0 }), toMapPoint({ x: art.x1, y: art.y1, z: 0 })]
    : null;
}

/** How far past the furthest building the map may still move, in metres, so it isn't drawn against the edge. */
export const MapMargin = 50;

/**
 * The corners the map is held to: the map picture's, reaching further where buildings stand past its edges, as the game
 * allows (asked 2026-11-23). Belts and pipes count by their points. Null without map art: then nothing holds the map.
 */
export function mapExtent(
  art: { x0: number; y0: number; x1: number; y1: number } | null | undefined,
  world:
    | Pick<WorldMapResponse, 'machines' | 'providers' | 'logistics' | 'consumers' | 'conveyors'>
    | null
    | undefined,
): [[number, number], [number, number]] | null {
  const extent = mapArtExtent(art);
  if (!extent) return null;

  const points = [
    ...[
      ...(world?.machines ?? []),
      ...(world?.providers ?? []),
      ...(world?.logistics ?? []),
      ...(world?.consumers ?? []),
    ].map((b) => toMapPoint(b.position)),
    ...(world?.conveyors ?? []).flatMap((conveyor) =>
      conveyor.curve.map((point) => toMapPoint(point.location)),
    ),
  ];
  // Not Math.min(...points): a large world's belts have more points than a call takes arguments.
  const furthest = points.reduce(
    (far, [lat, lng]) => ({
      south: Math.min(far.south, lat),
      west: Math.min(far.west, lng),
      north: Math.max(far.north, lat),
      east: Math.max(far.east, lng),
    }),
    { south: Infinity, west: Infinity, north: -Infinity, east: -Infinity },
  );
  // A side reaches past the picture only when a building does: then to the furthest one, and the margin past it.
  const reach = (picture: number, building: number, outwards: number) =>
    building * outwards > picture * outwards ? building + outwards * MapMargin : picture;
  return [
    [
      reach(Math.min(extent[0][0], extent[1][0]), furthest.south, -1),
      reach(Math.min(extent[0][1], extent[1][1]), furthest.west, -1),
    ],
    [
      reach(Math.max(extent[0][0], extent[1][0]), furthest.north, 1),
      reach(Math.max(extent[0][1], extent[1][1]), furthest.east, 1),
    ],
  ];
}

/** A point on the map (latitude and longitude in metres) as a game position at height 0: the opposite of toMapPoint. */
export function fromMapPoint(lat: number, lng: number): Position {
  return { x: Math.round(lng * 100), y: Math.round(-lat * 100), z: 0 };
}

/**
 * A building as the ground it covers, as shapes on the map: each of its boxes seen from above, turned the way it faces
 * and moved to where it stands (ADR-0020). A Quantum Encoder is its cross of boxes, not the rectangle around them.
 */
export function footprintShapes(
  building: { className: string; position: Position; yaw: number },
  footprints: ReadonlyMap<string, FootprintDocument>,
): [number, number][][] {
  return buildingBoxes(building, footprints).map((box) => fromAbove(box).map(toMapPoint));
}

/** Points per bent stretch of a belt: enough for a smooth curve at the closest zoom. */
const StepsPerBend = 8;

/**
 * A belt's or pipe's curve as a line on the map. Between two points the game's curve is a cubic Hermite spline: it
 * leaves one point along its leave tangent and arrives at the next along its arrive tangent. A straight stretch (both
 * tangents along it) needs only its ends; a bend is sampled.
 */
export function sampleCurve(curve: CurvePointDocument[]): [number, number][] {
  if (curve.length === 0) return [];
  const line: [number, number][] = [toMapPoint(curve[0].location)];
  for (let i = 0; i + 1 < curve.length; i++) {
    const [from, to] = [curve[i].location, curve[i + 1].location];
    const [leave, arrive] = [curve[i].leaveTangent, curve[i + 1].arriveTangent];
    const steps = isStraight(from, to, leave, arrive) ? 1 : StepsPerBend;
    for (let step = 1; step <= steps; step++) {
      const t = step / steps;
      const [h00, h10, h01, h11] = [
        2 * t ** 3 - 3 * t ** 2 + 1,
        t ** 3 - 2 * t ** 2 + t,
        -2 * t ** 3 + 3 * t ** 2,
        t ** 3 - t ** 2,
      ];
      line.push(
        toMapPoint({
          x: h00 * from.x + h10 * leave.x + h01 * to.x + h11 * arrive.x,
          y: h00 * from.y + h10 * leave.y + h01 * to.y + h11 * arrive.y,
          z: 0,
        }),
      );
    }
  }
  return line;
}

/** Both tangents point along the stretch from one point to the next (seen from above), so it's a straight line. */
function isStraight(from: Position, to: Position, leave: Position, arrive: Position): boolean {
  const [dx, dy] = [to.x - from.x, to.y - from.y];
  const length = Math.hypot(dx, dy);
  if (length < 1) return true;
  const along = (tangent: Position) => {
    const size = Math.hypot(tangent.x, tangent.y);
    return size < 1e-9 || Math.abs(dx * tangent.y - dy * tangent.x) / (length * size) < 0.01;
  };
  return along(leave) && along(arrive);
}

/**
 * Where a buildable is on the map, to zoom to it from a finding: where it stands, or halfway along a belt or pipe. Null
 * when the map doesn't have it.
 */
export function locate(world: WorldMapResponse, id: string): [number, number] | null {
  const building = [
    ...world.machines,
    ...world.providers,
    ...world.logistics,
    ...world.consumers,
  ].find((b) => b.id === id);
  if (building) return toMapPoint(building.position);

  const conveyor = world.conveyors.find((c) => c.id === id);
  if (!conveyor || conveyor.curve.length === 0) return null;
  const [first, last] = [
    conveyor.curve[0].location,
    conveyor.curve[conveyor.curve.length - 1].location,
  ];
  return toMapPoint({ x: (first.x + last.x) / 2, y: (first.y + last.y) / 2, z: 0 });
}

/** How each machine (or generator) runs, by id, from the findings; empty while there are none. */
export function machineStatuses(
  findings: Pick<MachineFinding, 'id' | 'limit'>[] | undefined,
): Map<string, MachineLimit> {
  return new Map((findings ?? []).map((machine) => [machine.id, machine.limit]));
}

/** The ids of the machines or generators of the status picked in the overview (asked 2026-11-23); null when none is picked. */
export function buildingsShown(
  analysis:
    | {
        machines: { id: string; limit: MachineLimit }[];
        generators: { id: string; limit: MachineLimit }[];
      }
    | undefined,
  shown: ShownStatus | null,
): Set<string> | null {
  if (!shown) return null;
  const buildings =
    shown.of === 'machines' ? (analysis?.machines ?? []) : (analysis?.generators ?? []);
  return new Set(
    buildings.filter((building) => building.limit === shown.limit).map((building) => building.id),
  );
}

/** What stands on each resource node, by the node's name: a node with none is free, where a miner or extractor could still go. */
export function nodeTakers(providers: MapProvider[]): Map<string, MapProvider> {
  return new Map(
    providers
      .filter((provider) => provider.node !== null)
      .map((provider) => [provider.node!, provider]),
  );
}

/** Colours on the dark theme: running green, held back red or amber, without a recipe or unknown grey. */
export const statusColours: Record<MachineStatus, string> = {
  none: '#4caf50',
  starved: '#ef5350',
  outputBlocked: '#ffa726',
  noRecipe: '#9e9e9e',
  unknown: '#78909c',
};

export const statusLabels: Record<MachineStatus, string> = {
  none: 'Running',
  starved: 'Starved',
  outputBlocked: 'Output blocked',
  noRecipe: 'No recipe',
  unknown: 'Not analysed yet',
};

/** What's on a belt, lift, splitter or in a machine, at save time: per item, a count (fluids in m³). */
export type Contents = Record<string, number>;

/** What something holds, item by item, most first, counts rounded to two decimals: for a page to show with icons. */
export function contentEntries(contents: Contents): { item: string; count: number }[] {
  return Object.entries(contents)
    .sort(([, a], [, b]) => b - a)
    .map(([item, count]) => ({ item, count: Number(count.toFixed(2)) }));
}

/** "Iron Ore × 7, Iron Ingot × 2", most first; "empty"; or "" when the snapshot doesn't know (older than version 6). */
export function describeContents(
  contents: Contents | null | undefined,
  name: (item: string) => string,
): string {
  if (!contents) return '';
  const entries = contentEntries(contents);
  return entries.length === 0
    ? 'empty'
    : entries.map(({ item, count }) => `${name(item)} × ${count}`).join(', ');
}

/** Belts grey, pipes teal; a bottleneck red and thicker (BR-A12). */
export const conveyorColours = { belt: '#90a4ae', pipe: '#4dd0e1', bottleneck: '#ef5350' };

/** Node purities, by how much they give: impure ×0.5, normal ×1, pure ×2 (BR-W9). */
export const purityColours: Record<string, string> = {
  // Green, yellow and red, as the owner reads them (asked 2026-11-23).
  impure: '#e53935',
  normal: '#fdd835',
  pure: '#43a047',
};

/** The game's icon before a name in a tooltip (ADR-0032), which Leaflet takes as HTML; nothing when there's none. */
export function iconTag(url: string | null): string {
  return url
    ? `<img class="tooltip-icon" src="${escapeHtml(url)}" width="16" height="16" alt="">`
    : '';
}

/**
 * A resource node on the map, free or not (asked 2026-11-23): a pin, as on the game's own map. Its resource's icon on its
 * purity's colour in a white ring, on a white stick down to a dot where the node lies. Styled by .node-pin in
 * styles.scss: Leaflet puts markers outside the page's component.
 */
export function nodeMarkerHtml(url: string, purityColour: string): string {
  return `<div class="node-pin" style="--purity: ${purityColour}"><div class="node-marker"><img src="${escapeHtml(url)}" alt=""></div></div>`;
}

/**
 * How big a resource node's marker is at a zoom level, in pixels (asked 2026-11-23): 22 at zoom -2, and about 1.4 times
 * as big for each step in, half as fast as the map grows, so it keeps to its place as the land grows under it. Never
 * smaller than 18, to make out the icon, nor bigger than 72.
 */
export function nodeMarkerSize(zoom: number): number {
  return Math.round(Math.min(72, Math.max(18, 22 * 2 ** ((zoom + 2) / 2))));
}

/** Text as it reads in a tooltip, which Leaflet takes as HTML: a player's factory name may hold <, & or quotes. */
export function escapeHtml(text: string): string {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

/**
 * How a machine or a factory's generator is drawn: filled by how it runs, edged in its factory's colour, boldly when its
 * factory is the one highlighted, and dimmed when another one is.
 * @param inHighlighted whether it's in the highlighted factory; null when no factory is highlighted.
 */
export function buildingStyle(
  fill: string,
  factoryColour: string | undefined,
  inHighlighted: boolean | null,
) {
  const dimmed = inHighlighted === false;
  return {
    color: factoryColour ?? fill,
    weight: inHighlighted ? 3.5 : factoryColour ? 2 : 1,
    fillColor: fill,
    fillOpacity: dimmed ? 0.15 : 0.75,
    opacity: dimmed ? 0.15 : 1,
  };
}
