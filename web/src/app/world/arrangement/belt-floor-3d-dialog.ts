import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSliderModule } from '@angular/material/slider';

import { ArrangementResponse } from '../../api/models/arrangement-response';
import { BeltFloor3d } from './belt-floor-3d';

export interface BeltFloor3dDialogData {
  arrangement: ArrangementResponse;
  colourOf: (factoryId: string) => string | undefined;
}

/** The belt floor of a run in 3D, over the page. */
@Component({
  selector: 'app-belt-floor-3d-dialog',
  imports: [MatButtonModule, MatDialogModule, MatIconModule, MatSliderModule, BeltFloor3d],
  template: `
    <div class="header">
      <h2 mat-dialog-title>The belt floor in 3D</h2>
      <button mat-icon-button mat-dialog-close aria-label="Close">
        <mat-icon>close</mat-icon>
      </button>
    </div>
    <p class="muted">
      Each belt and pipe at its height: a ramp where it turns 2 m up or down, a lift where it turns
      4 m or more; the factories as slabs at the ceiling. Drag to turn, scroll to zoom.
    </p>
    @if (highest() > 0) {
      <label class="levels">
        <span>{{ upTo() === null ? 'All levels' : 'Up to level ' + upTo() }}</span>
        <mat-slider min="0" [max]="highest()" step="1" discrete showTickMarks>
          <input
            matSliderThumb
            [value]="upTo() ?? highest()"
            (valueChange)="upTo.set($event)"
            aria-label="Up to level"
          />
        </mat-slider>
        <button
          mat-stroked-button
          type="button"
          [disabled]="upTo() === null"
          (click)="upTo.set(null)"
        >
          All levels
        </button>
        <span class="muted"
          >level 0 on the floor; below the chosen level faded, as built; above it hidden</span
        >
      </label>
    }
    <app-belt-floor-3d
      class="floor"
      [arrangement]="data.arrangement"
      [colourOf]="data.colourOf"
      [upTo]="upTo()"
    />
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      height: 100%;
      padding: 0 16px 16px;
      box-sizing: border-box;
    }
    .header {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .muted {
      margin: 0 0 8px 24px;
      color: var(--mat-sys-on-surface-variant);
    }
    .levels {
      display: flex;
      align-items: center;
      gap: 12px;
      margin: 0 0 8px 24px;
    }
    .levels mat-slider {
      width: 240px;
    }
    .floor {
      flex: 1;
      min-height: 0;
    }
  `,
})
export class BeltFloor3dDialog {
  protected readonly data = inject<BeltFloor3dDialogData>(MAT_DIALOG_DATA);

  /** The highest level any belt or pipe runs on. */
  protected readonly highest = computed(() =>
    Math.max(0, ...this.data.arrangement.belts.flatMap((belt) => belt.levels)),
  );

  /** The highest level shown; null for all. */
  protected readonly upTo = signal<number | null>(null);
}
