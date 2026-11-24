import * as L from 'leaflet';

import { FootprintDocument } from '../../api/models/footprint-document';
import { Position } from '../../shared/display';
import { BuildingOutline } from './building-outline';
import { footprintShapes, toMapPoint } from './map-model';

/**
 * A building on a map as the ground it covers, each of its boxes seen from above, turned the way it faces (ADR-0020); a
 * dot when its class has no footprint, as a lift, or when the snapshot is older than its footprints. The world map's and
 * the optimal arrangement's, so both draw a building alike (asked 2026-11-23).
 */
export function buildingShape(
  building: { className: string; position: Position; yaw: number },
  footprints: ReadonlyMap<string, FootprintDocument>,
  style: L.PathOptions,
  dot = 4,
): L.Path {
  return footprints.has(building.className)
    ? new BuildingOutline(
        footprintShapes(building, footprints).map((ring) => [ring]),
        { ...style, fillRule: 'nonzero' },
      )
    : L.circleMarker(toMapPoint(building.position), { ...style, radius: dot });
}
