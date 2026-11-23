import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatTooltip } from '@angular/material/tooltip';
import { By } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { Api } from '../api/api';
import { deleteSnapshot } from '../api/fn/world/delete-snapshot';
import { getSnapshots } from '../api/fn/world/get-snapshots';
import { importSave } from '../api/fn/world/import-save';
import { SnapshotSummaryResponse } from '../api/models/snapshot-summary-response';
import { SnapshotPicker } from './snapshot-picker';

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

describe('the snapshot picker above the sidebar', () => {
  async function render(
    confirmed: boolean,
    current: string | null = 'snapshot-2',
    snapshots = [desert, development],
  ) {
    const api = {
      invoke: vi.fn((fn: unknown) =>
        Promise.resolve(
          fn === getSnapshots ? snapshots : fn === importSave ? { id: 'snapshot-3' } : undefined,
        ),
      ),
    };
    TestBed.configureTestingModule({
      imports: [SnapshotPicker],
      providers: [
        provideRouter([]),
        { provide: Api, useValue: api },
        // The confirmation: what the player answers.
        { provide: MatDialog, useValue: { open: () => ({ afterClosed: () => of(confirmed) }) } },
      ],
    });
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(SnapshotPicker);
    fixture.componentRef.setInput('current', current);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    /** Opens the list of snapshots: a menu, drawn in an overlay outside the picker. */
    const openList = async () => {
      (element.querySelector('button.current') as HTMLButtonElement).click();
      await fixture.whenStable();
    };
    /** The tooltip on what the selector finds: a tooltip's text is only drawn on hover. */
    const tooltip = (selector: string) =>
      fixture.debugElement.query(By.css(selector)).injector.get(MatTooltip).message;
    return { fixture, api, navigate, element, openList, tooltip };
  }

  const menu = () => document.querySelector('.snapshot-list') as HTMLElement;
  const deleteButton = (name: string) =>
    menu().querySelector(
      `button[aria-label="Delete the snapshot of ${name}"]`,
    ) as HTMLButtonElement;

  afterEach(() => document.querySelectorAll('.cdk-overlay-container').forEach((o) => o.remove()));

  it('names the snapshot shown', async () => {
    const { element } = await render(true);

    expect(element.querySelector('button.current')?.textContent).toContain('Desert Start');
  });

  it('asks to pick one when none is shown', async () => {
    const { element } = await render(true, null);

    expect(element.querySelector('button.current')?.textContent).toContain('Pick a snapshot');
  });

  it('lists each snapshot by its world name, linking to its map, the one shown marked', async () => {
    const { openList } = await render(true);

    await openList();

    const links = [...menu().querySelectorAll('a.snapshot')];
    expect(links.map((link) => link.textContent?.trim())).toEqual(['Desert Start', 'Development']);
    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      '/snapshots/snapshot-2/map',
      '/snapshots/snapshot-1/map',
    ]);
    expect(menu().querySelector('a.snapshot.current')?.textContent?.trim()).toBe('Desert Start');
  });

  it('deletes a snapshot once the player confirms, staying on the map shown', async () => {
    const { fixture, api, navigate, openList } = await render(true);

    await openList();
    deleteButton('Development').click();
    await fixture.whenStable();

    expect(api.invoke).toHaveBeenCalledWith(deleteSnapshot, { id: 'snapshot-1' });
    expect(navigate).not.toHaveBeenCalled();
  });

  it('keeps the snapshot when the player cancels', async () => {
    const { fixture, api, openList } = await render(false);

    await openList();
    deleteButton('Development').click();
    await fixture.whenStable();

    expect(api.invoke).not.toHaveBeenCalledWith(deleteSnapshot, expect.anything());
  });

  it('goes back to the map with no snapshot loaded once the snapshot shown is deleted', async () => {
    const { fixture, navigate, openList } = await render(true);

    await openList();
    deleteButton('Desert Start').click();
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith(['/snapshots']);
  });

  it('says what the import is for, and where the game keeps its saves', async () => {
    const { element, tooltip } = await render(true);

    expect(element.querySelector('button.import')?.getAttribute('aria-label')).toBe(
      'Import a save',
    );
    expect(tooltip('button.import')).toContain('new snapshot');
    expect(tooltip('button.import')).toContain('SaveGames');
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

  it('says there are no snapshots yet, and how to make one, before any is imported', async () => {
    const { element, tooltip } = await render(true, null, []);

    expect((element.querySelector('button.current') as HTMLButtonElement).disabled).toBe(true);
    // On what holds the button: a disabled button takes no pointer, so shows no tooltip of its own.
    expect(tooltip('.current-holder')).toContain('No snapshots yet');
    expect(tooltip('.current-holder')).toContain('import');
  });
});
