import * as L from 'leaflet';

import { FootprintDocument } from '../../api/models/footprint-document';
import { buildingShape } from './building-shape';

describe('a building on a map', () => {
  // A constructor covers 10 m by 10 m around where it stands.
  const footprints = new Map<string, FootprintDocument>([
    [
      'Build_ConstructorMk1_C',
      { id: 'Build_ConstructorMk1_C', minX: -500, minY: -500, maxX: 500, maxY: 500 },
    ],
  ]);
  const at = { position: { x: 1000, y: 2000, z: 0 }, yaw: 0 };

  it('is the ground it covers, where its class has a footprint', () => {
    const shape = buildingShape({ ...at, className: 'Build_ConstructorMk1_C' }, footprints, {
      color: '#ff4081',
    });

    expect(shape).toBeInstanceOf(L.Polygon);
    expect(shape.options.color).toBe('#ff4081');
  });

  it('is a dot of the size asked where its class has none', () => {
    const shape = buildingShape({ ...at, className: 'Build_ConveyorLiftMk1_C' }, footprints, {}, 3);

    expect(shape).toBeInstanceOf(L.CircleMarker);
    expect(shape instanceof L.CircleMarker && shape.getRadius()).toBe(3);
  });
});
