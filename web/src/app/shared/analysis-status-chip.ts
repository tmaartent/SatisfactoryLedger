import { Component, input } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { AnalysisStatus } from '../api/models/analysis-status';

/** Where a snapshot's analysis is (ADR-0017): pending while the analysis service is at it. */
@Component({
  selector: 'app-analysis-status-chip',
  imports: [MatChipsModule, MatProgressSpinnerModule],
  template: `
    <mat-chip [class]="status()" disableRipple>
      @if (status() === 'pending') {
        <mat-spinner matChipAvatar diameter="16" />
      }
      {{ labels[status()] }}
    </mat-chip>
  `,
  styles: `
    .ready { --mat-chip-elevated-container-color: color-mix(in srgb, var(--mat-sys-primary) 25%, transparent); }
    .failed { --mat-chip-elevated-container-color: var(--mat-sys-error-container); }
  `,
})
export class AnalysisStatusChip {
  readonly status = input.required<AnalysisStatus>();

  protected readonly labels: Record<AnalysisStatus, string> = {
    pending: 'Analysing…',
    ready: 'Analysed',
    failed: 'Analysis failed',
  };
}
