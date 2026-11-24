/**
 * Whether a point lies in any of a building's rings, each a box of its footprint (asked 2026-11-24). Each ring by itself,
 * by ray casting: a point where two boxes overlap lies in both, which counts once, not twice. Leaflet's own test casts one
 * ray across all the rings together, so where two overlap, as in a machine's middle, the crossings cancel out.
 */
export function inAnyRing(
  point: { x: number; y: number },
  rings: readonly (readonly { x: number; y: number }[])[],
): boolean {
  return rings.some((ring) => {
    let inside = false;
    for (let j = 0, k = ring.length - 1; j < ring.length; k = j++) {
      const [a, b] = [ring[j], ring[k]];
      if (
        a.y > point.y !== b.y > point.y &&
        point.x < ((b.x - a.x) * (point.y - a.y)) / (b.y - a.y) + a.x
      )
        inside = !inside;
    }
    return inside;
  });
}
