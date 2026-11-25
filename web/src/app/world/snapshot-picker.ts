import { DatePipe } from '@angular/common';
import {
  Component,
  computed,
  effect,
  inject,
  input,
  resource,
  signal,
  untracked,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';

import { Api } from '../api/api';
import { getSnapshots } from '../api/fn/world/get-snapshots';
import { importSave } from '../api/fn/world/import-save';
import { SnapshotSummaryResponse } from '../api/models/snapshot-summary-response';
import { problemMessage } from '../shared/problem';
import { SnapshotDeletion } from './snapshot-deletion';
import { loaded } from '../shared/loaded';

/**
 * Which snapshot the World pages show, above the sidebar on every one of them (asked 2026-11-23): the sidebar's tabs are
 * all about that snapshot, so picking it is apart from them. Its name opens the list of snapshots, newest first, each
 * linking to its map, with a delete button; beside it, the import of a new save. It was the map's Snapshots tab.
 */
@Component({
  selector: 'app-snapshot-picker',
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressBarModule,
    MatTooltipModule,
  ],
  templateUrl: './snapshot-picker.html',
  styleUrl: './snapshot-picker.scss',
})
export class SnapshotPicker {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly deletion = inject(SnapshotDeletion);

  /** The snapshot the page shows; null on the World's start, before one is picked. */
  readonly current = input<string | null>(null);

  protected readonly snapshots = resource({ loader: () => this.api.invoke(getSnapshots) });
  protected readonly shown = computed(
    () => loaded(this.snapshots)?.find((snapshot) => snapshot.id === this.current()) ?? null,
  );
  protected readonly none = computed(() => loaded(this.snapshots)?.length === 0);
  protected readonly importing = signal(false);
  protected readonly loaded = loaded;

  constructor() {
    // A list that failed to load, as while the API is starting, is said where it shows on every screen: the picker is
    // as narrow as the rail when the sidebar is collapsed, as on a tablet. Nothing asks again by itself (step 98).
    effect(() => {
      if (!this.snapshots.error()) return;
      untracked(() =>
        this.snackBar
          .open("The snapshots couldn't be loaded. Is the API running?", 'Try again')
          .onAction()
          .subscribe(() => this.snapshots.reload()),
      );
    });
  }

  /** Deletes a snapshot after asking; when it's the one shown, back to the map with no snapshot loaded. */
  protected async delete(snapshot: SnapshotSummaryResponse): Promise<void> {
    if (!(await this.deletion.confirmAndDelete(snapshot))) return;
    if (snapshot.id === this.current()) await this.router.navigate(['/snapshots']);
    this.snapshots.reload();
  }

  protected async import(files: FileList | null): Promise<void> {
    const file = files?.item(0);
    if (!file) return;
    this.importing.set(true);
    try {
      const snapshot = await this.api.invoke(importSave, { body: { file } });
      this.snapshots.reload();
      await this.router.navigate(['/snapshots', snapshot.id, 'map']);
    } catch (error) {
      this.snackBar.open(`The save couldn't be imported: ${problemMessage(error)}`, 'Close');
    } finally {
      this.importing.set(false);
    }
  }
}
