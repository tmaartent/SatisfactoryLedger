import { ArrangementSummary } from '../../api/models/arrangement-summary';

// A run's settings, in the units the player sets them (foundations, foundation heights, how long the solver looks),
// and a run as the list of runs says it: its settings, what it saves, how it ended. Not the arrangement itself.

/** A foundation is 8 by 8 metres: players lay out plots and roads in foundations. */
const foundationMetres = 8;

/** A size in foundations, in metres. */
export function metres(foundations: number): number {
  return foundations * foundationMetres;
}

/** A size in metres, in foundations. */
export function foundations(metres: number): number {
  return metres / foundationMetres;
}

/** A foundation is 4 metres high: the belt floor's ceiling is given in foundation heights. */
const foundationHeightMetres = 4;

/** A ceiling in foundation heights, in metres. */
export function ceilingMetres(heights: number): number {
  return heights * foundationHeightMetres;
}

/** A ceiling in metres, in foundation heights. */
export function ceilingHeights(metres: number): number {
  return metres / foundationHeightMetres;
}

/**
 * How long the solver may look for a better layout. It stops at the time, with the best layout it found then, unless it
 * proves one the best first.
 */
export type SolverTime = 'fast' | 'balanced' | 'thorough';

const solverSeconds: Record<SolverTime, number> = { fast: 15, balanced: 60, thorough: 600 };

/** The seconds the solver gets: a quarter of a minute to try settings, a minute, or ten (the most the server allows). */
export function solverTime(time: SolverTime): number {
  return solverSeconds[time];
}

/** A run's seconds as the longest of the three it reaches, for runs asked for in seconds. */
export function solverTimeOf(seconds: number): SolverTime {
  return seconds >= solverSeconds.thorough
    ? 'thorough'
    : seconds >= solverSeconds.balanced
      ? 'balanced'
      : 'fast';
}

/** The belt work saved, as a share of what it is now: "−34%". */
export function saving(costNow: number, cost: number): string {
  if (costNow <= 0) return '—';
  return `−${Math.round(((costNow - cost) / costNow) * 100)}%`;
}

/** How a run ended, or that it's still computed. */
export function runStatus(run: ArrangementSummary): string {
  if (run.status === 'pending') return 'Computing…';
  if (run.status === 'failed') return `Failed: ${run.error ?? ''}`;
  return run.quality === 'optimal'
    ? 'Optimal'
    : run.quality === 'feasible'
      ? 'Best found in time'
      : 'No layout found: nothing placed';
}

/**
 * A run in one line, as the list of runs shows it under when it was asked for (asked 2026-11-25): its plots, or
 * footprints when its factories take those, and roads in foundations, supply lines when it routes them, the belt work it saves, the belts it left unrouted, its last-resort
 * crossings (step 102), how many placements that took when more than one (step 101), how it ended.
 */
export function runSummary(run: ArrangementSummary): string {
  const size = (metres: number) => String(Math.round(foundations(metres) * 10) / 10);
  const parts = [
    run.footprints ? 'footprints' : `${size(run.plotWidthMetres)} × ${size(run.plotDepthMetres)}`,
    `roads ${size(run.roadXMetres)} · ${size(run.roadYMetres)}`,
  ];
  if (run.supplyLines) parts.push('supply lines');
  if (run.costNow !== null && run.cost !== null) parts.push(saving(run.costNow, run.cost));
  if (run.unroutedBelts) parts.push(`${run.unroutedBelts} not routed`);
  if (run.lastResortCrossings)
    parts.push(`${run.lastResortCrossings} crossing${run.lastResortCrossings === 1 ? '' : 's'}`);
  if ((run.placements ?? 1) > 1) parts.push(`best of ${run.placements} placements`);
  parts.push(runStatus(run));
  return parts.join(' · ');
}
