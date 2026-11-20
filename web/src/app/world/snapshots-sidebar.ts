import { DatePipe } from '@angular/common';
import { Component, inject, input, resource, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
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

/**
 * Beside the map (asked 2026-11-19): the import of a new save, and every snapshot by its world's name, newest first,
 * linking to its map, with a delete button.
 */
@Component({
  selector: 'app-snapshots-sidebar',
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
  ],
  templateUrl: './snapshots-sidebar.html',
  styleUrl: './snapshots-sidebar.scss',
})
export class SnapshotsSidebar {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly deletion = inject(SnapshotDeletion);

  /** The snapshot whose map is shown; none on the world's start, before there is one. */
  readonly current = input<string>();

  protected readonly snapshots = resource({ loader: () => this.api.invoke(getSnapshots) });
  protected readonly importing = signal(false);

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
