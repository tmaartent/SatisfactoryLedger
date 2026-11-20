// Many belts merged into one mesh keep drawing fast; a click then has to find which belt its triangle came from.

/** Where each piece of a merged mesh starts, in triangles: the pieces lie one after the other, in the order merged. */
export function pieceStarts(triangles: readonly number[]): number[] {
  const starts: number[] = [];
  let next = 0;
  for (const count of triangles) {
    starts.push(next);
    next += count;
  }
  return starts;
}

/** The piece a triangle of the merged mesh belongs to: the last one starting at or before it. */
export function pieceAt(starts: readonly number[], triangle: number): number {
  let [low, high] = [0, starts.length - 1];
  while (low < high) {
    const middle = Math.ceil((low + high) / 2);
    if (starts[middle] <= triangle) low = middle;
    else high = middle - 1;
  }
  return low;
}
