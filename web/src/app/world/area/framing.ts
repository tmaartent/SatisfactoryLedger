import { CurvePointDocument } from '../../api/models/curve-point-document';
import { Position } from '../../shared/display';
import { Area, inArea, sampleCurve3d } from './area-model';

/**
 * How far back the camera stands to show a factory `across` wide, as seen from a corner, and `high` tall, on a screen
 * `aspect` wide per unit of height. The camera's angle of view is set upright, so a screen higher than wide, as a
 * phone's, sees less to the sides: for the factory's width the camera stands as much further back as the screen is
 * narrower; its height fits as on a square screen (asked 2026-11-24).
 */
export function framingDistance(size: { across: number; high: number }, aspect: number): number {
  return Math.max(size.high, size.across / Math.min(1, aspect)) * 1.3;
}

/**
 * What the camera frames in an area without buildings, only belts and pipes: the points along them that lie in the
 * area. A belt runs on past it, cut off only at a factory's sides, and would pull the camera its way.
 */
export function pointsToFrame(curves: CurvePointDocument[][], area: Area): Position[] {
  return curves.flatMap((curve) => sampleCurve3d(curve).filter((point) => inArea(point, area)));
}
