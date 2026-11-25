import { TestBed } from '@angular/core/testing';

import { ArrangementSummary } from '../../api/models/arrangement-summary';
import { RunPicker } from './run-picker';

const older: ArrangementSummary = {
  id: 'run-1',
  snapshotId: 'snapshot-1',
  world: 'Learning',
  requestedAt: '2026-11-24T09:30:00',
  computedAt: '2026-11-24T09:31:00',
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
  seconds: 60,
  spotWeight: 1,
  together: 0,
  duration: null,
  costNow: 1000,
  cost: 660,
  beltMetres: 1234,
  levelsUsed: 2,
  unroutedBelts: 0,
};

const newer: ArrangementSummary = {
  ...older,
  id: 'run-2',
  requestedAt: '2026-11-25T14:05:00',
  status: 'pending',
  quality: null,
  cost: null,
  costNow: null,
};

describe('the list of runs above the arrangement', () => {
  async function render(runs: ArrangementSummary[], selected: string | null) {
    const fixture = TestBed.createComponent(RunPicker);
    fixture.componentRef.setInput('runs', runs);
    fixture.componentRef.setInput('selected', selected);
    const picked: string[] = [];
    const deleted: ArrangementSummary[] = [];
    fixture.componentInstance.picked.subscribe((id) => picked.push(id));
    fixture.componentInstance.deleted.subscribe((run) => deleted.push(run));
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const current = () => element.querySelector('button.current') as HTMLButtonElement;
    const openList = async () => {
      current().click();
      await fixture.whenStable();
    };
    return { fixture, element, current, openList, picked, deleted };
  }

  const menu = () => document.querySelector('.run-list') as HTMLElement;

  afterEach(() => document.querySelectorAll('.cdk-overlay-container').forEach((o) => o.remove()));

  it('names the run shown by when it was asked for and how it ended', async () => {
    const { current } = await render([newer, older], 'run-1');

    expect(current().textContent).toContain('24 Nov, 09:30');
    expect(current().textContent).toContain('Optimal');
  });

  it('says when there are no runs yet, and opens nothing', async () => {
    const { current } = await render([], null);

    expect(current().textContent).toContain('No runs yet');
    expect(current().disabled).toBe(true);
  });

  it('says how many earlier runs there are while a new area is drawn', async () => {
    const { current } = await render([newer, older], null);

    expect(current().textContent).toContain('2 earlier runs');
    expect(current().disabled).toBe(false);
  });

  it('lists every run, newest first, each summed up', async () => {
    const { openList } = await render([newer, older], 'run-1');
    await openList();

    const rows = [...menu().querySelectorAll('.run')].map((row) => row.textContent ?? '');
    expect(rows).toHaveLength(2);
    expect(rows[0]).toContain('25 Nov, 14:05');
    expect(rows[0]).toContain('Computing…');
    expect(rows[1]).toContain('6 × 6 · roads 4 · 4 · −34% · Optimal');
  });

  it('shows a run picked from the list', async () => {
    const { openList, picked } = await render([newer, older], 'run-1');
    await openList();

    (menu().querySelectorAll('.run')[0] as HTMLElement).click();

    expect(picked).toEqual(['run-2']);
  });

  it('deletes a run with the button beside it, without showing it', async () => {
    const { openList, picked, deleted } = await render([newer, older], 'run-1');
    await openList();

    (menu().querySelector('button[aria-label^="Delete the run of 24 Nov"]') as HTMLElement).click();

    expect(deleted.map((run) => run.id)).toEqual(['run-1']);
    expect(picked).toEqual([]);
  });
});
