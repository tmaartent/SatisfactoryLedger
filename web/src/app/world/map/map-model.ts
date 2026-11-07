// The world map's logic, apart from Leaflet: where things go, and what colour they get.

import { CurvePointDocument } from '../../api/models/curve-point-document';
import { FootprintDocument } from '../../api/models/footprint-document';
import { MachineFinding } from '../../api/models/machine-finding';
import { MachineLimit } from '../../api/models/machine-limit';
import { MapProvider } from '../../api/models/map-provider';
import { ResourceNodeResponse } from '../../api/models/resource-node-response';
import { WorldMapResponse } from '../../api/models/world-map-response';
import { Position } from '../../shared/display';

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
 * The four corners of a building's footprint on the map: its rectangle, in its own coordinates, turned by its yaw and
 * moved to where it stands (ADR-0020).
 */
export function footprintCorners(position: Position, yaw: number, footprint: FootprintDocument): [number, number][] {
  const turn = (yaw * Math.PI) / 180;
  const [cos, sin] = [Math.cos(turn), Math.sin(turn)];
  const corners: [number, number][] = [
    [footprint.minX, footprint.minY],
    [footprint.maxX, footprint.minY],
    [footprint.maxX, footprint.maxY],
    [footprint.minX, footprint.maxY],
  ];
  return corners.map(([x, y]) => toMapPoint({ x: position.x + x * cos - y * sin, y: position.y + x * sin + y * cos, z: 0 }));
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
      const [h00, h10, h01, h11] = [2 * t ** 3 - 3 * t ** 2 + 1, t ** 3 - 2 * t ** 2 + t, -2 * t ** 3 + 3 * t ** 2, t ** 3 - t ** 2];
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
  const building = [...world.machines, ...world.providers, ...world.logistics, ...world.consumers].find((b) => b.id === id);
  if (building) return toMapPoint(building.position);

  const conveyor = world.conveyors.find((c) => c.id === id);
  if (!conveyor || conveyor.curve.length === 0) return null;
  const [first, last] = [conveyor.curve[0].location, conveyor.curve[conveyor.curve.length - 1].location];
  return toMapPoint({ x: (first.x + last.x) / 2, y: (first.y + last.y) / 2, z: 0 });
}

/** How each machine (or generator) runs, by id, from the findings; empty while there are none. */
export function machineStatuses(findings: Pick<MachineFinding, 'id' | 'limit'>[] | undefined): Map<string, MachineLimit> {
  return new Map((findings ?? []).map((machine) => [machine.id, machine.limit]));
}

/** The resource nodes no provider stands on: where a miner or extractor could still go. */
export function freeNodes(nodes: ResourceNodeResponse[], providers: MapProvider[]): ResourceNodeResponse[] {
  const taken = new Set(providers.map((provider) => provider.node).filter((node) => node !== null));
  return nodes.filter((node) => !taken.has(node.name));
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

/** "Iron Ore × 7, Iron Ingot × 2", most first; "empty"; or "" when the snapshot doesn't know (older than version 6). */
export function describeContents(contents: Contents | null | undefined, name: (item: string) => string): string {
  if (!contents) return '';
  const entries = Object.entries(contents).sort(([, a], [, b]) => b - a);
  return entries.length === 0 ? 'empty' : entries.map(([item, count]) => `${name(item)} × ${Number(count.toFixed(2))}`).join(', ');
}

/** What a belt carries most of; null when it carries nothing. */
export function mainItem(contents: Contents | null | undefined): string | null {
  const entries = Object.entries(contents ?? {});
  return entries.length === 0 ? null : entries.reduce((most, entry) => (entry[1] > most[1] ? entry : most))[0];
}

/** Distinct colours on the dark theme, for "Colour belts by what they carry". */
const ItemPalette = [
  '#e57373', '#64b5f6', '#81c784', '#ffb74d', '#ba68c8', '#4dd0e1', '#fff176', '#f06292',
  '#a1887f', '#90a4ae', '#aed581', '#7986cb', '#ff8a65', '#4db6ac', '#dce775', '#9575cd',
];

/** The same colour for an item every time, from its class name. */
export function itemColour(item: string): string {
  let hash = 0;
  for (const character of item) hash = (hash * 31 + character.charCodeAt(0)) | 0;
  return ItemPalette[Math.abs(hash) % ItemPalette.length];
}

/** Belts grey, pipes teal; a bottleneck red and thicker (BR-A12). */
export const conveyorColours = { belt: '#90a4ae', pipe: '#4dd0e1', bottleneck: '#ef5350' };

/** Node purities, by how much they give: impure ×0.5, normal ×1, pure ×2 (BR-W9). */
export const purityColours: Record<string, string> = {
  impure: '#8d6e63',
  normal: '#bdbdbd',
  pure: '#fff176',
};
