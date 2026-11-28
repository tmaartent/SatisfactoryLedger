import { MachineFinding } from '../../api/models/machine-finding';
import { MapProvider } from '../../api/models/map-provider';
import { CurvePointDocument } from '../../api/models/curve-point-document';
import { FootprintDocument } from '../../api/models/footprint-document';
import { WorldMapResponse } from '../../api/models/world-map-response';
import {
  MapMargin,
  buildingStyle,
  buildingsShown,
  iconTag,
  nodeTakers,
  nodeMarkerHtml,
  nodeMarkerSize,
  contentEntries,
  describeContents,
  dragColours,
  describeHeadLift,
  describeDronePort,
  describePipeFlow,
  mapArtExtent,
  mapExtent,
  mapArtTiles,
  escapeHtml,
  footprintShapes,
  fromMapPoint,
  locate,
  machineStatuses,
  sampleCurve,
  toMapPoint,
} from './map-model';

describe('map model', () => {
  describe('a building in its factory', () => {
    it("is edged in its factory's colour over the fill of how it runs", () => {
      expect(buildingStyle('#66bb6a', '#ff4081', null)).toEqual({
        color: '#ff4081',
        weight: 2,
        fillColor: '#66bb6a',
        fillOpacity: 0.75,
        opacity: 1,
      });
    });

    it('is edged boldly when its factory is the one highlighted', () => {
      expect(buildingStyle('#66bb6a', '#ff4081', true)).toMatchObject({ weight: 3.5, opacity: 1 });
    });

    it('is dimmed when another factory is highlighted', () => {
      expect(buildingStyle('#66bb6a', '#ff4081', false)).toMatchObject({
        weight: 2,
        fillOpacity: 0.15,
        opacity: 0.15,
      });
    });

    it('of no factory is edged in its own fill', () => {
      expect(buildingStyle('#66bb6a', undefined, null)).toMatchObject({
        color: '#66bb6a',
        weight: 1,
      });
    });
  });

  it('shows a name in a tooltip as written, not as markup', () => {
    expect(escapeHtml('Plates & <Rods> "east"')).toBe('Plates &amp; &lt;Rods&gt; &quot;east&quot;');
  });

  it('turns a point on the map back into a game position, the opposite of placing it', () => {
    expect(fromMapPoint(500, 123.45)).toEqual({ x: 12345, y: -50000, z: 0 });
  });

  it('places a position in metres, with north up', () => {
    // The game's y grows southwards; the map's latitude grows upwards.
    expect(toMapPoint({ x: 12345, y: -50000, z: 900 })).toEqual([500, 123.45]);
  });

  it('knows how each machine runs once the analysis is ready', () => {
    const findings: MachineFinding[] = [
      {
        id: 'smelter',
        className: 'Build_SmelterMk1_C',
        recipe: null,
        position: null,
        limit: 'starved',
        limitingItem: null,
        utilisation: 0.5,
        approximate: false,
        blockedOn: {},
        starvedOf: {},
      },
    ];

    expect(machineStatuses(findings).get('smelter')).toBe('starved');
  });

  it("calls a machine's status unknown while there are no findings", () => {
    expect(machineStatuses(undefined).get('smelter') ?? 'unknown').toBe('unknown');
  });

  it('knows which provider stands on which resource node; a node none stands on is free', () => {
    const provider = (id: string, node: string | null): MapProvider => ({
      id,
      node,
      className: 'Build_MinerMk1_C',
      position: { x: 0, y: 0, z: 0 },
      purity: null,
      resource: null,
      yaw: 0,
    });
    const miner = provider('miner', 'BP_ResourceNode35');
    const takers = nodeTakers([miner, provider('well', null)]);

    expect([takers.get('BP_ResourceNode35'), takers.get('BP_ResourceNode36'), takers.size]).toEqual(
      [miner, undefined, 1],
    );
  });

  const point = (
    x: number,
    y: number,
    arrive: [number, number],
    leave: [number, number],
  ): CurvePointDocument => ({
    location: { x, y, z: 0 },
    arriveTangent: { x: arrive[0], y: arrive[1], z: 0 },
    leaveTangent: { x: leave[0], y: leave[1], z: 0 },
  });

  it('draws a straight belt as a line between its two ends', () => {
    const straight = [point(0, 0, [-1, 0], [-450, 0]), point(-900, 0, [-450, 0], [-1, 0])];

    expect(sampleCurve(straight)).toEqual([
      toMapPoint({ x: 0, y: 0, z: 0 }),
      toMapPoint({ x: -900, y: 0, z: 0 }),
    ]);
  });

  it('follows a bend through the middle of its curve, not the straight line', () => {
    // Leaves along +x, arrives along +y: halfway along the Hermite curve it's at (62.5, 37.5) cm.
    const bend = [point(0, 0, [100, 0], [100, 0]), point(100, 100, [0, 100], [0, 100])];

    const line = sampleCurve(bend);

    expect(line[0]).toEqual(toMapPoint({ x: 0, y: 0, z: 0 }));
    expect(line[line.length - 1]).toEqual(toMapPoint({ x: 100, y: 100, z: 0 }));
    expect(line).toContainEqual(toMapPoint({ x: 62.5, y: 37.5, z: 0 }));
  });

  const constructor: FootprintDocument = {
    id: 'Build_ConstructorMk1_C',
    minX: -400,
    minY: -500,
    maxX: 400,
    maxY: 500,
  };
  const origin = { x: 0, y: 0, z: 0 };
  const shapes = (
    position: { x: number; y: number; z: number },
    yaw: number,
    footprint: FootprintDocument,
  ) =>
    footprintShapes(
      { className: footprint.id, position, yaw },
      new Map([[footprint.id, footprint]]),
    );
  // The extent of a building's shapes on the map, in metres: [south, north] and [west, east].
  const extent = (rings: [number, number][][]) => extentOf(rings.flat());
  const extentOf = (corners: [number, number][]) => ({
    lat: [Math.min(...corners.map((c) => c[0])), Math.max(...corners.map((c) => c[0]))].map(
      (v) => Math.round(v * 100) / 100,
    ),
    lng: [Math.min(...corners.map((c) => c[1])), Math.max(...corners.map((c) => c[1]))].map(
      (v) => Math.round(v * 100) / 100,
    ),
  });

  it("draws an unturned constructor's footprint 8 m along x and 10 m along y", () => {
    expect(extent(shapes(origin, 0, constructor))).toEqual({ lat: [-5, 5], lng: [-4, 4] });
  });

  it('turns a footprint with its building', () => {
    // A quarter turn: its 10 m now run along x, its 8 m along y.
    expect(extent(shapes(origin, 90, constructor))).toEqual({ lat: [-4, 4], lng: [-5, 5] });
  });

  it('keeps a footprint that sticks out to one side where it is', () => {
    // A miner covers y −5 to 9 m in its own coordinates; the game's y grows southwards, the map's latitude upwards.
    const miner: FootprintDocument = {
      id: 'Build_MinerMk1_C',
      minX: -300,
      minY: -500,
      maxX: 300,
      maxY: 900,
    };

    expect(extent(shapes({ x: 1000, y: 0, z: 0 }, 0, miner))).toEqual({
      lat: [-9, 5],
      lng: [7, 13],
    });
  });

  it("draws each of a building's boxes as seen from above, not the rectangle around them", () => {
    // A cross of two boxes: 20 by 4 metres along x, and along y.
    const box = {
      minZ: 0,
      maxZ: 400,
      rotationX: 0,
      rotationY: 0,
      rotationZ: 0,
      rotationW: 1,
      offsetX: 0,
      offsetY: 0,
      offsetZ: 0,
    };
    const cross: FootprintDocument = {
      id: 'Build_Cross_C',
      minX: -1000,
      minY: -1000,
      maxX: 1000,
      maxY: 1000,
      volumes: [
        { ...box, minX: -1000, maxX: 1000, minY: -200, maxY: 200 },
        { ...box, minX: -200, maxX: 200, minY: -1000, maxY: 1000 },
      ],
    };

    expect(shapes(origin, 0, cross).map(extentOf)).toEqual([
      { lat: [-2, 2], lng: [-10, 10] },
      { lat: [-10, 10], lng: [-2, 2] },
    ]);
  });

  it('draws a box tilted up as the ground it covers', () => {
    // 10 m long, 2 m wide and 2 m high, tilted a quarter around its y axis: from above it's 2 by 2 metres.
    const tilted: FootprintDocument = {
      id: 'Build_Tilted_C',
      minX: -500,
      minY: -100,
      maxX: 500,
      maxY: 100,
      volumes: [
        {
          minX: -500,
          maxX: 500,
          minY: -100,
          maxY: 100,
          minZ: -100,
          maxZ: 100,
          rotationX: 0,
          rotationY: Math.SQRT1_2,
          rotationZ: 0,
          rotationW: Math.SQRT1_2,
          offsetX: 0,
          offsetY: 0,
          offsetZ: 0,
        },
      ],
    };

    expect(extent(shapes(origin, 0, tilted))).toEqual({ lat: [-1, 1], lng: [-1, 1] });
  });

  const name = (id: string) => id.replace('Desc_', '').replace('_C', '');

  it('describes what a belt carries, most first', () => {
    expect(describeContents({ Desc_IronIngot_C: 2, Desc_OreIron_C: 7 }, name)).toBe(
      'OreIron × 7, IronIngot × 2',
    );
  });

  it('describes what flows through a pipe in the model, against what it carries at most', () => {
    expect(
      describePipeFlow(
        { id: 'pipe', perMinute: { Desc_Water_C: 60 }, ofNetwork: false, capacity: 300 },
        name,
      ),
    ).toBe('Water 60 of 300 m³/min');
  });

  describe('a drone port in the model', () => {
    const port = {
      id: 'port',
      className: 'Build_DroneStation_C',
      fuel: 'Desc_PackagedRocketFuel_C',
      position: null,
      approximate: false,
    };

    it('says what its drones burn and what its belts bring', () => {
      expect(
        describeDronePort({ ...port, burns: 1.944, supplied: 1.5, noDroneRefuels: false }, name),
      ).toBe('Drones burn PackagedRocketFuel 1.944/min · gets 1.5');
    });

    it('says so when no drone refuels there, whatever is in its slot', () => {
      expect(
        describeDronePort({ ...port, burns: null, supplied: 0, noDroneRefuels: true }, name),
      ).toBe('No drone refuels here: its PackagedRocketFuel burns nothing');
    });
  });

  it('describes what the network of a pipe inside it takes in, as the model takes a network as one', () => {
    expect(
      describePipeFlow(
        { id: 'pipe', perMinute: { Desc_Water_C: 300 }, ofNetwork: true, capacity: 600 },
        name,
      ),
    ).toBe('its network takes in Water 300 m³/min');
  });

  it('says how much head lift a pipe has to spare (BR-A22)', () => {
    expect(describeHeadLift(5.7)).toBe('5.7 m to spare');
  });

  it('says how far a pipe climbs above its head lift (BR-A22)', () => {
    expect(describeHeadLift(-4.3)).toBe('4.3 m short: fluid may not get across');
  });

  it('draws the rectangle for an arrangement blue, and the one for 3D yellow, so it says which page opens (asked 2026-11-24)', () => {
    expect(dragColours).toEqual({ '3d': '#ffeb3b', arrangement: '#448aff' });
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

  it('lists what something holds item by item, most first and rounded, so each can be shown with its icon', () => {
    expect(contentEntries({ Desc_Water_C: 12.3456, Desc_OreBauxite_C: 100 })).toEqual([
      { item: 'Desc_OreBauxite_C', count: 100 },
      { item: 'Desc_Water_C', count: 12.35 },
    ]);
  });

  describe('finding a buildable to zoom to', () => {
    const placed = (id: string, x: number, y: number) => ({
      id,
      className: 'X',
      position: { x, y, z: 0 },
      yaw: 0,
    });
    const building = (id: string, x: number, y: number) => ({
      ...placed(id, x, y),
      contents: null,
      isGenerator: false,
    });
    const world: WorldMapResponse = {
      machines: [
        {
          ...placed('smelter', 10000, 20000),
          recipe: null,
          inputContents: null,
          outputContents: null,
        },
      ],
      providers: [{ ...placed('miner', 30000, 0), node: null, resource: null, purity: null }],
      logistics: [building('splitter', 0, 0)],
      consumers: [building('drone port', -50000, 0)],
      conveyors: [
        {
          id: 'belt',
          className: 'Build_ConveyorBeltMk1_C',
          kind: 'belt',
          contents: null,
          looseEnds: 0,
          curve: [point(0, 0, [1000, 0], [1000, 0]), point(1000, 0, [1000, 0], [1000, 0])],
        },
      ],
      footprints: [],
    };

    it('finds a machine where it stands', () => {
      expect(locate(world, 'smelter')).toEqual([-200, 100]);
    });

    it('finds providers, logistics and consumers too', () => {
      expect([
        locate(world, 'miner'),
        locate(world, 'splitter'),
        locate(world, 'drone port'),
      ]).toEqual([
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

  describe("the game's map picture under the map", () => {
    const art = {
      gameVersion: '1.2',
      x0: -1000,
      y0: -500,
      x1: 1000,
      y1: 500,
      tiles: [
        {
          column: 1,
          row: 0,
          x0: 0,
          y0: -500,
          x1: 1000,
          y1: 500,
          url: '/api/v1/catalog/map-art/tiles/1/0',
        },
      ],
    };

    it('lays each tile over its part of the world, in metres with north up', () => {
      expect(mapArtTiles(art)).toEqual([
        {
          url: '/api/v1/catalog/map-art/tiles/1/0',
          bounds: [
            [5, 0],
            [-5, 10],
          ],
        },
      ]);
    });

    it('lays nothing without map art', () => {
      expect(mapArtTiles(null)).toEqual([]);
    });

    it('covers the rectangle of the world it shows, as the corners the map can be held to', () => {
      expect(mapArtExtent(art)).toEqual([
        [5, -10],
        [-5, 10],
      ]);
    });

    it('holds the map to nothing without map art', () => {
      expect(mapArtExtent(null)).toBeNull();
    });

    describe('with buildings past its edges, as the game allows', () => {
      const building = (x: number, y: number): MapProvider => ({
        id: `b${x}`,
        className: 'Build_MinerMk1_C',
        position: { x, y, z: 0 },
        yaw: 0,
        node: null,
        resource: null,
        purity: null,
      });
      const off = (
        buildings: MapProvider[],
        conveyors: CurvePointDocument[][] = [],
      ): WorldMapResponse => ({
        machines: [],
        providers: buildings,
        logistics: [],
        consumers: [],
        conveyors: conveyors.map((curve, i) => ({
          id: `c${i}`,
          className: 'Build_ConveyorBeltMk1_C',
          kind: 'belt',
          contents: null,
          looseEnds: 0,
          curve,
        })),
        footprints: [],
      });

      it('reaches as far as the buildings, and a margin past them', () => {
        expect(mapExtent(art, off([building(3000, 0)]))).toEqual([
          [-5, -10],
          [5, 30 + MapMargin],
        ]);
      });

      it('reaches as far as the belts and pipes too', () => {
        expect(
          mapExtent(art, off([], [[point(0, 0, [0, 0], [0, 0]), point(0, 4000, [0, 0], [0, 0])]])),
        ).toEqual([
          [-40 - MapMargin, -10],
          [5, 10],
        ]);
      });

      it('stays the map picture when every building is on it', () => {
        expect(mapExtent(art, off([building(500, -200)]))).toEqual([
          [-5, -10],
          [5, 10],
        ]);
      });

      it('is nothing without map art', () => {
        expect(mapExtent(null, off([building(3000, 0)]))).toBeNull();
      });
    });
  });

  describe('the buildings of a status, picked in the overview', () => {
    const analysis = {
      machines: [
        { id: 'a', limit: 'noRecipe' as const },
        { id: 'b', limit: 'starved' as const },
        { id: 'c', limit: 'noRecipe' as const },
      ],
      generators: [{ id: 'g', limit: 'starved' as const }],
    };

    it('are the machines of that status', () => {
      expect(buildingsShown(analysis, { of: 'machines', limit: 'noRecipe' })).toEqual(
        new Set(['a', 'c']),
      );
    });

    it('are the generators of that status, not its machines', () => {
      expect(buildingsShown(analysis, { of: 'generators', limit: 'starved' })).toEqual(
        new Set(['g']),
      );
    });

    it('are none picked when no status is', () => {
      expect(buildingsShown(analysis, null)).toBeNull();
    });

    it('are the bottlenecks, belts and pipes, when those are picked', () => {
      expect(
        buildingsShown({ ...analysis, bottlenecks: [{ id: 'belt-1' }] }, { of: 'bottlenecks' }),
      ).toEqual(new Set(['belt-1']));
    });
  });

  describe("the game's icons on the map", () => {
    it('puts an icon before a name in a tooltip', () => {
      expect(iconTag('/api/v1/catalog/icons/Desc_OreIron_C')).toBe(
        '<img class="tooltip-icon" src="/api/v1/catalog/icons/Desc_OreIron_C" width="16" height="16" alt="">',
      );
    });

    it('puts nothing in a tooltip for what has no icon', () => {
      expect(iconTag(null)).toBe('');
    });

    it("draws a node as a pin: its resource's icon on its purity's colour", () => {
      expect(nodeMarkerHtml('/api/v1/catalog/icons/Desc_OreIron_C', '#fff176')).toBe(
        '<div class="node-pin" style="--purity: #fff176"><div class="node-marker"><img src="/api/v1/catalog/icons/Desc_OreIron_C" alt=""></div></div>',
      );
    });

    it('grows as the map is zoomed in, half as fast as the map: a step in is about 1.4 times as big', () => {
      expect([nodeMarkerSize(-2), nodeMarkerSize(-1), nodeMarkerSize(0)]).toEqual([22, 31, 44]);
    });

    it('stays big enough to make out when zoomed all the way out, and no bigger than a thumb when zoomed all the way in', () => {
      expect([nodeMarkerSize(-4), nodeMarkerSize(3)]).toEqual([18, 72]);
    });
  });
});
