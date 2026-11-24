import { inAnyRing } from './ring-hit';

describe('whether a point lies on a building of several boxes', () => {
  // A machine as two boxes that overlap from x 5 to 10: its body and what sticks out of it.
  const body = [
    { x: 0, y: 0 },
    { x: 10, y: 0 },
    { x: 10, y: 10 },
    { x: 0, y: 10 },
  ];
  const side = [
    { x: 5, y: 2 },
    { x: 15, y: 2 },
    { x: 15, y: 8 },
    { x: 5, y: 8 },
  ];

  it('does where its boxes overlap, as in its middle', () => {
    expect(inAnyRing({ x: 7, y: 5 }, [body, side])).toBe(true);
  });

  it('does where only one of its boxes lies', () => {
    expect(inAnyRing({ x: 2, y: 5 }, [body, side])).toBe(true);
    expect(inAnyRing({ x: 13, y: 5 }, [body, side])).toBe(true);
  });

  it('does not beside it', () => {
    expect(inAnyRing({ x: 13, y: 9 }, [body, side])).toBe(false);
    expect(inAnyRing({ x: 20, y: 5 }, [body, side])).toBe(false);
  });
});
