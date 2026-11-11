import { CurvePointDocument } from '../../api/models/curve-point-document';
import { FootprintDocument } from '../../api/models/footprint-document';
import { VolumeDocument } from '../../api/models/volume-document';
import { Area, areaAround, areaBetween, buildingBoxes, curveInArea, inArea, sampleCurve3d, toScene, volumeCorners } from './area-model';

const volume = (patch: Partial<VolumeDocument> = {}): VolumeDocument => ({
  minX: -400, minY: -500, minZ: 0, maxX: 400, maxY: 500, maxZ: 1000,
  rotationX: 0, rotationY: 0, rotationZ: 0, rotationW: 1,
  offsetX: 0, offsetY: 0, offsetZ: 0,
  ...patch,
});

const round = (points: { x: number; y: number; z: number }[]) => points.map((p) => [Math.round(p.x), Math.round(p.y), Math.round(p.z)]);

describe('area model', () => {
  describe('the area of a rectangle drawn on the map', () => {
    it('is the same whichever corner the drawing starts from', () => {
      expect(areaBetween({ x: 500, y: -200, z: 0 }, { x: -100, y: 300, z: 0 })).toEqual({ x0: -100, y0: -200, x1: 500, y1: 300 });
    });

    const area: Area = { x0: 0, y0: 0, x1: 1000, y1: 1000 };

    it('holds what stands inside it, at any height', () => {
      expect([inArea({ x: 500, y: 500, z: 99999 }, area), inArea({ x: 1500, y: 500, z: 0 }, area)]).toEqual([true, false]);
    });

    it('holds a belt when any of its points lies inside', () => {
      const point = (x: number): CurvePointDocument => ({ location: { x, y: 500, z: 0 }, arriveTangent: { x: 1, y: 0, z: 0 }, leaveTangent: { x: 1, y: 0, z: 0 } });

      expect([curveInArea([point(-500), point(500)], area), curveInArea([point(-900), point(-500)], area)]).toEqual([true, false]);
    });
  });

  describe("the area around a factory's machines", () => {
    it('reaches a margin past the outermost ones on every side', () => {
      expect(areaAround([{ x: 0, y: 500, z: 0 }, { x: 1000, y: -500, z: 800 }], 100)).toEqual({ x0: -100, y0: -600, x1: 1100, y1: 600 });
    });

    it('is nothing when none of its machines are in the snapshot', () => {
      expect(areaAround([], 100)).toBeNull();
    });
  });

  describe("a building's boxes in 3D", () => {
    it('takes a box as the game file has it when it is neither turned nor moved', () => {
      const corners = volumeCorners(volume());

      expect(round([corners[0], corners[7]])).toEqual([[-400, -500, 0], [400, 500, 1000]]);
    });

    it('turns a box by its quaternion, then moves it by its offset', () => {
      // A quarter turn around the vertical axis: x becomes y. Then 1 m up.
      const quarter = Math.SQRT1_2;
      const corners = volumeCorners(volume({ rotationZ: quarter, rotationW: quarter, offsetZ: 100 }));

      expect(round([corners[0]])).toEqual([[500, -400, 100]]);
    });

    const footprint = (volumes: VolumeDocument[] | null): FootprintDocument => ({ id: 'Build_ConstructorMk1_C', minX: -400, minY: -500, maxX: 400, maxY: 500, volumes });

    it('turns a building by its yaw and places it where it stands', () => {
      const [box] = buildingBoxes({ className: 'Build_ConstructorMk1_C', position: { x: 10000, y: 0, z: 500 }, yaw: 90 }, new Map([['Build_ConstructorMk1_C', footprint([volume()])]]));

      // A quarter turn: the box's own corner (-400, -500) lands at (+500, -400) from where it stands.
      expect(round([box[0]])).toEqual([[10500, -400, 500]]);
    });

    it('raises the flat footprint to 4 metres for a snapshot without volumes', () => {
      const [box] = buildingBoxes({ className: 'Build_ConstructorMk1_C', position: { x: 0, y: 0, z: 0 }, yaw: 0 }, new Map([['Build_ConstructorMk1_C', footprint(null)]]));

      expect(round([box[0], box[7]])).toEqual([[-400, -500, 0], [400, 500, 400]]);
    });

    it('draws a 2 metre cube for a building without a footprint, such as a lift', () => {
      const [box] = buildingBoxes({ className: 'Build_ConveyorLiftMk1_C', position: { x: 0, y: 0, z: 0 }, yaw: 0 }, new Map());

      expect(round([box[0], box[7]])).toEqual([[-100, -100, 0], [100, 100, 200]]);
    });
  });

  it('puts game positions in a scene in metres, height up, around the middle of the area', () => {
    // The game: x east, y south, z up (centimetres). The scene: x east, y up, z south (metres).
    expect(toScene({ x: 1500, y: 2500, z: 300 }, { x: 1000, y: 2000 })).toEqual([5, 3, 5]);
  });

  it('samples a belt along its curve, heights included', () => {
    const points = sampleCurve3d([
      { location: { x: 0, y: 0, z: 0 }, arriveTangent: { x: 0, y: 0, z: 0 }, leaveTangent: { x: 1000, y: 0, z: 1000 } },
      { location: { x: 1000, y: 0, z: 1000 }, arriveTangent: { x: 1000, y: 0, z: 1000 }, leaveTangent: { x: 0, y: 0, z: 0 } },
    ]);

    expect(round([points[0], points[points.length - 1]])).toEqual([[0, 0, 0], [1000, 0, 1000]]);
  });
});
