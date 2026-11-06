import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, resource } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';

import { Api } from '../api/api';
import { analyseSnapshot } from '../api/fn/world/analyse-snapshot';
import { getSnapshot } from '../api/fn/world/get-snapshot';
import { traceFactories } from '../api/fn/world/trace-factories';
import { AnalysisStatusChip } from '../shared/analysis-status-chip';
import { CatalogNames } from '../shared/catalog-names';
import { problemMessage } from '../shared/problem';
import { DefineFactoryDialog, DefineFactoryDialogData } from './define-factory-dialog';
import { FindingsView } from './findings';

/** One snapshot: what it holds, its findings once the analysis is ready, and the factories traced in it. */
@Component({
  selector: 'app-snapshot-page',
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, MatListModule, MatProgressBarModule, MatTabsModule, AnalysisStatusChip, FindingsView],
  templateUrl: './snapshot-page.html',
})
export class SnapshotPage {
  private readonly api = inject(Api);
  private readonly dialog = inject(MatDialog);
  protected readonly names = inject(CatalogNames);

  /** From the route: /snapshots/:id. */
  readonly id = input.required<string>();

  protected readonly snapshot = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => this.api.invoke(getSnapshot, params),
  });

  /** Loaded once the analysis isn't pending any more; a failed one answers with its reason. */
  protected readonly analysis = resource({
    params: () => {
      const snapshot = this.snapshot.value();
      return snapshot && snapshot.analysis !== 'pending' ? { id: snapshot.id } : undefined;
    },
    loader: ({ params }) => this.api.invoke(analyseSnapshot, params),
  });

  protected readonly factories = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => this.api.invoke(traceFactories, params),
  });

  protected readonly problem = problemMessage;

  constructor() {
    // While the analysis service is at it, ask again every two seconds.
    effect((onCleanup) => {
      if (this.snapshot.value()?.analysis !== 'pending') return;
      const timer = setTimeout(() => this.snapshot.reload(), 2000);
      onCleanup(() => clearTimeout(timer));
    });
  }

  protected defineFactory(): void {
    const snapshot = this.snapshot.value();
    if (!snapshot) return;
    const data: DefineFactoryDialogData = { snapshotId: snapshot.id, world: snapshot.sessionName };
    this.dialog
      .open(DefineFactoryDialog, { data, width: '640px' })
      .afterClosed()
      .subscribe((defined) => {
        if (defined) this.factories.reload();
      });
  }
}
