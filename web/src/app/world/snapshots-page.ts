import { DatePipe } from '@angular/common';
import { Component, effect, inject, resource, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';

import { Api } from '../api/api';
import { getSnapshots } from '../api/fn/world/get-snapshots';
import { importSave } from '../api/fn/world/import-save';
import { AnalysisStatusChip } from '../shared/analysis-status-chip';
import { problemMessage } from '../shared/problem';

/** Every imported snapshot, newest first, and the import of a new save. */
@Component({
  selector: 'app-snapshots-page',
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, MatTableModule, AnalysisStatusChip],
  templateUrl: './snapshots-page.html',
})
export class SnapshotsPage {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly snapshots = resource({ loader: () => this.api.invoke(getSnapshots) });
  protected readonly importing = signal(false);
  protected readonly columns = ['sessionName', 'savedAt', 'machines', 'logistics', 'analysis'];

  constructor() {
    // While an analysis is pending, ask again every two seconds.
    effect((onCleanup) => {
      if (!this.snapshots.value()?.some((snapshot) => snapshot.analysis === 'pending')) return;
      const timer = setTimeout(() => this.snapshots.reload(), 2000);
      onCleanup(() => clearTimeout(timer));
    });
  }

  protected async import(files: FileList | null): Promise<void> {
    const file = files?.item(0);
    if (!file) return;
    this.importing.set(true);
    try {
      const snapshot = await this.api.invoke(importSave, { body: { file } });
      await this.router.navigate(['/snapshots', snapshot.id]);
    } catch (error) {
      this.snackBar.open(`The save couldn't be imported: ${problemMessage(error)}`, 'Close');
    } finally {
      this.importing.set(false);
    }
  }
}
