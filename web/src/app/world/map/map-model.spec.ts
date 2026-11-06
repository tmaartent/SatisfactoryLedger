import { MachineFinding } from '../../api/models/machine-finding';
import { MapProvider } from '../../api/models/map-provider';
import { ResourceNodeResponse } from '../../api/models/resource-node-response';
import { CurvePointDocument } from '../../api/models/curve-point-document';
import { FootprintDocument } from '../../api/models/footprint-document';
import { footprintCorners, freeNodes, machineStatuses, sampleCurve, toMapPoint } from './map-model';

describe('map model', () => {
  it('places a position in metres, with north up', () => {
    // The game's y grows southwards; the map's latitude grows upwards.
    expect(toMapPoint({ x: 12345, y: -50000, z: 900 })).toEqual([500, 123.45]);
  });

  it('knows how each machine runs once the analysis is ready', () => {
    const findings = [{ id: 'smelter', limit: 'starved' } as MachineFinding];

    expect(machineStatuses(findings).get('smelter')).toBe('starved');
  });

  it("calls a machine's status unknown while there are no findings", () => {
    expect(machineStatuses(undefined).get('smelter') ?? 'unknown').toBe('unknown');
  });

  it('finds the resource nodes no provider stands on', () => {
    const node = (name: string) => ({ name }) as ResourceNodeResponse;
    const providers = [{ node: 'BP_ResourceNode35' } as MapProvider, { node: null } as MapProvider];

    expect(freeNodes([node('BP_ResourceNode35'), node('BP_ResourceNode36')], providers).map((n: ResourceNodeResponse) => n.name)).toEqual([
      'BP_ResourceNode36',
    ]);
  });

  const point = (x: number, y: number, arrive: [number, number], leave: [number, number]): CurvePointDocument => ({
    location: { x, y, z: 0 },
    arriveTangent: { x: arrive[0], y: arrive[1], z: 0 },
    leaveTangent: { x: leave[0], y: leave[1], z: 0 },
  });

  it('draws a straight belt as a line between its two ends', () => {
    const straight = [point(0, 0, [-1, 0], [-450, 0]), point(-900, 0, [-450, 0], [-1, 0])];

    expect(sampleCurve(straight)).toEqual([toMapPoint({ x: 0, y: 0, z: 0 }), toMapPoint({ x: -900, y: 0, z: 0 })]);
  });

  it('follows a bend through the middle of its curve, not the straight line', () => {
    // Leaves along +x, arrives along +y: halfway along the Hermite curve it's at (62.5, 37.5) cm.
    const bend = [point(0, 0, [100, 0], [100, 0]), point(100, 100, [0, 100], [0, 100])];

    const line = sampleCurve(bend);

    expect(line[0]).toEqual(toMapPoint({ x: 0, y: 0, z: 0 }));
    expect(line[line.length - 1]).toEqual(toMapPoint({ x: 100, y: 100, z: 0 }));
    expect(line).toContainEqual(toMapPoint({ x: 62.5, y: 37.5, z: 0 }));
  });

  const constructor: FootprintDocument = { id: 'Build_ConstructorMk1_C', minX: -400, minY: -500, maxX: 400, maxY: 500 };
  const origin = { x: 0, y: 0, z: 0 };
  // The extent of corners on the map, in metres: [south, north] and [west, east].
  const extent = (corners: [number, number][]) => ({
    lat: [Math.min(...corners.map((c) => c[0])), Math.max(...corners.map((c) => c[0]))].map((v) => Math.round(v * 100) / 100),
    lng: [Math.min(...corners.map((c) => c[1])), Math.max(...corners.map((c) => c[1]))].map((v) => Math.round(v * 100) / 100),
  });

  it("draws an unturned constructor's footprint 8 m along x and 10 m along y", () => {
    expect(extent(footprintCorners(origin, 0, constructor))).toEqual({ lat: [-5, 5], lng: [-4, 4] });
  });

  it('turns a footprint with its building', () => {
    // A quarter turn: its 10 m now run along x, its 8 m along y.
    expect(extent(footprintCorners(origin, 90, constructor))).toEqual({ lat: [-4, 4], lng: [-5, 5] });
  });

  it('keeps a footprint that sticks out to one side where it is', () => {
    // A miner covers y −5 to 9 m in its own coordinates; the game's y grows southwards, the map's latitude upwards.
    const miner: FootprintDocument = { id: 'Build_MinerMk1_C', minX: -300, minY: -500, maxX: 300, maxY: 900 };

    expect(extent(footprintCorners({ x: 1000, y: 0, z: 0 }, 0, miner))).toEqual({ lat: [-9, 5], lng: [7, 13] });
  });
});
