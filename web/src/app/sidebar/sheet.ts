/** How high the open tab stands on a phone, as a sheet over the map (asked 2026-11-24): half the screen, or all of it. */
export type SheetHeight = 'half' | 'full';

/** How far a finger moves on the sheet's handle before it counts as a swipe rather than a tap, in pixels. */
const swipe = 40;

/**
 * Where the sheet goes after its handle is swiped by this many pixels down (up when less than 0): up to full height,
 * down a step, closed below half height; a tap switches between half and full.
 */
export function afterSwipe(height: SheetHeight, down: number): SheetHeight | 'closed' {
  if (down <= -swipe) return 'full';
  if (down >= swipe) return height === 'full' ? 'half' : 'closed';
  return height === 'full' ? 'half' : 'full';
}
