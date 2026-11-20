import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { Api } from '../api/api';
import { deleteSnapshot } from '../api/fn/world/delete-snapshot';
import { getSnapshots } from '../api/fn/world/get-snapshots';
import { importSave } from '../api/fn/world/import-save';
import { SnapshotSummaryResponse } from '../api/models/snapshot-summary-response';
import { SnapshotsSidebar } from './snapshots-sidebar';

const development: SnapshotSummaryResponse = {
  id: 'snapshot-1',
  sessionName: 'Development',
  gameBuild: 517554,
  savedAt: '2026-11-06T08:07:28Z',
  importedAt: '2026-11-06T10:45:24Z',
  counts: { machines: 5, providers: 7, consumers: 8, logistics: 58, connections: 71 },
  analysis: 'ready',
};

const desert: SnapshotSummaryResponse = {
  ...development,
  id: 'snapshot-2',
  sessionName: 'Desert Start',
};

describe('the snapshots beside the map', () => {
  async function render(confirmed: boolean, current = 'snapshot-2') {
    const api = {
      invoke: vi.fn((fn: unknown) =>
        Promise.resolve(
          fn === getSnapshots
            ? [desert, development]
            : fn === importSave
              ? { id: 'snapshot-3' }
              : undefined,
        ),
      ),
    };
    TestBed.configureTestingModule({
      imports: [SnapshotsSidebar],
      providers: [
        provideRouter([]),
        { provide: Api, useValue: api },
        // The confirmation: what the player answers.
        { provide: MatDialog, useValue: { open: () => ({ afterClosed: () => of(confirmed) }) } },
      ],
    });
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(SnapshotsSidebar);
    fixture.componentRef.setInput('current', current);
    await fixture.whenStable();
    return { fixture, api, navigate, element: fixture.nativeElement as HTMLElement };
  }

  const deleteButton = (element: HTMLElement, name: string) =>
    element.querySelector(
      `button[aria-label="Delete the snapshot of ${name}"]`,
    ) as HTMLButtonElement;

  it('lists each snapshot by its world name, linking to its map', async () => {
    const { element } = await render(true);

    const links = [...element.querySelectorAll('a.snapshot')];
    expect(links.map((link) => link.textContent?.trim())).toEqual(['Desert Start', 'Development']);
    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      '/snapshots/snapshot-2/map',
      '/snapshots/snapshot-1/map',
    ]);
  });

  it('marks the snapshot whose map is shown', async () => {
    const { element } = await render(true);

    expect(element.querySelector('a.snapshot.current')?.textContent?.trim()).toBe('Desert Start');
  });

  it('deletes a snapshot once the player confirms', async () => {
    const { fixture, api, element } = await render(true);

    deleteButton(element, 'Development').click();
    await fixture.whenStable();

    expect(api.invoke).toHaveBeenCalledWith(deleteSnapshot, { id: 'snapshot-1' });
  });

  it('keeps the snapshot when the player cancels', async () => {
    const { fixture, api, element } = await render(false);

    deleteButton(element, 'Development').click();
    await fixture.whenStable();

    expect(api.invoke).not.toHaveBeenCalledWith(deleteSnapshot, expect.anything());
  });

  it('goes back to the map with no snapshot loaded once the snapshot shown is deleted', async () => {
    const { fixture, navigate, element } = await render(true);

    deleteButton(element, 'Desert Start').click();
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith(['/snapshots']);
  });

  it('stays on the map shown when another snapshot is deleted', async () => {
    const { fixture, navigate, element } = await render(true);

    deleteButton(element, 'Development').click();
    await fixture.whenStable();

    expect(navigate).not.toHaveBeenCalled();
  });

  it('opens the map of a save once it is imported', async () => {
    const { fixture, api, navigate, element } = await render(true);
    const input = element.querySelector('input[type="file"]') as HTMLInputElement;
    const file = new File(['save'], 'Desert Start.sav');
    Object.defineProperty(input, 'files', { value: { 0: file, length: 1, item: () => file } });

    input.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(api.invoke).toHaveBeenCalledWith(importSave, { body: { file } });
    expect(navigate).toHaveBeenCalledWith(['/snapshots', 'snapshot-3', 'map']);
  });
});
