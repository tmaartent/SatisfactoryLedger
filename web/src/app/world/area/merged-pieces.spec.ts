import { pieceAt, pieceStarts } from './merged-pieces';

describe('the pieces of a merged mesh', () => {
  // Three belts merged into one mesh: 10, 4 and 6 triangles.
  const starts = pieceStarts([10, 4, 6]);

  it('start one after the other, each where the one before ends', () => {
    expect(starts).toEqual([0, 10, 14]);
  });

  it('give a triangle to the piece it lies in', () => {
    expect([0, 9, 10, 13, 14, 19].map((triangle) => pieceAt(starts, triangle))).toEqual([
      0, 0, 1, 1, 2, 2,
    ]);
  });
});
