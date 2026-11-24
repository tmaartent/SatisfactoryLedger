import { CurvePointDocument } from '../../api/models/curve-point-document';
import { framingDistance, pointsToFrame } from './framing';

describe('how far back the camera stands to show a factory', () => {
  it('is a little more than its largest size on a screen as wide as high', () => {
    expect(framingDistance({ across: 100, high: 40 }, 1)).toBeCloseTo(130);
    expect(framingDistance({ across: 40, high: 100 }, 1)).toBeCloseTo(130);
  });

  it('is the same on a screen wider than high, which has room to the sides', () => {
    expect(framingDistance({ across: 100, high: 40 }, 16 / 9)).toBeCloseTo(130);
  });

  it('is further for a wide factory on a screen higher than wide, as a phone, by as much as it is narrower', () => {
    expect(framingDistance({ across: 100, high: 40 }, 0.5)).toBeCloseTo(260);
  });

  it('is no further for a tall factory on a screen higher than wide, which has room for its height', () => {
    expect(framingDistance({ across: 40, high: 100 }, 0.5)).toBeCloseTo(130);
  });
});

describe('what the camera frames in an area without buildings', () => {
  const point = (x: number, y: number, z = 0): CurvePointDocument => ({
    location: { x, y, z },
    arriveTangent: { x: 0, y: 0, z: 0 },
    leaveTangent: { x: 0, y: 0, z: 0 },
  });
  const area = { x0: 0, y0: 0, x1: 1000, y1: 1000 };

  it('is the belts and pipes, where they lie in the area', () => {
    const points = pointsToFrame([[point(100, 100, -1500), point(900, 100, -1500)]], area);

    expect(points.length).toBeGreaterThan(0);
    expect(points.every((p) => p.x >= 0 && p.x <= 1000 && p.z === -1500)).toBe(true);
  });

  it('leaves out where a belt runs on past the area', () => {
    const points = pointsToFrame([[point(500, 500), point(50_000, 500)]], area);

    expect(Math.max(...points.map((p) => p.x))).toBeLessThanOrEqual(1000);
  });

  it('is nothing when no belt or pipe crosses the area', () => {
    expect(pointsToFrame([[point(5000, 5000), point(6000, 5000)]], area)).toEqual([]);
  });
});
