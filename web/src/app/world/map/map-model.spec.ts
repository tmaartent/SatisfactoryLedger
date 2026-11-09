import { MachineFinding } from '../../api/models/machine-finding';
import { MapProvider } from '../../api/models/map-provider';
import { ResourceNodeResponse } from '../../api/models/resource-node-response';
import { CurvePointDocument } from '../../api/models/curve-point-document';
import { FootprintDocument } from '../../api/models/footprint-document';
import { WorldMapResponse } from '../../api/models/world-map-response';
import { describeContents, footprintCorners, freeNodes, fromMapPoint, itemColour, locate, machineStatuses, mainItem, sampleCurve, toMapPoint } from './map-model';

describe('map model', () => {
  it('turns a point on the map back into a game position, the opposite of placing it', () => {
    expect(fromMapPoint(500, 123.45)).toEqual({ x: 12345, y: -50000, z: 0 });
  });

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

  const name = (id: string) => id.replace('Desc_', '').replace('_C', '');

  it('describes what a belt carries, most first', () => {
    expect(describeContents({ Desc_IronIngot_C: 2, Desc_OreIron_C: 7 }, name)).toBe('OreIron × 7, IronIngot × 2');
  });

  it('says a belt with nothing on it is empty', () => {
    expect(describeContents({}, name)).toBe('empty');
  });

  it("says nothing when the snapshot doesn't know what's on it", () => {
    expect(describeContents(null, name)).toBe('');
  });

  it('shows fluids in cubic metres, rounded', () => {
    expect(describeContents({ Desc_Water_C: 12.3456 }, name)).toBe('Water × 12.35');
  });

  it('takes what a belt carries most of as its main item', () => {
    expect(mainItem({ Desc_IronIngot_C: 2, Desc_OreIron_C: 7 })).toBe('Desc_OreIron_C');
    expect(mainItem({})).toBeNull();
  });

  it('gives an item the same colour every time, and ore and ingots different ones', () => {
    expect(itemColour('Desc_OreIron_C')).toBe(itemColour('Desc_OreIron_C'));
    expect(itemColour('Desc_OreIron_C')).not.toBe(itemColour('Desc_IronIngot_C'));
  });

  describe('finding a buildable to zoom to', () => {
    const building = (id: string, x: number, y: number) => ({ id, className: 'X', position: { x, y, z: 0 }, yaw: 0, contents: null });
    const world = {
      machines: [{ ...building('smelter', 10000, 20000), recipe: null }],
      providers: [{ ...building('miner', 30000, 0), node: null, resource: null, purity: null }],
      logistics: [building('splitter', 0, 0)],
      consumers: [building('drone port', -50000, 0)],
      conveyors: [
        {
          id: 'belt',
          className: 'Build_ConveyorBeltMk1_C',
          kind: 'belt',
          contents: null,
          curve: [point(0, 0, [1000, 0], [1000, 0]), point(1000, 0, [1000, 0], [1000, 0])],
        },
      ],
      footprints: [],
    } as unknown as WorldMapResponse;

    it('finds a machine where it stands', () => {
      expect(locate(world, 'smelter')).toEqual([-200, 100]);
    });

    it('finds providers, logistics and consumers too', () => {
      expect([locate(world, 'miner'), locate(world, 'splitter'), locate(world, 'drone port')]).toEqual([
        [-0, 300],
        [-0, 0],
        [-0, -500],
      ]);
    });

    it('finds a belt halfway along it', () => {
      expect(locate(world, 'belt')).toEqual([-0, 5]);
    });

    it('finds nothing for a buildable the map does not have', () => {
      expect(locate(world, 'unknown')).toBeNull();
    });
  });
});
