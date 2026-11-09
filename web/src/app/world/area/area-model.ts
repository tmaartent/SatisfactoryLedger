// The 3D view's logic, apart from three.js: what lies in an area, and where each building's boxes go.

import { CurvePointDocument } from '../../api/models/curve-point-document';
import { FootprintDocument } from '../../api/models/footprint-document';
import { VolumeDocument } from '../../api/models/volume-document';
import { Position } from '../../shared/display';

/** A rectangle on the map, in the game's units (centimetres): x from x0 to x1, y from y0 to y1. Any height. */
export interface Area {
  x0: number;
  y0: number;
  x1: number;
  y1: number;
}

/** The area between two corners of a rectangle drawn on the map, whichever corner the drawing started from. */
export function areaBetween(a: Position, b: Position): Area {
  return { x0: Math.min(a.x, b.x), y0: Math.min(a.y, b.y), x1: Math.max(a.x, b.x), y1: Math.max(a.y, b.y) };
}

export function inArea(position: Position, area: Area): boolean {
  return position.x >= area.x0 && position.x <= area.x1 && position.y >= area.y0 && position.y <= area.y1;
}

/** A belt or pipe belongs to the area when any of its points lies inside it. */
export function curveInArea(curve: CurvePointDocument[], area: Area): boolean {
  return curve.some((point) => inArea(point.location, area));
}

/** A box as its 8 corners: corner k has its x max when bit 2 is set, y max for bit 1, z max for bit 0. */
export type Box = Position[];

/** Turns a vector by a quaternion: v + 2w(q × v) + 2q × (q × v). */
function rotate(q: [number, number, number, number], v: Position): Position {
  const [x, y, z, w] = q;
  const c = { x: y * v.z - z * v.y, y: z * v.x - x * v.z, z: x * v.y - y * v.x };
  const cc = { x: y * c.z - z * c.y, y: z * c.x - x * c.z, z: x * c.y - y * c.x };
  return { x: v.x + 2 * w * c.x + 2 * cc.x, y: v.y + 2 * w * c.y + 2 * cc.y, z: v.z + 2 * w * c.z + 2 * cc.z };
}

/** A clearance box in the building's own coordinates: turned by its quaternion, then moved by its offset. */
export function volumeCorners(volume: VolumeDocument): Box {
  const corners: Box = [];
  for (const x of [volume.minX, volume.maxX]) {
    for (const y of [volume.minY, volume.maxY]) {
      for (const z of [volume.minZ, volume.maxZ]) {
        const turned = rotate([volume.rotationX, volume.rotationY, volume.rotationZ, volume.rotationW], { x, y, z });
        corners.push({ x: turned.x + volume.offsetX, y: turned.y + volume.offsetY, z: turned.z + volume.offsetZ });
      }
    }
  }
  return corners;
}

/** A box from its lowest and highest corner, not turned. */
function plainBox(min: Position, max: Position): Box {
  return volumeCorners({
    minX: min.x, minY: min.y, minZ: min.z, maxX: max.x, maxY: max.y, maxZ: max.z,
    rotationX: 0, rotationY: 0, rotationZ: 0, rotationW: 1, offsetX: 0, offsetY: 0, offsetZ: 0,
  });
}

/** How high a flat footprint is raised when the snapshot has no volumes (older than version 10). */
const FlatFootprintHeight = 400;

/** A building's boxes where it stands, turned by its yaw around the vertical (degrees, from x towards y). */
export function buildingBoxes(
  building: { className: string; position: Position; yaw: number },
  footprints: ReadonlyMap<string, FootprintDocument>,
): Box[] {
  const footprint = footprints.get(building.className);
  const local: Box[] = footprint?.volumes?.length
    ? footprint.volumes.map(volumeCorners)
    : footprint
      ? [plainBox({ x: footprint.minX, y: footprint.minY, z: 0 }, { x: footprint.maxX, y: footprint.maxY, z: FlatFootprintHeight })]
      : [plainBox({ x: -100, y: -100, z: 0 }, { x: 100, y: 100, z: 200 })];

  const turn = (building.yaw * Math.PI) / 180;
  const [cos, sin] = [Math.cos(turn), Math.sin(turn)];
  const { x, y, z } = building.position;
  return local.map((box) => box.map((c) => ({ x: x + c.x * cos - c.y * sin, y: y + c.x * sin + c.y * cos, z: z + c.z })));
}

/**
 * A game position in a three.js scene: metres, around the area's middle. The game has x east, y south and z up, and is
 * left-handed; three.js wants y up and is right-handed. Swapping y and z does both, and keeps north at the top when
 * looking down.
 */
export function toScene(position: Position, middle: { x: number; y: number }): [number, number, number] {
  return [(position.x - middle.x) / 100, position.z / 100, (position.y - middle.y) / 100];
}

/** Points per stretch of a belt between two of its points. */
const StepsPerStretch = 6;

/** A belt's or pipe's curve as points, heights included: a cubic Hermite spline between its points, as on the map. */
export function sampleCurve3d(curve: CurvePointDocument[]): Position[] {
  if (curve.length === 0) return [];
  const points: Position[] = [curve[0].location];
  for (let i = 0; i + 1 < curve.length; i++) {
    const [from, to] = [curve[i].location, curve[i + 1].location];
    const [leave, arrive] = [curve[i].leaveTangent, curve[i + 1].arriveTangent];
    for (let step = 1; step <= StepsPerStretch; step++) {
      const t = step / StepsPerStretch;
      const [h00, h10, h01, h11] = [2 * t ** 3 - 3 * t ** 2 + 1, t ** 3 - 2 * t ** 2 + t, -2 * t ** 3 + 3 * t ** 2, t ** 3 - t ** 2];
      const at = (a: 'x' | 'y' | 'z') => h00 * from[a] + h10 * leave[a] + h01 * to[a] + h11 * arrive[a];
      points.push({ x: at('x'), y: at('y'), z: at('z') });
    }
  }
  return points;
}
