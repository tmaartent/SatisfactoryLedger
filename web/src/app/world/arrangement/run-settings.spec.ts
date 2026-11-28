import { ArrangementSummary } from '../../api/models/arrangement-summary';
import {
  ceilingHeights,
  ceilingMetres,
  foundations,
  metres,
  runStatus,
  runSummary,
  saving,
  solverTime,
  solverTimeOf,
} from './run-settings';

describe("a run's settings, and the run as the list of runs says it", () => {
  describe('a run as the list of runs names it', () => {
    const run: ArrangementSummary = {
      id: 'run-1',
      snapshotId: 'snapshot-1',
      world: 'Learning',
      requestedAt: '2026-11-25T10:00:00Z',
      computedAt: '2026-11-25T10:01:00Z',
      status: 'ready',
      quality: 'optimal',
      error: null,
      area: null,
      exclusions: [],
      plotWidthMetres: 48,
      plotDepthMetres: 48,
      roadXMetres: 32,
      roadYMetres: 32,
      ceilingMetres: 64,
      supplyLines: false,
      footprints: false,
      seconds: 60,
      spotWeight: 1,
      together: 0,
      duration: null,
      costNow: 1000,
      cost: 660,
      beltMetres: 1234,
      levelsUsed: 2,
      unroutedBelts: 0,
      placements: 1,
      lastResortCrossings: 0,
    };

    it('says how it ended', () => {
      expect(runStatus(run)).toBe('Optimal');
      expect(runStatus({ ...run, quality: 'feasible' })).toBe('Best found in time');
      expect(runStatus({ ...run, quality: null })).toBe('No layout found: nothing placed');
      expect(runStatus({ ...run, status: 'pending' })).toBe('Computing…');
      expect(runStatus({ ...run, status: 'failed', error: 'no room' })).toBe('Failed: no room');
    });

    it('sums it up in one line: its plots and roads, the belt work it saves and how it ended', () => {
      expect(runSummary(run)).toBe('6 × 6 · roads 4 · 4 · −34% · Optimal');
    });

    it('says footprints instead of a plot size when its factories take their footprints', () => {
      expect(runSummary({ ...run, footprints: true })).toBe(
        'footprints · roads 4 · 4 · −34% · Optimal',
      );
    });

    it('says when it routes supply lines, and how many belts it left unrouted', () => {
      expect(runSummary({ ...run, supplyLines: true, unroutedBelts: 3 })).toBe(
        '6 × 6 · roads 4 · 4 · supply lines · −34% · 3 not routed · Optimal',
      );
    });

    it('says when it took more than one placement to leave the fewest belts unrouted', () => {
      // Step 101: placed again when belts were left over, keeping the best.
      expect(runSummary({ ...run, unroutedBelts: 1, placements: 3 })).toBe(
        '6 × 6 · roads 4 · 4 · −34% · 1 not routed · best of 3 placements · Optimal',
      );
    });

    it('says how many last-resort crossings its belts make', () => {
      // Step 102: where a belt couldn't get through otherwise.
      expect(runSummary({ ...run, lastResortCrossings: 2 })).toBe(
        '6 × 6 · roads 4 · 4 · −34% · 2 crossings · Optimal',
      );
      expect(runSummary({ ...run, lastResortCrossings: 1 })).toBe(
        '6 × 6 · roads 4 · 4 · −34% · 1 crossing · Optimal',
      );
      expect(runSummary({ ...run, lastResortCrossings: null })).toBe(
        '6 × 6 · roads 4 · 4 · −34% · Optimal',
      );
    });

    it('leaves out the saving while it is computed', () => {
      expect(runSummary({ ...run, status: 'pending', cost: null, costNow: null })).toBe(
        '6 × 6 · roads 4 · 4 · Computing…',
      );
    });
  });

  it('gives plots and roads in foundations of 8 metres', () => {
    expect(metres(6)).toBe(48);
  });

  it('shows a size in metres as foundations', () => {
    expect(foundations(32)).toBe(4);
  });

  it('says the belt work saved as a share of what it is now', () => {
    expect(saving(22016, 14580)).toBe('−34%');
  });

  it('says nothing is saved when nothing was to save', () => {
    expect(saving(0, 0)).toBe('—');
  });

  it('gives the ceiling of the belt floor in foundation heights of 4 metres', () => {
    expect([ceilingMetres(8), ceilingHeights(32)]).toEqual([32, 8]);
  });

  it('gives the solver a quarter of a minute, a minute or ten minutes', () => {
    expect([solverTime('fast'), solverTime('balanced'), solverTime('thorough')]).toEqual([
      15, 60, 600,
    ]);
  });

  it("takes a run's own solver time as the longest of the three it reaches", () => {
    expect([solverTimeOf(30), solverTimeOf(60), solverTimeOf(300), solverTimeOf(600)]).toEqual([
      'fast',
      'balanced',
      'balanced',
      'thorough',
    ]);
  });
});
