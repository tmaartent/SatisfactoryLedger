/**
 * How far back the camera stands to show a factory `across` wide, as seen from a corner, and `high` tall, on a screen
 * `aspect` wide per unit of height. The camera's angle of view is set upright, so a screen higher than wide, as a
 * phone's, sees less to the sides: for the factory's width the camera stands as much further back as the screen is
 * narrower; its height fits as on a square screen (asked 2026-11-24).
 */
export function framingDistance(size: { across: number; high: number }, aspect: number): number {
  return Math.max(size.high, size.across / Math.min(1, aspect)) * 1.3;
}
