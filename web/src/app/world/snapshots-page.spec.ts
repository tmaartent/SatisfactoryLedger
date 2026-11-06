import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { Api } from '../api/api';
import { deleteSnapshot } from '../api/fn/world/delete-snapshot';
import { getSnapshots } from '../api/fn/world/get-snapshots';
import { SnapshotSummaryResponse } from '../api/models/snapshot-summary-response';
import { SnapshotsPage } from './snapshots-page';

const development: SnapshotSummaryResponse = {
  id: 'snapshot-1',
  sessionName: 'Development',
  gameBuild: 517554,
  savedAt: '2026-11-06T08:07:28Z',
  importedAt: '2026-11-06T10:45:24Z',
  counts: { machines: 5, providers: 7, consumers: 8, logistics: 58, connections: 71 },
  analysis: 'ready',
};

describe('SnapshotsPage', () => {
  async function render(confirmed: boolean) {
    const api = { invoke: vi.fn((fn: unknown) => Promise.resolve(fn === getSnapshots ? [development] : undefined)) };
    TestBed.configureTestingModule({
      imports: [SnapshotsPage],
      providers: [
        provideRouter([]),
        { provide: Api, useValue: api },
        // The confirmation: what the player answers.
        { provide: MatDialog, useValue: { open: () => ({ afterClosed: () => of(confirmed) }) } },
      ],
    });
    const fixture = TestBed.createComponent(SnapshotsPage);
    await fixture.whenStable();
    return { fixture, api };
  }

  const deleteButton = (element: HTMLElement) =>
    element.querySelector('button[aria-label="Delete the snapshot of Development"]') as HTMLButtonElement;

  it('deletes a snapshot once the player confirms', async () => {
    const { fixture, api } = await render(true);

    deleteButton(fixture.nativeElement).click();
    await fixture.whenStable();

    expect(api.invoke).toHaveBeenCalledWith(deleteSnapshot, { id: 'snapshot-1' });
  });

  it('keeps the snapshot when the player cancels', async () => {
    const { fixture, api } = await render(false);

    deleteButton(fixture.nativeElement).click();
    await fixture.whenStable();

    expect(api.invoke).not.toHaveBeenCalledWith(deleteSnapshot, expect.anything());
  });
});
