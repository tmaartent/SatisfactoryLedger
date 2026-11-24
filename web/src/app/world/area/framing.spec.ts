import { framingDistance } from './framing';

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
